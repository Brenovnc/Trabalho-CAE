using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StudyPlatform.Api.Data;
using StudyPlatform.Api.DTOs.Auth;
using StudyPlatform.Api.Models;
using StudyPlatform.Api.Services.Auth;
using Xunit;

namespace StudyPlatform.Tests;

public sealed class AuthEndpointsTests : IAsyncLifetime
{
    private readonly AuthApiFactory _factory = new();
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await _factory.ResetDatabaseAsync();
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://localhost"),
            HandleCookies = true,
            AllowAutoRedirect = false,
        });
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task TeacherCanRegisterLoginReadIdentityAndLogoutWithSecureCookie()
    {
        var registration = await PostAsync("/api/auth/teachers/register", new
        {
            name = "Ada Lovelace",
            email = "Ada@example.test",
            password = "a-secure-password",
            confirmPassword = "a-secure-password",
        });

        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        var responseBody = await registration.Content.ReadAsStringAsync();
        Assert.DoesNotContain("password", responseBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hash", responseBody, StringComparison.OrdinalIgnoreCase);

        var setCookie = Assert.Single(registration.Headers.GetValues("Set-Cookie"));
        Assert.Contains("HttpOnly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SameSite=Strict", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Max-Age=28800", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Secure", setCookie, StringComparison.OrdinalIgnoreCase);

        var teacher = await FindTeacherAsync("ada@example.test");
        Assert.NotEqual("a-secure-password", teacher.PasswordHash);
        Assert.Equal(PasswordVerificationResult.Success,
            new PasswordHasher<Teacher>().VerifyHashedPassword(
                teacher, teacher.PasswordHash, "a-secure-password"));

        var me = await _client.GetFromJsonAsync<AuthenticatedUserResponse>("/api/auth/me");
        Assert.Equal(AuthRoles.Teacher, me!.Role);
        Assert.Equal("Ada Lovelace", me.Name);
        Assert.Equal("Ada@example.test", me.Email);

        var login = await PostAsync("/api/auth/teachers/login", new
        {
            email = "ADA@EXAMPLE.TEST",
            password = "a-secure-password",
        });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var logout = await PostAsync("/api/auth/logout", new { });
        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await _client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task TeacherEmailIsUniqueIgnoringCaseAndPasswordsMustMatch()
    {
        await PostAsync("/api/auth/teachers/register", new
        {
            name = "Teacher",
            email = "teacher@example.test",
            password = "a-secure-password",
            confirmPassword = "a-secure-password",
        });

        var duplicate = await PostAsync("/api/auth/teachers/register", new
        {
            name = "Other",
            email = "TEACHER@EXAMPLE.TEST",
            password = "another-secure-password",
            confirmPassword = "another-secure-password",
        });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        var mismatch = await PostAsync("/api/auth/teachers/register", new
        {
            name = "Mismatch",
            email = "mismatch@example.test",
            password = "a-secure-password",
            confirmPassword = "a-different-password",
        });
        Assert.Equal(HttpStatusCode.BadRequest, mismatch.StatusCode);
        Assert.Contains("ConfirmPassword", await mismatch.Content.ReadAsStringAsync());

        var invalidLogin = await PostAsync("/api/auth/teachers/login", new
        {
            email = "teacher@example.test",
            password = "wrong-password",
        });
        Assert.Equal(HttpStatusCode.Unauthorized, invalidLogin.StatusCode);
    }

    [Fact]
    public async Task StudentActivationConsumesOneTimeCodeAndAllowsCaseInsensitiveClassroomLogin()
    {
        var studentId = await SeedStudentAsync("Redes-A", "12345", "TMP82K", DateTime.UtcNow.AddDays(7));
        var activation = await PostAsync("/api/auth/students/activate", new
        {
            classroomCode = "rEdEs-a",
            enrollmentNumber = "12345",
            temporaryCode = "tmp82k",
            password = "student-password",
            confirmPassword = "student-password",
        });

        Assert.Equal(HttpStatusCode.OK, activation.StatusCode);
        var activatedResponse = await activation.Content.ReadAsStringAsync();
        Assert.DoesNotContain("hash", activatedResponse, StringComparison.OrdinalIgnoreCase);

        var student = await FindStudentAsync(studentId);
        Assert.True(student.IsActivated);
        Assert.Null(student.TemporaryAccessCodeHash);
        Assert.Null(student.TemporaryAccessCodeExpiresAtUtc);
        Assert.Equal(PasswordVerificationResult.Success,
            new PasswordHasher<Student>().VerifyHashedPassword(
                student, student.PasswordHash!, "student-password"));

        var reuse = await PostAsync("/api/auth/students/activate", new
        {
            classroomCode = "REDES-A",
            enrollmentNumber = "12345",
            temporaryCode = "TMP82K",
            password = "student-password",
            confirmPassword = "student-password",
        });
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);

        await PostAsync("/api/auth/logout", new { });
        var login = await PostAsync("/api/auth/students/login", new
        {
            classroomCode = "REDES-A",
            enrollmentNumber = "12345",
            password = "student-password",
        });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var me = await _client.GetFromJsonAsync<AuthenticatedUserResponse>("/api/auth/me");
        Assert.Equal(studentId, me!.Id);
        Assert.Equal(AuthRoles.Student, me.Role);
        Assert.Equal("12345", me.EnrollmentNumber);
        Assert.Null(me.Email);
    }

    [Fact]
    public async Task InvalidTemporaryCodeLocksAfterFiveAttempts()
    {
        var studentId = await SeedStudentAsync("Turma-Limite", "5", "VALID7", DateTime.UtcNow.AddDays(7));

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var response = await PostAsync("/api/auth/students/activate", new
            {
                classroomCode = "TURMA-LIMITE",
                enrollmentNumber = "5",
                temporaryCode = "WRONG7",
                password = "student-password",
                confirmPassword = "student-password",
            });
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        var student = await FindStudentAsync(studentId);
        Assert.Equal(5, student.TemporaryAccessCodeFailedAttempts);

        var correctCodeAfterLock = await PostAsync("/api/auth/students/activate", new
        {
            classroomCode = "Turma-Limite",
            enrollmentNumber = "5",
            temporaryCode = "VALID7",
            password = "student-password",
            confirmPassword = "student-password",
        });
        Assert.Equal(HttpStatusCode.Unauthorized, correctCodeAfterLock.StatusCode);
    }

    [Fact]
    public async Task ExpiredTemporaryCodeIsRejectedWithoutAcceptingActivation()
    {
        var studentId = await SeedStudentAsync("Turma-Vencida", "6", "OLD82K", DateTime.UtcNow.AddDays(-1));
        var response = await PostAsync("/api/auth/students/activate", new
        {
            classroomCode = "Turma-Vencida",
            enrollmentNumber = "6",
            temporaryCode = "OLD82K",
            password = "student-password",
            confirmPassword = "student-password",
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var student = await FindStudentAsync(studentId);
        Assert.False(student.IsActivated);
        Assert.Equal(0, student.TemporaryAccessCodeFailedAttempts);
        Assert.Null(student.PasswordHash);
    }

    [Fact]
    public async Task StudentEnrollmentIsScopedToClassroom()
    {
        var firstStudentId = await SeedStudentAsync("Turma-Um", "same", "FIRST7", DateTime.UtcNow.AddDays(7));
        var secondStudentId = await SeedStudentAsync("Turma-Dois", "same", "SECOND7", DateTime.UtcNow.AddDays(7));

        var activation = await PostAsync("/api/auth/students/activate", new
        {
            classroomCode = "TURMA-DOIS",
            enrollmentNumber = "same",
            temporaryCode = "SECOND7",
            password = "student-password",
            confirmPassword = "student-password",
        });

        Assert.Equal(HttpStatusCode.OK, activation.StatusCode);
        var user = await activation.Content.ReadFromJsonAsync<AuthenticatedUserResponse>();
        Assert.Equal(secondStudentId, user!.Id);
        Assert.NotEqual(firstStudentId, user.Id);
    }

    [Fact]
    public async Task StateChangingRequestsRequireCsrfAndCorsIsRestrictedToFrontendOrigin()
    {
        var blocked = await _client.PostAsJsonAsync("/api/auth/teachers/register", new
        {
            name = "Blocked",
            email = "blocked@example.test",
            password = "a-secure-password",
            confirmPassword = "a-secure-password",
        });
        Assert.Equal(HttpStatusCode.BadRequest, blocked.StatusCode);
        Assert.Contains("csrf_validation_failed", await blocked.Content.ReadAsStringAsync());
        Assert.Null(await FindTeacherOrNullAsync("blocked@example.test"));

        using var preflight = new HttpRequestMessage(HttpMethod.Options, "/api/auth/teachers/register");
        preflight.Headers.Add("Origin", "http://localhost:5173");
        preflight.Headers.Add("Access-Control-Request-Method", "POST");
        preflight.Headers.Add("Access-Control-Request-Headers", "content-type,x-csrf-token");
        var allowed = await _client.SendAsync(preflight);
        Assert.Equal("http://localhost:5173", allowed.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Contains("true", allowed.Headers.GetValues("Access-Control-Allow-Credentials"));

        using var disallowedRequest = new HttpRequestMessage(HttpMethod.Options, "/api/auth/teachers/register");
        disallowedRequest.Headers.Add("Origin", "http://evil.example");
        disallowedRequest.Headers.Add("Access-Control-Request-Method", "POST");
        var disallowed = await _client.SendAsync(disallowedRequest);
        Assert.False(disallowed.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task AuthMeRequiresAuthentication()
    {
        var response = await _client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("unauthenticated", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task StudentLoginRejectsUnactivatedStudentAndWrongPassword()
    {
        await SeedStudentAsync("Turma-Login", "7", "LOGIN7", DateTime.UtcNow.AddDays(7));
        var beforeActivation = await PostAsync("/api/auth/students/login", new
        {
            classroomCode = "Turma-Login",
            enrollmentNumber = "7",
            password = "student-password",
        });
        Assert.Equal(HttpStatusCode.Unauthorized, beforeActivation.StatusCode);

        var activation = await PostAsync("/api/auth/students/activate", new
        {
            classroomCode = "Turma-Login",
            enrollmentNumber = "7",
            temporaryCode = "LOGIN7",
            password = "student-password",
            confirmPassword = "student-password",
        });
        Assert.Equal(HttpStatusCode.OK, activation.StatusCode);
        await PostAsync("/api/auth/logout", new { });

        var wrongPassword = await PostAsync("/api/auth/students/login", new
        {
            classroomCode = "Turma-Login",
            enrollmentNumber = "7",
            password = "incorrect-password",
        });
        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
    }

    [Fact]
    public async Task RolePoliciesDistinguishTeacherAndStudent()
    {
        var authorization = _factory.Services.GetRequiredService<Microsoft.AspNetCore.Authorization.IAuthorizationService>();
        var teacher = AuthPrincipalFactory.ForTeacher(new Teacher
        {
            Name = "Policy Teacher",
            Email = "policy@example.test",
        });
        var student = AuthPrincipalFactory.ForStudent(new Student
        {
            ClassroomId = Guid.NewGuid(),
            EnrollmentNumber = "policy-enrollment",
        });

        Assert.True((await authorization.AuthorizeAsync(teacher, null, AuthPolicies.Teacher)).Succeeded);
        Assert.False((await authorization.AuthorizeAsync(teacher, null, AuthPolicies.Student)).Succeeded);
        Assert.True((await authorization.AuthorizeAsync(student, null, AuthPolicies.Student)).Succeeded);
        Assert.False((await authorization.AuthorizeAsync(student, null, AuthPolicies.Teacher)).Succeeded);
    }
    private async Task<HttpResponseMessage> PostAsync(string path, object body)
    {
        var tokenResponse = await _client.GetFromJsonAsync<CsrfTokenResponse>("/api/auth/csrf");
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Add("X-CSRF-TOKEN", tokenResponse!.Token);
        return await _client.SendAsync(request);
    }

    private async Task<Guid> SeedStudentAsync(
        string classroomCode,
        string enrollmentNumber,
        string temporaryCode,
        DateTime expiresAtUtc)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var teacher = new Teacher
        {
            Name = "Seed Teacher",
            Email = $"seed-{Guid.NewGuid():N}@example.test",
            PasswordHash = "seed-only",
        };
        dbContext.Teachers.Add(teacher);
        var classroom = new Classroom
        {
            Teacher = teacher,
            Name = classroomCode,
            Code = classroomCode,
        };
        var student = new Student
        {
            Classroom = classroom,
            EnrollmentNumber = enrollmentNumber,
            Name = "Student Test",
            TemporaryAccessCodeExpiresAtUtc = expiresAtUtc,
            TemporaryAccessCodeFailedAttempts = 0,
        };
        student.TemporaryAccessCodeHash = new PasswordHasher<Student>()
            .HashPassword(student, temporaryCode.Trim().ToUpperInvariant());
        dbContext.Students.Add(student);
        await dbContext.SaveChangesAsync();
        return student.Id;
    }

    private async Task<Teacher> FindTeacherAsync(string email)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .Teachers.SingleAsync(teacher => teacher.EmailNormalized == email.ToLowerInvariant());
    }

    private async Task<Teacher?> FindTeacherOrNullAsync(string email)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .Teachers.SingleOrDefaultAsync(teacher => teacher.EmailNormalized == email.ToLowerInvariant());
    }

    private async Task<Student> FindStudentAsync(Guid id)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .Students.SingleAsync(student => student.Id == id);
    }
}
