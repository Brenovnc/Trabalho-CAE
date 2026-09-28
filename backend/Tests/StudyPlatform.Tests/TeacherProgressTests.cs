using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using StudyPlatform.Api.Data;
using StudyPlatform.Api.Domain.Enums;
using StudyPlatform.Api.DTOs.Auth;
using StudyPlatform.Api.DTOs.Progress;
using StudyPlatform.Api.Models;
using StudyPlatform.Api.Services.Progress;
using Xunit;

namespace StudyPlatform.Tests;

[Collection("LearningDatabase")]
public sealed class TeacherProgressTests : IAsyncLifetime
{
    private const string DatabaseName = "trabalho_cae_learning_test";
    private static readonly DateTime Now = new(2026, 9, 28, 15, 0, 0, DateTimeKind.Utc);
    private readonly string connectionString = BuildConnectionString();
    private readonly LearningDbFactory factory;
    private Guid teacherId;
    private Guid classroomId;
    private Guid studentId;
    private Guid secondStudentId;
    private Guid foreignClassroomId;
    private Guid foreignStudentId;

    public TeacherProgressTests() => factory = new LearningDbFactory(connectionString);

    public async Task InitializeAsync()
    {
        await factory.ResetDatabaseAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var teacher = new Teacher { Name = "Progress teacher", Email = "progress@example.test" };
        teacher.PasswordHash = new PasswordHasher<Teacher>().HashPassword(teacher, "TeacherPass123!");
        var classroom = new Classroom { Teacher = teacher, Name = "Networks", Code = "PROGRESS" };
        var student = new Student { Classroom = classroom, EnrollmentNumber = "001", Name = "João", IsActive = true, IsActivated = true };
        var secondStudent = new Student { Classroom = classroom, EnrollmentNumber = "002", Name = null, IsActive = true, IsActivated = true };
        var inactiveStudent = new Student { Classroom = classroom, EnrollmentNumber = "003", Name = "Inactive", IsActive = false, IsActivated = true };
        var module = new Module { Teacher = teacher, Title = "Published networks", Subject = "Networks", Status = ModuleStatus.Published };
        var concepts = Enumerable.Range(1, 10).Select(index =>
            new Concept { Module = module, Name = $"Concept {index}", Definition = "Definition" }).ToArray();
        var inactiveConcepts = Enumerable.Range(1, 2).Select(index =>
            new Concept { Module = module, Name = $"Inactive {index}", Definition = "Old definition", IsActive = false }).ToArray();
        var draft = new Module { Teacher = teacher, Title = "Draft networks", Subject = "Networks", Status = ModuleStatus.Draft };
        draft.Concepts.Add(new Concept { Name = "Draft concept", Definition = "Not studied yet." });
        var archived = new Module { Teacher = teacher, Title = "Archived networks", Subject = "Networks", Status = ModuleStatus.Archived };
        archived.Concepts.Add(new Concept { Name = "Archived concept", Definition = "Not studied now." });
        var foreignTeacher = new Teacher { Name = "Foreign teacher", Email = "foreign-progress@example.test", PasswordHash = "unused" };
        var foreignClassroom = new Classroom { Teacher = foreignTeacher, Name = "Foreign room", Code = "FOREIGN-PROGRESS" };
        var foreignStudent = new Student { Classroom = foreignClassroom, EnrollmentNumber = "F001", Name = "Foreign student", IsActive = true, IsActivated = true };
        foreignStudent.PasswordHash = new PasswordHasher<Student>().HashPassword(foreignStudent, "StudentPass123!");

        db.AddRange(teacher, classroom, student, secondStudent, inactiveStudent, module, draft, archived,
            new ClassroomModule { Classroom = classroom, Module = module, AssignedAtUtc = Now },
            new ClassroomModule { Classroom = classroom, Module = draft, AssignedAtUtc = Now },
            new ClassroomModule { Classroom = classroom, Module = archived, AssignedAtUtc = Now },
            foreignTeacher, foreignClassroom, foreignStudent);
        db.Concepts.AddRange(concepts);
        db.Concepts.AddRange(inactiveConcepts);
        await db.SaveChangesAsync();
        for (var index = 0; index < 9; index++)
        {
            var state = index switch
            {
                0 => LearningState.Mastered,
                1 => LearningState.Mastered,
                2 => LearningState.Mastered,
                3 => LearningState.Mastered,
                4 => LearningState.Mastered,
                5 => LearningState.Exposure,
                6 => LearningState.Recognition,
                7 => LearningState.New,
                _ => LearningState.GuidedRecall,
            };
            var due = index switch
            {
                0 => Now,
                1 => Now.AddDays(-1),
                5 => Now.AddDays(-1),
                6 => Now.AddDays(1),
                7 => Now.AddDays(-1),
                _ => (DateTime?)null,
            };
            db.StudentConceptStates.Add(new StudentConceptState { StudentId = student.Id, ConceptId = concepts[index].Id, LearningState = state, DueAtUtc = due });
        }
        db.StudentConceptStates.Add(new StudentConceptState
        {
            StudentId = student.Id, ConceptId = inactiveConcepts[0].Id, LearningState = LearningState.Mastered, DueAtUtc = Now.AddDays(-2),
        });
        await db.SaveChangesAsync();

        teacherId = teacher.Id;
        classroomId = classroom.Id;
        studentId = student.Id;
        secondStudentId = secondStudent.Id;
        foreignClassroomId = foreignClassroom.Id;
        foreignStudentId = foreignStudent.Id;
    }

    public async Task DisposeAsync() => await factory.DisposeAsync();

    [Fact]
    public async Task DashboardAndClassroomDetailsCountOnlyOwnedStudyableData()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var service = new TeacherProgressService(db, new ManualTimeProvider(new DateTimeOffset(Now)));

        var dashboard = await service.GetDashboardAsync(teacherId, default);
        Assert.Equal(new TeacherDashboardResponse(2, 1, 2), dashboard);

        var progress = await service.GetClassroomProgressAsync(teacherId, classroomId, default);
        Assert.Equal(10, progress.TotalActiveConcepts);
        Assert.Equal(new[] { "001", "002", "003" }, progress.Students.Select(student => student.EnrollmentNumber));
        var first = Assert.Single(progress.Students, student => student.StudentId == studentId);
        Assert.Equal("João", first.Name);
        Assert.True(first.IsActive);
        Assert.Equal(5, first.MasteredConcepts);
        Assert.Equal(3, first.LearningConcepts);
        Assert.Equal(2, first.NotStartedConcepts);
        Assert.Equal(3, first.PendingReviews);
        Assert.Equal(50, first.ProgressPercent);
        var noHistory = Assert.Single(progress.Students, student => student.StudentId == secondStudentId);
        Assert.Null(noHistory.Name);
        Assert.Equal(0, noHistory.ProgressPercent);
        Assert.Equal(0, noHistory.PendingReviews);

        var detail = await service.GetStudentProgressAsync(teacherId, classroomId, studentId, default);
        var module = Assert.Single(detail.Modules);
        Assert.Equal("Published networks", module.Title);
        Assert.Equal(10, module.ActiveConcepts);
        Assert.Equal(5, module.MasteredConcepts);
        Assert.Equal(3, module.LearningConcepts);
        Assert.Equal(2, module.NotStartedConcepts);
        Assert.Equal(3, module.PendingReviews);
        Assert.Equal(50, module.ProgressPercent);
    }

    [Fact]
    public async Task DashboardAndProgressEndpointsEnforceRoleAndOwnership()
    {
        using var teacherClient = CreateClient();
        await LoginAsync(teacherClient, "/api/auth/teachers/login", new { email = "progress@example.test", password = "TeacherPass123!" });
        var dashboard = await teacherClient.GetFromJsonAsync<TeacherDashboardResponse>("/api/teacher/dashboard");
        Assert.Equal(new TeacherDashboardResponse(2, 1, 2), dashboard);
        Assert.Equal(HttpStatusCode.OK, (await teacherClient.GetAsync($"/api/classrooms/{classroomId}/progress")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await teacherClient.GetAsync($"/api/classrooms/{classroomId}/students/{studentId}/progress")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await teacherClient.GetAsync($"/api/classrooms/{foreignClassroomId}/progress")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await teacherClient.GetAsync($"/api/classrooms/{classroomId}/students/{foreignStudentId}/progress")).StatusCode);

        using var studentClient = CreateClient();
        await LoginAsync(studentClient, "/api/auth/students/login", new { classroomCode = "FOREIGN-PROGRESS", enrollmentNumber = "F001", password = "StudentPass123!" });
        Assert.Equal(HttpStatusCode.Forbidden, (await studentClient.GetAsync("/api/teacher/dashboard")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await studentClient.GetAsync($"/api/classrooms/{classroomId}/progress")).StatusCode);
    }

    [Fact]
    public async Task StudentLoginAndAuthenticatedRequestsUpdateLastAccessInUtc()
    {
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var student = await db.Students.SingleAsync(item => item.Id == studentId);
            student.PasswordHash = new PasswordHasher<Student>().HashPassword(student, "StudentPass123!");
            await db.SaveChangesAsync();
        }
        using var client = CreateClient();
        await LoginAsync(client, "/api/auth/students/login", new { classroomCode = "PROGRESS", enrollmentNumber = "001", password = "StudentPass123!" });
        await client.GetAsync("/api/student/modules");
        await using var verifyScope = factory.Services.CreateAsyncScope();
        var lastAccess = await verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Students
            .Where(student => student.Id == studentId).Select(student => student.LastAccessAtUtc).SingleAsync();
        Assert.NotNull(lastAccess);
        Assert.Equal(DateTimeKind.Utc, lastAccess!.Value.Kind);
    }

    private static async Task LoginAsync(HttpClient client, string path, object requestBody)
    {
        var csrf = await client.GetFromJsonAsync<CsrfTokenResponse>("/api/auth/csrf");
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(requestBody) };
        request.Headers.Add("X-CSRF-TOKEN", csrf!.Token);
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private HttpClient CreateClient() => factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("http://localhost"), HandleCookies = true, AllowAutoRedirect = false,
    });

    private static string BuildConnectionString()
    {
        var raw = Environment.GetEnvironmentVariable("STUDYPLATFORM_LEARNING_TEST_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=trabalho_cae_learning_test;Username=trabalho_cae;Password=local_dev_password";
        var builder = new NpgsqlConnectionStringBuilder(raw);
        if (builder.Database != DatabaseName) throw new InvalidOperationException($"Tests require the dedicated {DatabaseName} database.");
        return builder.ConnectionString;
    }
}
