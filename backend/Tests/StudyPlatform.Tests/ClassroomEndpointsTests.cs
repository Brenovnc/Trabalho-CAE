using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StudyPlatform.Api.Data;
using StudyPlatform.Api.Domain.Enums;
using StudyPlatform.Api.DTOs.Auth;
using StudyPlatform.Api.DTOs.Classrooms;
using StudyPlatform.Api.DTOs.Students;
using StudyPlatform.Api.Models;
using Xunit;

namespace StudyPlatform.Tests;

public sealed class ClassroomEndpointsTests : IAsyncLifetime
{
    private readonly ClassroomsApiFactory factory = new();
    private HttpClient teacherClient = null!;
    private Guid teacherId;

    public async Task InitializeAsync()
    {
        await factory.ResetDatabaseAsync();
        teacherClient = CreateClient();
        var csrf = await teacherClient.GetFromJsonAsync<CsrfTokenResponse>("/api/auth/csrf");
        var registration = await SendAsync<AuthenticatedUserResponse>(teacherClient, HttpMethod.Post, "/api/auth/teachers/register",
            new { name = "Classroom Teacher", email = "classroom@example.test", password = "secure-password", confirmPassword = "secure-password" });
        teacherId = registration.Id;
    }

    public async Task DisposeAsync() { teacherClient.Dispose(); await factory.DisposeAsync(); }

    [Fact]
    public async Task ClassroomCodeIsNormalizedUniqueCaseInsensitiveAndValidated()
    {
        var created = await SendAsync<ClassroomDetailsResponse>(teacherClient, HttpMethod.Post, "/api/classrooms", new { name = "Turma A", code = "turma-a" });
        Assert.Equal("TURMA-A", created.Code);
        var duplicate = await SendRawAsync(teacherClient, HttpMethod.Post, "/api/classrooms", new { name = "Outra", code = "Turma-a" });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        var invalid = await SendRawAsync(teacherClient, HttpMethod.Post, "/api/classrooms", new { name = "Invalida", code = "AB--C" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Single(await teacherClient.GetFromJsonAsync<List<ClassroomSummaryResponse>>("/api/classrooms") ?? []);
    }

    [Fact]
    public async Task OwnershipAndRoleAreEnforcedForClassrooms()
    {
        var classroom = await CreateClassroomAsync();
        var outsider = CreateClient();
        var csrf = await outsider.GetFromJsonAsync<CsrfTokenResponse>("/api/auth/csrf");
        await SendAsync<AuthenticatedUserResponse>(outsider, HttpMethod.Post, "/api/auth/teachers/register",
            new { name = "Other", email = "other@example.test", password = "secure-password", confirmPassword = "secure-password" });
        Assert.Empty(await outsider.GetFromJsonAsync<List<ClassroomSummaryResponse>>("/api/classrooms") ?? []);
        var foreignClassroom = await SendAsync<ClassroomDetailsResponse>(outsider, HttpMethod.Post, "/api/classrooms", new { name = "Foreign class", code = "FOREIGN" });
        var foreignStudent = await SendAsync<StudentCredentialsResponse>(outsider, HttpMethod.Post, $"/api/classrooms/{foreignClassroom.Id}/students", new { enrollmentNumber = "foreign-enrollment", name = (string?)null });
        Assert.Equal(HttpStatusCode.NotFound, (await SendRawAsync(teacherClient, HttpMethod.Post, $"/api/classrooms/{foreignClassroom.Id}/students/{foreignStudent.StudentId}/reset-access", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync($"/api/classrooms/{classroom.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendRawAsync(outsider, HttpMethod.Put, $"/api/classrooms/{classroom.Id}", new { name = "Stolen", code = "STOLEN" })).StatusCode);
        outsider.Dispose();
    }

    [Fact]
    public async Task StudentCredentialIsOneTimeHashedActivatesAndCanBeReset()
    {
        var classroom = await CreateClassroomAsync();
        var generated = await SendAsync<StudentCredentialsResponse>(teacherClient, HttpMethod.Post,
            $"/api/classrooms/{classroom.Id}/students", new { enrollmentNumber = "12345", name = "Ada" });
        Assert.Matches("^[A-HJ-NP-Z2-9]{6}$", generated.TemporaryAccessCode);
        Assert.Equal("12345", generated.EnrollmentNumber);
        Assert.InRange(generated.ExpiresAtUtc - DateTime.UtcNow, TimeSpan.FromDays(6.9), TimeSpan.FromDays(7.1));
        var student = await FindStudentAsync(generated.StudentId);
        Assert.Null(student.PasswordHash); Assert.False(student.IsActivated); Assert.True(student.IsActive);
        Assert.Equal(0, student.TemporaryAccessCodeFailedAttempts);
        Assert.NotNull(student.TemporaryAccessCodeHash); Assert.DoesNotContain(generated.TemporaryAccessCode, student.TemporaryAccessCodeHash);
        Assert.DoesNotContain("Hash", System.Text.Json.JsonSerializer.Serialize(generated));

        using var studentClient = CreateClient();
        var activated = await SendRawAsync(studentClient, HttpMethod.Post, "/api/auth/students/activate", new
        {
            classroomCode = "REDES2026", enrollmentNumber = "12345", temporaryCode = generated.TemporaryAccessCode,
            password = "student-password", confirmPassword = "student-password",
        });
        Assert.Equal(HttpStatusCode.OK, activated.StatusCode);
        Assert.Null((await FindStudentAsync(generated.StudentId)).TemporaryAccessCodeHash);
        var login = await SendRawAsync(studentClient, HttpMethod.Post, "/api/auth/students/login", new { classroomCode = "redes2026", enrollmentNumber = "12345", password = "student-password" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        await SendRawAsync(studentClient, HttpMethod.Post, "/api/auth/logout", new { });

        var newCode = await SendAsync<StudentCredentialsResponse>(teacherClient, HttpMethod.Post,
            $"/api/classrooms/{classroom.Id}/students/{generated.StudentId}/reset-access", new { });
        var resetStudent = await FindStudentAsync(generated.StudentId);
        Assert.Null(resetStudent.PasswordHash); Assert.False(resetStudent.IsActivated); Assert.Equal(0, resetStudent.TemporaryAccessCodeFailedAttempts);
        Assert.NotEqual(student.TemporaryAccessCodeHash, resetStudent.TemporaryAccessCodeHash);
        var oldCode = await SendRawAsync(studentClient, HttpMethod.Post, "/api/auth/students/activate", new
        {
            classroomCode = "REDES2026", enrollmentNumber = "12345", temporaryCode = generated.TemporaryAccessCode,
            password = "student-password", confirmPassword = "student-password",
        });
        Assert.Equal(HttpStatusCode.Unauthorized, oldCode.StatusCode);
        var reactivated = await SendRawAsync(studentClient, HttpMethod.Post, "/api/auth/students/activate", new
        {
            classroomCode = "REDES2026", enrollmentNumber = "12345", temporaryCode = newCode.TemporaryAccessCode,
            password = "student-password-new", confirmPassword = "student-password-new",
        });
        Assert.Equal(HttpStatusCode.OK, reactivated.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendRawAsync(studentClient, HttpMethod.Post, "/api/auth/students/login", new { classroomCode = "REDES2026", enrollmentNumber = "12345", password = "student-password" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SendRawAsync(studentClient, HttpMethod.Post, "/api/auth/students/login", new { classroomCode = "REDES2026", enrollmentNumber = "12345", password = "student-password-new" })).StatusCode);
    }

    [Fact]
    public async Task EnrollmentIsUniqueWithinClassroomAndDeactivationPreservesStudentRow()
    {
        var firstClass = await CreateClassroomAsync("A01"); var secondClass = await CreateClassroomAsync("A02");
        var first = await SendAsync<StudentCredentialsResponse>(teacherClient, HttpMethod.Post, $"/api/classrooms/{firstClass.Id}/students", new { enrollmentNumber = "same", name = "" });
        var second = await SendAsync<StudentCredentialsResponse>(teacherClient, HttpMethod.Post, $"/api/classrooms/{secondClass.Id}/students", new { enrollmentNumber = "same", name = (string?)null });
        Assert.Null(first.Name); Assert.NotEqual(first.StudentId, second.StudentId);
        var duplicate = await SendRawAsync(teacherClient, HttpMethod.Post, $"/api/classrooms/{firstClass.Id}/students", new { enrollmentNumber = "same", name = "Duplicate" });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        using var studentClient = CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await SendRawAsync(studentClient, HttpMethod.Post, "/api/auth/students/activate", new { classroomCode = "A01", enrollmentNumber = "same", temporaryCode = first.TemporaryAccessCode, password = "student-password", confirmPassword = "student-password" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await studentClient.GetAsync("/api/classrooms")).StatusCode);
        await SendRawAsync(teacherClient, HttpMethod.Post, $"/api/classrooms/{firstClass.Id}/students/{first.StudentId}/deactivate", new { });
        Assert.Equal(HttpStatusCode.Unauthorized, (await studentClient.GetAsync("/api/auth/me")).StatusCode);
        Assert.False((await FindStudentAsync(first.StudentId)).IsActive);
        var login = await SendRawAsync(CreateClient(), HttpMethod.Post, "/api/auth/students/login", new { classroomCode = "A01", enrollmentNumber = "same", password = "anything" });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.True(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Students.AnyAsync(x => x.Id == first.StudentId));
    }

    [Fact]
    public async Task OnlyPublishedOwnedModulesCanBeAssociatedAndAssociationCanBeRemoved()
    {
        var classroom = await CreateClassroomAsync();
        var published = new Module { TeacherId = teacherId, Title = "Published", Subject = "Networks", Status = ModuleStatus.Published };
        var draft = new Module { TeacherId = teacherId, Title = "Draft", Subject = "Networks", Status = ModuleStatus.Draft };
        var archived = new Module { TeacherId = teacherId, Title = "Archived", Subject = "Networks", Status = ModuleStatus.Archived };
        var foreignTeacher = new Teacher { Name = "Foreign", Email = "foreign@example.test", PasswordHash = "test" };
        var foreign = new Module { Teacher = foreignTeacher, Title = "Foreign", Subject = "Networks", Status = ModuleStatus.Published };
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(); db.Modules.AddRange(published, draft, archived, foreign); await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.NoContent, (await SendRawAsync(teacherClient, HttpMethod.Post, $"/api/classrooms/{classroom.Id}/modules/{published.Id}", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await SendRawAsync(teacherClient, HttpMethod.Post, $"/api/classrooms/{classroom.Id}/modules/{draft.Id}", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await SendRawAsync(teacherClient, HttpMethod.Post, $"/api/classrooms/{classroom.Id}/modules/{archived.Id}", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendRawAsync(teacherClient, HttpMethod.Post, $"/api/classrooms/{classroom.Id}/modules/{foreign.Id}", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await SendRawAsync(teacherClient, HttpMethod.Post, $"/api/classrooms/{classroom.Id}/modules/{published.Id}", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await SendRawAsync(teacherClient, HttpMethod.Delete, $"/api/classrooms/{classroom.Id}/modules/{published.Id}", null)).StatusCode);
    }

    [Fact]
    public async Task ArchivedClassroomPreservesRecordsAndRejectsNewStudents()
    {
        var classroom = await CreateClassroomAsync();
        var student = await SendAsync<StudentCredentialsResponse>(teacherClient, HttpMethod.Post, $"/api/classrooms/{classroom.Id}/students", new { enrollmentNumber = "existing", name = "Existing" });
        using var studentClient = CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await SendRawAsync(studentClient, HttpMethod.Post, "/api/auth/students/activate", new { classroomCode = classroom.Code, enrollmentNumber = student.EnrollmentNumber, temporaryCode = student.TemporaryAccessCode, password = "student-password", confirmPassword = "student-password" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await SendRawAsync(teacherClient, HttpMethod.Post, $"/api/classrooms/{classroom.Id}/archive", new { })).StatusCode);
        Assert.Equal("Archived", (await teacherClient.GetFromJsonAsync<ClassroomDetailsResponse>($"/api/classrooms/{classroom.Id}"))!.Status);
        Assert.Equal(HttpStatusCode.Unauthorized, (await studentClient.GetAsync("/api/auth/me")).StatusCode);
        Assert.Single(await teacherClient.GetFromJsonAsync<List<StudentSummaryResponse>>($"/api/classrooms/{classroom.Id}/students") ?? []);
        Assert.Equal(HttpStatusCode.Conflict, (await SendRawAsync(teacherClient, HttpMethod.Post, $"/api/classrooms/{classroom.Id}/students", new { enrollmentNumber = "late", name = "Late" })).StatusCode);
    }
    private HttpClient CreateClient() => factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("http://localhost"), HandleCookies = true, AllowAutoRedirect = false });
    private async Task<ClassroomDetailsResponse> CreateClassroomAsync(string code = "Redes2026") => await SendAsync<ClassroomDetailsResponse>(teacherClient, HttpMethod.Post, "/api/classrooms", new { name = "Redes", code });
    private async Task<Student> FindStudentAsync(Guid id)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Students.SingleAsync(x => x.Id == id);
    }
    private async Task<T> SendAsync<T>(HttpClient client, HttpMethod method, string path, object body)
    {
        var response = await SendRawAsync(client, method, path, body);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
    private static async Task<HttpResponseMessage> SendRawAsync(HttpClient client, HttpMethod method, string path, object? body)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null) request.Content = JsonContent.Create(body);
        if (method != HttpMethod.Get && method != HttpMethod.Delete)
        {
            var token = await client.GetFromJsonAsync<CsrfTokenResponse>("/api/auth/csrf");
            request.Headers.Add("X-CSRF-TOKEN", token!.Token);
        }
        else if (method == HttpMethod.Delete)
        {
            var token = await client.GetFromJsonAsync<CsrfTokenResponse>("/api/auth/csrf");
            request.Headers.Add("X-CSRF-TOKEN", token!.Token);
        }
        return await client.SendAsync(request);
    }
}

