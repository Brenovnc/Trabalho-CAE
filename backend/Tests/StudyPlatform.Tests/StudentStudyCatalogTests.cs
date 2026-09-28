using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using StudyPlatform.Api.Data;
using StudyPlatform.Api.Domain.Enums;
using StudyPlatform.Api.Models;
using StudyPlatform.Api.Services.Learning;
using Xunit;

namespace StudyPlatform.Tests;

[Collection("LearningDatabase")]
public sealed class StudentStudyCatalogTests : IAsyncLifetime
{
    private const string TestDatabase = "trabalho_cae_learning_test";
    private readonly string connectionString = CreateConnectionString();
    private readonly LearningDbFactory factory;
    private Guid studentId;
    private Guid moduleId;
    private Guid conceptId;

    public StudentStudyCatalogTests() => factory = new LearningDbFactory(connectionString);

    public async Task InitializeAsync()
    {
        await factory.ResetDatabaseAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var teacher = new Teacher { Name = "Catalog teacher", Email = "catalog@example.test", PasswordHash = "not-used" };
        var classroom = new Classroom { Teacher = teacher, Name = "Catalog room", Code = "CATALOG-ROOM" };
        var student = new Student { Classroom = classroom, EnrollmentNumber = "C001", IsActive = true, IsActivated = true, PasswordHash = "not-used" };
        var module = new Module { Teacher = teacher, Title = "Assigned module", Subject = "Networks", Description = "Module summary", Status = ModuleStatus.Published };
        var concept = new Concept { Module = module, Name = "DNS", Definition = "DNS maps names to addresses." };
        concept.Keywords.Add(new ConceptKeyword { Value = "DNS" });
        concept.Clues.Add(new ConceptClue { Position = 0, Text = "A name service." });
        var assignment = new ClassroomModule { Classroom = classroom, Module = module };
        db.AddRange(teacher, classroom, student, module, concept, assignment);
        await db.SaveChangesAsync();
        studentId = student.Id;
        moduleId = module.Id;
        conceptId = concept.Id;
    }

    public async Task DisposeAsync() => await factory.DisposeAsync();

    [Fact]
    public async Task CatalogReturnsOnlyPublishedModulesAssignedToTheStudentsClassroom()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var module = await db.Modules.SingleAsync(x => x.Id == moduleId);
        var classroomId = await db.Students.Where(x => x.Id == studentId).Select(x => x.ClassroomId).SingleAsync();
        var unassigned = new Module { TeacherId = module.TeacherId, Title = "Other classroom", Subject = "Networks", Status = ModuleStatus.Published };
        var draft = new Module { TeacherId = module.TeacherId, Title = "Draft", Subject = "Networks", Status = ModuleStatus.Draft };
        db.Modules.AddRange(unassigned, draft);
        db.ClassroomModules.Add(new ClassroomModule { ClassroomId = classroomId, Module = draft });
        await db.SaveChangesAsync();

        var service = new StudentStudyCatalogService(db);
        var modules = await service.ListModulesAsync(studentId, default);
        Assert.Single(modules);
        Assert.Equal(moduleId, modules[0].Id);
        Assert.Equal("Module summary", modules[0].Description);
        await Assert.ThrowsAsync<StudyPlatform.Api.Exceptions.ApiException>(() => service.GetModuleAsync(studentId, unassigned.Id, default));
        await Assert.ThrowsAsync<StudyPlatform.Api.Exceptions.ApiException>(() => service.GetModuleAsync(studentId, draft.Id, default));
    }

    [Fact]
    public async Task ModuleSummaryUsesActiveConceptsAndReturnsProgressReviewCountsAndNextDueTime()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var module = await db.Modules.SingleAsync(item => item.Id == moduleId);
        var now = DateTime.UtcNow;
        var concepts = Enumerable.Range(2, 10).Select(index => new Concept
        {
            Module = module, Name = $"Concept {index}", Definition = "Definition",
            IsActive = index != 11,
        }).ToArray();
        db.Concepts.AddRange(concepts);
        await db.SaveChangesAsync();
        var active = await db.Concepts.Where(concept => concept.ModuleId == moduleId && concept.IsActive).OrderBy(concept => concept.Name).ToListAsync();
        var stateDefinitions = new (LearningState State, DateTime? Due)[]
        {
            (LearningState.Mastered, now.AddHours(3)), (LearningState.Mastered, null), (LearningState.Mastered, null),
            (LearningState.Exposure, now.AddDays(-1)), (LearningState.Recognition, now.AddHours(2)),
            (LearningState.GuidedRecall, null), (LearningState.FreeRecall, now.AddDays(-2)), (LearningState.New, null),
        };
        for (var index = 0; index < stateDefinitions.Length; index++)
            db.StudentConceptStates.Add(new StudentConceptState
            {
                StudentId = studentId, ConceptId = active[index].Id,
                LearningState = stateDefinitions[index].State, DueAtUtc = stateDefinitions[index].Due,
            });
        await db.SaveChangesAsync();

        var response = await new StudentStudyCatalogService(db).GetModuleAsync(studentId, moduleId, default);
        Assert.Equal(10, response.ActiveConcepts);
        Assert.Equal(3, response.MasteredConcepts);
        Assert.Equal(4, response.LearningConcepts);
        Assert.Equal(3, response.NotStartedConcepts);
        Assert.Equal(30, response.ProgressPercent);
        Assert.Equal(2, response.PendingReviews);
        Assert.InRange(response.NextReviewAtUtc!.Value, now.AddHours(1).AddMinutes(-2), now.AddHours(2).AddMinutes(2));
        Assert.DoesNotContain(response.Concepts!, item => item.Name == "Concept 11");

        var student = await db.Students.SingleAsync(item => item.Id == studentId);
        student.IsActive = false;
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<StudyPlatform.Api.Exceptions.ApiException>(() => new StudentStudyCatalogService(db).GetModuleAsync(studentId, moduleId, default));
    }

    [Fact]
    public async Task ExposurePayloadDoesNotRevealKeywordsBeforeBackendRevealAndRetainsPresentationTime()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var sessions = scope.ServiceProvider.GetRequiredService<StudySessionService>();
        var start = await sessions.StartAsync(studentId, moduleId, default);
        var activity = Assert.IsType<StudyPlatform.Api.DTOs.Learning.PresentedActivityResponse>(start.Activity);
        Assert.Equal("EXPOSURE", activity.Type);
        Assert.False(activity.StartedAtUtc.Equals(default));
        Assert.DoesNotContain("DNS", activity.Payload.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, activity.Payload.GetProperty("revealedCount").GetInt32());

        await sessions.RevealNextHintAsync(studentId, start.SessionId, activity.PresentationId, default);
        var resumed = await sessions.GetAsync(studentId, start.SessionId, default);
        Assert.Equal(activity.PresentationId, resumed.Activity!.PresentationId);
        Assert.Equal(activity.StartedAtUtc, resumed.Activity.StartedAtUtc);
        Assert.Contains("DNS", resumed.Activity.Payload.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, resumed.Activity.Payload.GetProperty("revealedCount").GetInt32());
    }

    private static string CreateConnectionString()
    {
        var value = Environment.GetEnvironmentVariable("STUDYPLATFORM_LEARNING_TEST_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=trabalho_cae_learning_test;Username=trabalho_cae;Password=local_dev_password";
        var builder = new NpgsqlConnectionStringBuilder(value);
        if (builder.Database != TestDatabase)
            throw new InvalidOperationException($"Student catalog tests require the dedicated {TestDatabase} database.");
        return builder.ConnectionString;
    }
}
