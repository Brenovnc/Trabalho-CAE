using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using StudyPlatform.Api.Data;
using StudyPlatform.Api.DTOs.Auth;
using StudyPlatform.Api.DTOs.Classrooms;
using StudyPlatform.Api.DTOs.Students;
using StudyPlatform.Api.Models;
using StudyPlatform.Api.Services.Students;
using Xunit;

namespace StudyPlatform.Tests;

public sealed class StudentCsvImportTests : IAsyncLifetime
{
    private readonly StudentCsvApiFactory factory = new();
    private HttpClient teacher = null!;
    private ClassroomDetailsResponse classroom = null!;

    public async Task InitializeAsync()
    {
        await factory.ResetDatabaseAsync();
        teacher = Client();
        await PostJson(teacher, "/api/auth/teachers/register", new { name = "CSV Teacher", email = "csv@example.test", password = "secure-password", confirmPassword = "secure-password" });
        classroom = await PostJson<ClassroomDetailsResponse>(teacher, "/api/classrooms", new { name = "CSV Class", code = "CSV-CLASS" });
    }

    public async Task DisposeAsync() { teacher.Dispose(); await factory.DisposeAsync(); }

    [Fact]
    public void ParserSupportsBomQuotedCommaAndLineEndingsAndRejectsMalformedCsv()
    {
        var parser = new CsvStudentParser();
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes("matricula,nome\r\n00123,\"Silva, João\"\r\n12346,\r\n")).ToArray();
        var rows = parser.Parse(bytes);
        Assert.Equal(2, rows.Count);
        Assert.Equal("00123", rows[0].EnrollmentNumber);
        Assert.Equal("Silva, João", rows[0].Name);
        Assert.Null(rows[1].Name);
        var matriculaOnly = parser.Parse(Encoding.UTF8.GetBytes("matricula\n00124\n"));
        Assert.Single(matriculaOnly);
        Assert.Equal("00124", matriculaOnly[0].EnrollmentNumber);
        Assert.Equal(2, rows[0].LineNumber);
        Assert.Equal(3, rows[1].LineNumber);
        Assert.Throws<StudyPlatform.Api.Exceptions.ApiException>(() => parser.Parse(Encoding.UTF8.GetBytes("matricula,nome\n123,\"sem fim\n")));
        Assert.Throws<StudyPlatform.Api.Exceptions.ApiException>(() => parser.Parse(Encoding.UTF8.GetBytes("matricula,idade\n123,10\n")));
        Assert.Throws<StudyPlatform.Api.Exceptions.ApiException>(() => parser.Parse([]));
    }

    [Fact]
    public async Task PreviewDoesNotPersistAndConfirmationImportsOnlyValidRowsWithOneTimeCredentials()
    {
        const string csv = "matricula,nome\r\n00123,\"Silva, João\"\r\n,Sem matrícula\r\n00123,Duplicada\r\n";
        var previewResponse = await Upload("preview", csv);
        Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
        var preview = (await previewResponse.Content.ReadFromJsonAsync<StudentCsvPreviewResponse>())!;
        Assert.Equal(3, preview.TotalRows);
        Assert.Equal(1, preview.ValidRows);
        Assert.Equal(2, preview.InvalidRows);
        Assert.Equal(2, preview.Rows[0].LineNumber);
        Assert.Equal("00123", preview.Rows[0].EnrollmentNumber);
        Assert.True(preview.Rows[0].IsValid);
        Assert.Contains("obrigatória", preview.Rows[1].Errors[0]);
        Assert.Contains("duplicada", preview.Rows[2].Errors[0]);
        Assert.Empty(await GetStudents());

        var confirmResponse = await Upload("confirm", csv);
        Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);
        var result = (await confirmResponse.Content.ReadFromJsonAsync<StudentCsvImportResultResponse>())!;
        Assert.Equal(1, result.CreatedCount);
        Assert.Equal(2, result.SkippedRows.Count);
        Assert.Contains("matricula,nome,codigo_temporario", result.CredentialsCsv);
        Assert.Contains("00123,\"Silva, João\",", result.CredentialsCsv);
        Assert.DoesNotContain("Duplicada", result.CredentialsCsv);

        var student = (await GetStudents()).Single();
        Assert.Equal("00123", student.EnrollmentNumber);
        Assert.Equal("Silva, João", student.Name);
        Assert.True(student.IsActive);
        Assert.False(student.IsActivated);
        Assert.Null(student.PasswordHash);
        Assert.Equal(0, student.TemporaryAccessCodeFailedAttempts);
        Assert.InRange(student.TemporaryAccessCodeExpiresAtUtc!.Value - DateTime.UtcNow, TimeSpan.FromDays(6.9), TimeSpan.FromDays(7.1));
        var credentialLine = result.CredentialsCsv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries)[1];
        var issuedCode = credentialLine[(credentialLine.LastIndexOf(',') + 1)..].Trim('"');
        Assert.False(string.IsNullOrEmpty(student.TemporaryAccessCodeHash));
        Assert.DoesNotContain(issuedCode, student.TemporaryAccessCodeHash!);
        Assert.DoesNotContain("PasswordHash", result.CredentialsCsv);
        using var studentClient = Client();
        var activated = await SendJson(studentClient, "/api/auth/students/activate", new { classroomCode = classroom.Code, enrollmentNumber = student.EnrollmentNumber, temporaryCode = issuedCode, password = "csv-student-password", confirmPassword = "csv-student-password" });
        Assert.Equal(HttpStatusCode.OK, activated.StatusCode);
        var afterActivation = (await GetStudents()).Single();
        Assert.Null(afterActivation.TemporaryAccessCodeHash);
        Assert.True(afterActivation.IsActivated);
    }

    [Fact]
    public async Task PreviewDetectsExistingEnrollmentAndRejectsOversizedFileAndStudentRole()
    {
        var code = await PostJson<StudentCredentialsResponse>(teacher, $"/api/classrooms/{classroom.Id}/students", new { enrollmentNumber = "123", name = "Already" });
        var preview = await Upload("preview", "matricula\n123\n124\n");
        var data = (await preview.Content.ReadFromJsonAsync<StudentCsvPreviewResponse>())!;
        Assert.Equal(1, data.ValidRows);
        Assert.Contains("já existe", data.Rows[0].Errors[0]);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await Upload("preview", "matricula\n" + new string('x', CsvStudentParser.MaxFileBytes))).StatusCode);
        using var student = Client();
        var activated = await SendJson(student, "/api/auth/students/activate", new { classroomCode = classroom.Code, enrollmentNumber = "123", temporaryCode = code.TemporaryAccessCode, password = "student-password", confirmPassword = "student-password" });
        Assert.Equal(HttpStatusCode.OK, activated.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Upload("preview", "matricula\n999\n", student)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Upload("preview", "matricula\n999\n", csrf: false)).StatusCode);
        using var otherTeacher = Client();
        await PostJson(otherTeacher, "/api/auth/teachers/register", new { name = "Other", email = "other-csv@example.test", password = "secure-password", confirmPassword = "secure-password" });
        Assert.Equal(HttpStatusCode.NotFound, (await Upload("preview", "matricula\n999\n", otherTeacher)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await SendJson(teacher, "/api/classrooms/" + classroom.Id + "/archive", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Upload("preview", "matricula\n999\n")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Upload("confirm", "matricula\n999\n")).StatusCode);
    }

    [Fact]
    public async Task ConfirmationRevalidatesRowsCreatedAfterPreviewAndContinuesWithOtherValidRows()
    {
        const string csv = "matricula,nome\ncreated,Will be created\nraced,Will conflict\n";
        var previewResponse = await Upload("preview", csv);
        Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
        var preview = (await previewResponse.Content.ReadFromJsonAsync<StudentCsvPreviewResponse>())!;
        Assert.Equal(2, preview.ValidRows);

        await PostJson<StudentCredentialsResponse>(teacher, "/api/classrooms/" + classroom.Id + "/students", new { enrollmentNumber = "raced", name = "Added between steps" });
        var resultResponse = await Upload("confirm", csv);
        var result = (await resultResponse.Content.ReadFromJsonAsync<StudentCsvImportResultResponse>())!;
        Assert.Equal(HttpStatusCode.OK, resultResponse.StatusCode);
        Assert.Equal(1, result.CreatedCount);
        Assert.Single(result.SkippedRows);
        Assert.Equal(3, result.SkippedRows[0].LineNumber);
        Assert.Contains("já existe", result.SkippedRows[0].Errors[0]);
        Assert.Contains("created", result.CredentialsCsv);
        Assert.DoesNotContain("raced,", result.CredentialsCsv);
    }
    private HttpClient Client() => factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("http://localhost"), HandleCookies = true, AllowAutoRedirect = false });
    private async Task<List<Student>> GetStudents() {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Students.Where(x => x.ClassroomId == classroom.Id).AsNoTracking().ToListAsync();
    }

    private async Task<HttpResponseMessage> Upload(string operation, string csv, HttpClient? client = null, bool csrf = true)
    {
        client ??= teacher;
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/classrooms/{classroom.Id}/students/import/{operation}");
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        form.Add(file, "file", "students.csv");
        request.Content = form;
        if (csrf) request.Headers.Add("X-CSRF-TOKEN", (await client.GetFromJsonAsync<CsrfTokenResponse>("/api/auth/csrf"))!.Token);
        return await client.SendAsync(request);
    }

    private static async Task<T> PostJson<T>(HttpClient client, string path, object body)
    {
        var response = await SendJson(client, path, body);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private static async Task PostJson(HttpClient client, string path, object body) => _ = await PostJson<object>(client, path, body);

    private static async Task<HttpResponseMessage> SendJson(HttpClient client, string path, object body)
    {
        var token = await client.GetFromJsonAsync<CsrfTokenResponse>("/api/auth/csrf");
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", token!.Token);
        return await client.SendAsync(request);
    }
}

public sealed class StudentCsvApiFactory : WebApplicationFactory<Program>
{
    private const string DatabaseName = "trabalho_cae_student_csv_test";
    private readonly string connectionString = BuildConnectionString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = connectionString,
            ["Frontend:Origin"] = "http://localhost:5173",
        }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<ApplicationDbContext>();
            services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(connectionString));
        });
    }

    public async Task ResetDatabaseAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (db.Database.GetDbConnection().Database != DatabaseName) throw new InvalidOperationException("Refusing to clean a non-test database.");
        await db.Database.MigrateAsync();
        await db.Database.ExecuteSqlRawAsync("TRUNCATE TABLE \"Teachers\", \"Classrooms\", \"Students\" CASCADE");
    }

    private static string BuildConnectionString()
    {
        var raw = Environment.GetEnvironmentVariable("STUDYPLATFORM_CSV_TEST_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=trabalho_cae_student_csv_test;Username=trabalho_cae;Password=local_dev_password";
        var builder = new NpgsqlConnectionStringBuilder(raw);
        if (builder.Database != DatabaseName) throw new InvalidOperationException($"Tests require the dedicated {DatabaseName} database.");
        return builder.ConnectionString;
    }
}

