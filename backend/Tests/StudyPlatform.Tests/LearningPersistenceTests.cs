using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Npgsql;
using StudyPlatform.Api.Data;
using StudyPlatform.Api.Domain.Enums;
using StudyPlatform.Api.Models;
using StudyPlatform.Api.Services.Learning;
using Xunit;

namespace StudyPlatform.Tests;

[Collection("LearningDatabase")]
public sealed class LearningPersistenceTests : IAsyncLifetime
{
    private const string DatabaseName = "trabalho_cae_learning_test";
    private readonly string connectionString = BuildConnectionString();
    private readonly LearningDbFactory factory;
    private Guid studentId;
    private Guid moduleId;
    private Guid conceptId;
    private Guid sessionId;

    public LearningPersistenceTests() => factory = new LearningDbFactory(connectionString);

    public async Task InitializeAsync()
    {
        await factory.ResetDatabaseAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var teacher = new Teacher { Name = "Learning Teacher", Email = "learning@example.test", PasswordHash = "not-used" };
        var classroom = new Classroom { Teacher = teacher, Name = "Learning Class", Code = "LEARN-CLASS" };
        var student = new Student { Classroom = classroom, EnrollmentNumber = "L001", IsActive = true, IsActivated = true, PasswordHash = "not-used" };
        var module = new Module { Teacher = teacher, Title = "Learning", Subject = "Test", Status = ModuleStatus.Published };
        var concept = new Concept { Module = module, Name = "Concept", Definition = "Definition" };
        var assignment = new ClassroomModule { Classroom = classroom, Module = module };
        var session = new StudySession { Student = student, Module = module, StartedAtUtc = DateTime.UtcNow };
        db.AddRange(teacher, classroom, student, module, concept, assignment, session);
        await db.SaveChangesAsync();
        studentId = student.Id; moduleId = module.Id; conceptId = concept.Id; sessionId = session.Id;
    }

    public async Task DisposeAsync() => await factory.DisposeAsync();

    [Fact]
    public async Task ProgressionPersistsLazyStateAndRequiresDueReviewInAnotherSessionToMaster()
    {
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero));
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var service = new LearningProgressionService(db, new FsrsService(), new PerformanceRatingService(), new ConceptEligibilityService(db, clock), clock);
        var initial = await service.GetStateAsync(studentId, conceptId);
        Assert.Equal(LearningState.New, initial.LearningState);
        Assert.Empty(await db.StudentConceptStates.ToListAsync());

        var state = await service.MarkPresentedAsync(studentId, conceptId);
        Assert.Equal(LearningState.Exposure, state.LearningState);
        state = await service.CompleteExposureAsync(studentId, conceptId);
        Assert.Equal(LearningState.Recognition, state.LearningState);
        state = await service.RecordAnswerAsync(studentId, conceptId, sessionId, true, 1, 0, 2000);
        Assert.Equal(LearningState.GuidedRecall, state.LearningState);
        state = await service.RecordAnswerAsync(studentId, conceptId, sessionId, true, 1, 0, 2000);
        Assert.Equal(LearningState.FreeRecall, state.LearningState);

        state = await service.RecordAnswerAsync(studentId, conceptId, sessionId, true, 1, 0, 2000);
        Assert.Equal(1, state.FreeRecallSuccessCount);
        Assert.Equal(LearningState.FreeRecall, state.LearningState);
        Assert.NotNull(state.DueAtUtc);
        Assert.Equal(sessionId, state.LastFreeRecallSuccessSessionId);
        Assert.Equal(FsrsRating.Easy, state.LastFsrsRating);

        state = await service.RecordAnswerAsync(studentId, conceptId, sessionId, true, 1, 0, 2000);
        Assert.Equal(1, state.FreeRecallSuccessCount);
        Assert.Equal(LearningState.FreeRecall, state.LearningState);
        var earlyDue = state.DueAtUtc!.Value;

        var secondSession = new StudySession { StudentId = studentId, ModuleId = moduleId, StartedAtUtc = clock.GetUtcNow().UtcDateTime };
        db.StudySessions.Add(secondSession);
        await db.SaveChangesAsync();
        clock.Set(earlyDue.AddMinutes(-1));
        state = await service.RecordAnswerAsync(studentId, conceptId, secondSession.Id, true, 1, 0, 2000);
        Assert.Equal(1, state.FreeRecallSuccessCount);
        Assert.Equal(LearningState.FreeRecall, state.LearningState);
        Assert.False(await new ConceptEligibilityService(db, clock).IsDueAsync(studentId, conceptId));

        var finalDue = state.DueAtUtc!.Value;
        clock.Set(finalDue.AddSeconds(-1));
        Assert.False(await new ConceptEligibilityService(db, clock).IsDueAsync(studentId, conceptId));
        clock.Set(finalDue.AddSeconds(1));
        Assert.True(await new ConceptEligibilityService(db, clock).IsDueAsync(studentId, conceptId));
        state = await service.RecordAnswerAsync(studentId, conceptId, secondSession.Id, true, 1, 0, 2000);
        Assert.Equal(2, state.FreeRecallSuccessCount);
        Assert.Equal(LearningState.Mastered, state.LearningState);
        Assert.True(await new ConceptEligibilityService(db, clock).IsDueAsync(studentId, conceptId) is false);
        var persisted = await db.StudentConceptStates.AsNoTracking().SingleAsync();
        Assert.Equal(LearningState.Mastered, persisted.LearningState);
    }

    [Fact]
    public async Task ErrorsRegressCorrectlyAndResetFreeRecallSuccessChain()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now = new ManualTimeProvider(new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero));
        var service = new LearningProgressionService(db, new FsrsService(), new PerformanceRatingService(), new ConceptEligibilityService(db, now), now);
        var session = await db.StudySessions.SingleAsync();
        var state = new StudentConceptState { StudentId = studentId, ConceptId = conceptId, LearningState = LearningState.Recognition };
        db.StudentConceptStates.Add(state); await db.SaveChangesAsync();
        state = await service.RecordAnswerAsync(studentId, conceptId, session.Id, false, 1, 0, 1000);
        Assert.Equal(LearningState.Recognition, state.LearningState);
        Assert.Equal(FsrsRating.Again, state.LastFsrsRating);
        state.LearningState = LearningState.GuidedRecall; await db.SaveChangesAsync();
        state = await service.RecordAnswerAsync(studentId, conceptId, session.Id, false, 1, 0, 1000);
        Assert.Equal(LearningState.Recognition, state.LearningState);
        state.LearningState = LearningState.FreeRecall; state.FreeRecallSuccessCount = 1; state.LastFreeRecallSuccessSessionId = session.Id; await db.SaveChangesAsync();
        state = await service.RecordAnswerAsync(studentId, conceptId, session.Id, false, 1, 0, 1000);
        Assert.Equal(LearningState.GuidedRecall, state.LearningState);
        Assert.Equal(0, state.FreeRecallSuccessCount);
        state.LearningState = LearningState.Mastered; await db.SaveChangesAsync();
        state = await service.RecordAnswerAsync(studentId, conceptId, session.Id, true, 1, 0, 1000);
        Assert.Equal(LearningState.Mastered, state.LearningState);
        state = await service.RecordAnswerAsync(studentId, conceptId, session.Id, false, 1, 0, 1000);
        Assert.Equal(LearningState.FreeRecall, state.LearningState);
    }

    [Fact]
    public async Task EligibilityRequiresPublishedAssignedActiveConceptAndEveryPrerequisiteMastered()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var teacher = await db.Teachers.SingleAsync();
        var a = await db.Concepts.SingleAsync();
        var b = new Concept { ModuleId = moduleId, Name = "B", Definition = "B" };
        var c = new Concept { ModuleId = moduleId, Name = "C", Definition = "C" };
        db.Concepts.AddRange(b, c); await db.SaveChangesAsync();
        db.ConceptPrerequisites.AddRange(
            new ConceptPrerequisite { ConceptId = b.Id, PrerequisiteConceptId = a.Id, ModuleId = moduleId },
            new ConceptPrerequisite { ConceptId = c.Id, PrerequisiteConceptId = a.Id, ModuleId = moduleId },
            new ConceptPrerequisite { ConceptId = c.Id, PrerequisiteConceptId = b.Id, ModuleId = moduleId });
        await db.SaveChangesAsync();
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero));
        var eligibility = new ConceptEligibilityService(db, clock);
        Assert.True(await eligibility.CanIntroduceAsync(studentId, a.Id));
        Assert.False(await eligibility.CanIntroduceAsync(studentId, b.Id));
        db.StudentConceptStates.Add(new StudentConceptState { StudentId = studentId, ConceptId = a.Id, LearningState = LearningState.FreeRecall });
        await db.SaveChangesAsync();
        Assert.False(await eligibility.CanIntroduceAsync(studentId, b.Id));
        db.StudentConceptStates.Single(x => x.ConceptId == a.Id).LearningState = LearningState.Mastered;
        db.StudentConceptStates.Add(new StudentConceptState { StudentId = studentId, ConceptId = b.Id, LearningState = LearningState.Mastered });
        await db.SaveChangesAsync();
        Assert.True(await eligibility.CanIntroduceAsync(studentId, c.Id));
        db.StudentConceptStates.Single(x => x.ConceptId == b.Id).LearningState = LearningState.Recognition;
        await db.SaveChangesAsync();
        Assert.False(await eligibility.CanIntroduceAsync(studentId, c.Id));
        a.IsActive = false; await db.SaveChangesAsync();
        Assert.False(await eligibility.CanIntroduceAsync(studentId, a.Id));
    }

    private static string BuildConnectionString()
    {
        var raw = Environment.GetEnvironmentVariable("STUDYPLATFORM_LEARNING_TEST_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=trabalho_cae_learning_test;Username=trabalho_cae;Password=local_dev_password";
        var builder = new NpgsqlConnectionStringBuilder(raw);
        if (builder.Database != DatabaseName) throw new InvalidOperationException($"Tests require dedicated {DatabaseName} database.");
        return builder.ConnectionString;
    }
}

public sealed class ManualTimeProvider(DateTimeOffset initial) : TimeProvider
{
    private DateTimeOffset now = initial;
    public override DateTimeOffset GetUtcNow() => now;
    public void Set(DateTimeOffset value) => now = value;
}

public sealed class LearningDbFactory(string connectionString) : Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>
{
    private const string DatabaseName = "trabalho_cae_learning_test";
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:DefaultConnection"] = connectionString }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(connectionString));
        });
    }

    public async Task ResetDatabaseAsync()
    {
        var cs = new NpgsqlConnectionStringBuilder(connectionString);
        if (cs.Database != DatabaseName) throw new InvalidOperationException("Refusing to clean a non-learning-test database.");
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (db.Database.GetDbConnection().Database != DatabaseName) throw new InvalidOperationException("Refusing to clean a non-learning-test database.");
        await db.Database.MigrateAsync();
        await db.Database.ExecuteSqlRawAsync("TRUNCATE TABLE \"Teachers\", \"Classrooms\", \"Students\" CASCADE");
    }
}

[CollectionDefinition("LearningDatabase", DisableParallelization = true)]
public sealed class LearningDatabaseCollection;
