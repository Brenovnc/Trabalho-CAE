using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StudyPlatform.Api.Data;
using StudyPlatform.Api.Domain.Enums;
using StudyPlatform.Api.DTOs.Auth;
using StudyPlatform.Api.DTOs.Concepts;
using StudyPlatform.Api.DTOs.Modules;
using StudyPlatform.Api.Models;
using StudyPlatform.Api.Models.Activities;
using Xunit;

namespace StudyPlatform.Tests;

public sealed class ModulesEndpointsTests : IAsyncLifetime
{
    private readonly ModulesApiFactory factory = new();
    private HttpClient client = null!;

    public async Task InitializeAsync()
    {
        await factory.ResetDatabaseAsync();
        client = CreateClient();
    }

    public async Task DisposeAsync()
    {
        client.Dispose();
        await factory.DisposeAsync();
    }

    [Fact]
    public async Task TeacherCanCreateListEditAndArchiveOnlyOwnedModules()
    {
        var owner = await CreateTeacherAsync(client, "owner");
        var otherClient = CreateClient();
        await CreateTeacherAsync(otherClient, "other");
        var module = await CreateModuleAsync(client, "Networks");

        Assert.Equal(HttpStatusCode.NotFound, (await otherClient.GetAsync($"/api/modules/{module.Id}")).StatusCode);
        var foreignEdit = await SendAsync(otherClient, HttpMethod.Put, $"/api/modules/{module.Id}", new { title = "Stolen", subject = "Networking", description = "", version = 1 });
        Assert.Equal(HttpStatusCode.NotFound, foreignEdit.StatusCode);
        var list = await client.GetFromJsonAsync<List<ModuleSummaryResponse>>("/api/modules");
        Assert.Single(list!);
        Assert.Empty((await otherClient.GetFromJsonAsync<List<ModuleSummaryResponse>>("/api/modules"))!);

        var updated = await SendAsync<ModuleDetailsResponse>(client, HttpMethod.Put, $"/api/modules/{module.Id}",
            new { title = "Networks updated", subject = "Computer Networks", description = "MVP", version = 2 });
        Assert.Equal("Networks updated", updated!.Title);
        Assert.Equal(2, updated.Version);

        var archive = await SendAsync(client, HttpMethod.Post, $"/api/modules/{module.Id}/archive", new { });
        Assert.Equal(HttpStatusCode.NoContent, archive.StatusCode);
        Assert.Equal(ModuleStatus.Archived,
            (await client.GetFromJsonAsync<ModuleDetailsResponse>($"/api/modules/{module.Id}"))!.Status);
        Assert.NotEqual(Guid.Empty, owner.Id);
        otherClient.Dispose();
    }

    [Fact]
    public async Task StudentAndAnonymousUsersCannotManageModules()
    {
        var teacher = await CreateTeacherAsync(client, "roles");
        var module = await CreateModuleAsync(client, "Private module");
        var anonymous = CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/modules")).StatusCode);

        var studentClient = CreateClient();
        await SeedAndLoginStudentAsync(studentClient, teacher.Id);
        Assert.Equal(HttpStatusCode.Forbidden, (await studentClient.GetAsync("/api/modules")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await studentClient.GetAsync($"/api/modules/{module.Id}")).StatusCode);
        anonymous.Dispose(); studentClient.Dispose();
    }

    [Fact]
    public async Task ConceptCanBeCreatedUpdatedAndDuplicatedWithoutCrossModuleReferences()
    {
        await CreateTeacherAsync(client, "concepts");
        var module = await CreateModuleAsync(client, "Concept module");
        var otherModule = await CreateModuleAsync(client, "Other module");
        var prerequisite = await CreateConceptAsync(module.Id, "IP");
        var concept = await SaveConceptAsync(module.Id, "DNS", [prerequisite.Id]);

        var duplicate = await SendAsync<ConceptDetailsResponse>(client, HttpMethod.Post,
            $"/api/modules/{module.Id}/concepts/{concept.Id}/duplicate", null, HttpStatusCode.Created);
        Assert.NotEqual(concept.Id, duplicate!.Id);
        Assert.Equal(new[] { prerequisite.Id }, duplicate.PrerequisiteIds);
        var removePrerequisite = await SendAsync(client, HttpMethod.Put,
            $"/api/modules/{module.Id}/concepts/{concept.Id}", ConceptPayload("DNS", []));
        Assert.Equal(HttpStatusCode.OK, removePrerequisite.StatusCode);

        var invalid = await SendAsync(client, HttpMethod.Put,
            $"/api/modules/{module.Id}/concepts/{concept.Id}", ConceptPayload("DNS", [Guid.NewGuid()]));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var outsidePrerequisite = await CreateConceptAsync(otherModule.Id, "External concept");
        var crossModulePrerequisite = await SendAsync(client, HttpMethod.Put, $"/api/modules/{module.Id}/concepts/{concept.Id}", ConceptPayload("DNS", [outsidePrerequisite.Id]));
        Assert.Equal(HttpStatusCode.BadRequest, crossModulePrerequisite.StatusCode);
        var crossModuleConcept = await SaveConceptAsync(module.Id, "Router", [prerequisite.Id]);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/modules/{otherModule.Id}/concepts/{crossModuleConcept.Id}")).StatusCode);
    }

    [Fact]
    public async Task PrerequisiteServiceRejectsDirectAndIndirectCyclesAndAllowsAcyclicGraph()
    {
        await CreateTeacherAsync(client, "cycles");
        var module = await CreateModuleAsync(client, "Graph");
        var a = await CreateConceptAsync(module.Id, "A");
        var b = await CreateConceptAsync(module.Id, "B");
        var c = await CreateConceptAsync(module.Id, "C");

        Assert.Equal(HttpStatusCode.BadRequest,
            (await SendAsync(client, HttpMethod.Put, $"/api/modules/{module.Id}/concepts/{a.Id}", ConceptPayload("A", [a.Id]))).StatusCode);
        var updateA = await SendAsync(client, HttpMethod.Put, $"/api/modules/{module.Id}/concepts/{a.Id}", ConceptPayload("A", [b.Id]));
        Assert.True(updateA.StatusCode == HttpStatusCode.OK, await updateA.Content.ReadAsStringAsync());
        var updateB = await SendAsync(client, HttpMethod.Put, $"/api/modules/{module.Id}/concepts/{b.Id}", ConceptPayload("B", [c.Id]));
        Assert.True(updateB.StatusCode == HttpStatusCode.OK, await updateB.Content.ReadAsStringAsync());
        var indirectCycle = await SendAsync(client, HttpMethod.Put,
            $"/api/modules/{module.Id}/concepts/{c.Id}", ConceptPayload("C", [a.Id]));
        Assert.Equal(HttpStatusCode.BadRequest, indirectCycle.StatusCode);
        Assert.Contains("ciclo", await indirectCycle.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InvalidPublicationExplainsMissingContentAndOrderingIsOptionalForValidModule()
    {
        await CreateTeacherAsync(client, "publish");
        var invalidModule = await CreateModuleAsync(client, "Invalid");
        await SendAsync<ConceptDetailsResponse>(client, HttpMethod.Post, $"/api/modules/{invalidModule.Id}/concepts", new { name = "DNS", definition = "A definition", keywords = Array.Empty<string>(), clues = Array.Empty<string>(), prerequisiteIds = Array.Empty<Guid>(), recognitionActivities = Array.Empty<object>(), fillBlankActivities = Array.Empty<object>(), orderingActivities = Array.Empty<object>() }, HttpStatusCode.Created);
        var validation = await client.GetFromJsonAsync<PublicationValidationResponse>(
            $"/api/modules/{invalidModule.Id}/publication-validation");
        Assert.False(validation!.IsValid);
        Assert.Contains(validation.Errors, error => error.Code == "missing_keywords");
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await SendAsync(client, HttpMethod.Post, $"/api/modules/{invalidModule.Id}/publish", null)).StatusCode);

        var validModule = await CreateModuleAsync(client, "Valid");
        await SaveConceptAsync(validModule.Id, "DNS", []);
        var published = await SendAsync<ModuleDetailsResponse>(client, HttpMethod.Post,
            $"/api/modules/{validModule.Id}/publish", null);
        Assert.Equal(ModuleStatus.Published, published!.Status);
        Assert.Empty(published.Concepts[0].OrderingActivities);
    }

    [Fact]
    public async Task ModuleDuplicationCreatesDraftAndRemapsInternalPrerequisitesAndActivities()
    {
        await CreateTeacherAsync(client, "duplicate");
        var module = await CreateModuleAsync(client, "Source");
        var foundation = await SaveConceptAsync(module.Id, "IP", []);
        var dns = await SaveConceptAsync(module.Id, "DNS", [foundation.Id]);

        var copy = await SendAsync<ModuleDetailsResponse>(client, HttpMethod.Post,
            $"/api/modules/{module.Id}/duplicate", null, HttpStatusCode.Created);
        Assert.Equal(ModuleStatus.Draft, copy!.Status);
        Assert.NotEqual(module.Id, copy.Id);
        var copiedFoundation = Assert.Single(copy.Concepts, item => item.Name == "IP");
        var copiedDns = Assert.Single(copy.Concepts, item => item.Name == "DNS");
        Assert.NotEqual(foundation.Id, copiedFoundation.Id);
        Assert.Equal([copiedFoundation.Id], copiedDns.PrerequisiteIds);
        Assert.Equal(3, copiedDns.RecognitionActivities.Count);
        Assert.Equal(3, copiedDns.FillBlankActivities.Count);
        Assert.NotEqual(dns.Id, copiedDns.Id);
    }

    [Fact]
    public async Task RemovedActivityWithAttemptIsDeactivatedAndAttemptIsPreserved()
    {
        var teacher = await CreateTeacherAsync(client, "history");
        var module = await CreateModuleAsync(client, "History");
        await SaveConceptAsync(module.Id, "IP", []);
        var concept = await SaveConceptAsync(module.Id, "DNS", []);
        var activityId = concept.RecognitionActivities[0].Id;
        var sessionId = await SeedAttemptAsync(teacher.Id, module.Id, concept.Id, activityId);
        var newRequest = ConceptPayload("DNS", []);
        var response = await SendAsync<ConceptDetailsResponse>(client, HttpMethod.Put,
            $"/api/modules/{module.Id}/concepts/{concept.Id}", newRequest);
        Assert.False(response!.RecognitionActivities.Single(item => item.Id == activityId).IsActive);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(1, await db.ActivityAttempts.CountAsync(item => item.StudySessionId == sessionId));
        Assert.True(await db.RecognitionActivities.AnyAsync(item => item.Id == activityId));
    }


    [Fact]
    public async Task DeactivatingConceptPreservesConceptActivityAndAttemptHistory()
    {
        var teacher = await CreateTeacherAsync(client, "deactivate");
        var module = await CreateModuleAsync(client, "Deactivation");
        await SaveConceptAsync(module.Id, "IP", []);
        var concept = await SaveConceptAsync(module.Id, "DNS", []);
        var activityId = concept.RecognitionActivities[0].Id;
        var sessionId = await SeedAttemptAsync(teacher.Id, module.Id, concept.Id, activityId);

        var response = await SendAsync(client, HttpMethod.Post,
            $"/api/modules/{module.Id}/concepts/{concept.Id}/deactivate", null);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var inactive = await client.GetFromJsonAsync<ConceptDetailsResponse>(
            $"/api/modules/{module.Id}/concepts/{concept.Id}");
        Assert.False(inactive!.IsActive);
        Assert.False(inactive.RecognitionActivities.Single(item => item.Id == activityId).IsActive);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.True(await db.Concepts.AnyAsync(item => item.Id == concept.Id));
        Assert.Equal(1, await db.ActivityAttempts.CountAsync(item => item.StudySessionId == sessionId));
        var publication = await client.GetFromJsonAsync<PublicationValidationResponse>($"/api/modules/{module.Id}/publication-validation");
        Assert.True(publication!.IsValid);
    }
    private HttpClient CreateClient() => factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("http://localhost"), HandleCookies = true, AllowAutoRedirect = false,
    });

    private static async Task<Teacher> CreateTeacherAsync(HttpClient http, string label)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var response = await SendAsync<AuthenticatedUserResponse>(http, HttpMethod.Post,
            "/api/auth/teachers/register", new
            {
                name = $"Teacher {label}", email = $"{label}-{suffix}@example.test",
                password = "password-for-tests", confirmPassword = "password-for-tests",
            }, HttpStatusCode.Created);
        return new Teacher { Id = response!.Id, Name = response.Name!, Email = response.Email!, PasswordHash = "unused" };
    }

    private static async Task<ModuleDetailsResponse> CreateModuleAsync(HttpClient http, string title) =>
        (await SendAsync<ModuleDetailsResponse>(http, HttpMethod.Post, "/api/modules",
            new { title, description = "", subject = "Networking", version = 1 }, HttpStatusCode.Created))!;

    private async Task<ConceptDetailsResponse> CreateConceptAsync(Guid moduleId, string name) =>
        (await SendAsync<ConceptDetailsResponse>(client, HttpMethod.Post,
            $"/api/modules/{moduleId}/concepts", ConceptPayload(name, []), HttpStatusCode.Created))!;

    private async Task<ConceptDetailsResponse> SaveConceptAsync(Guid moduleId, string name, IReadOnlyList<Guid> prerequisites) =>
        (await SendAsync<ConceptDetailsResponse>(client, HttpMethod.Post,
            $"/api/modules/{moduleId}/concepts", ConceptPayload(name, prerequisites), HttpStatusCode.Created))!;

    private static object ConceptPayload(string name, IReadOnlyList<Guid> prerequisites, bool complete = true) => new
    {
        name,
        definition = complete ? $"{name} definition used in a test." : "",
        keywords = complete ? new[] { name.ToLowerInvariant() } : [],
        clues = complete ? new[] { $"Hint 1 for {name}", $"Hint 2 for {name}", $"Hint 3 for {name}" } : [],
        prerequisiteIds = prerequisites,
        recognitionActivities = complete ? Enumerable.Range(1, 3).Select(i => new
        {
            statement = $"Statement {i} for {name}", isCorrect = i % 2 == 0, explanation = $"Explanation {i} for {name}",
        }).ToArray() : [],
        fillBlankActivities = complete ? Enumerable.Range(1, 3).Select(i => new
        {
            text = $"{{{{1}}}} supports {name} #{i}", answers = new[] { new { slotNumber = 1, correctText = name } }, distractors = new[] { "Another answer" },
        }).ToArray() : [],
        orderingActivities = Array.Empty<object>(),
    };

    private static async Task<HttpResponseMessage> SendAsync(HttpClient http, HttpMethod method, string path, object? body)
    {
        var csrf = await http.GetFromJsonAsync<CsrfTokenResponse>("/api/auth/csrf");
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Add("X-CSRF-TOKEN", csrf!.Token);
        if (body is not null) request.Content = JsonContent.Create(body);
        return await http.SendAsync(request);
    }

    private static async Task<T?> SendAsync<T>(HttpClient http, HttpMethod method, string path, object? body,
        HttpStatusCode expected = HttpStatusCode.OK)
    {
        using var response = await SendAsync(http, method, path, body);
        var diagnostic = response.Headers.TryGetValues("X-Development-Exception", out var values) ? string.Join(" | ", values) : string.Empty;
        Assert.True(response.StatusCode == expected, $"Expected {expected}; received {response.StatusCode}: {await response.Content.ReadAsStringAsync()} {diagnostic}");
        if (response.Content.Headers.ContentLength == 0 || expected == HttpStatusCode.NoContent) return default;
        return await response.Content.ReadFromJsonAsync<T>();
    }

    private async Task SeedAndLoginStudentAsync(HttpClient http, Guid teacherId)
    {
        const string password = "student-test-password";
        var studentId = Guid.NewGuid();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var classroom = new Classroom { TeacherId = teacherId, Name = "Student test", Code = $"S{Guid.NewGuid():N}"[..20] };
            var student = new Student { Id = studentId, Classroom = classroom, EnrollmentNumber = "S1", Name = "Student", IsActivated = true };
            student.PasswordHash = new PasswordHasher<Student>().HashPassword(student, password);
            db.Students.Add(student);
            await db.SaveChangesAsync();
        }
        using var login = await SendAsync(http, HttpMethod.Post, "/api/auth/students/login",
            new { classroomCode = "invalid", enrollmentNumber = "S1", password });
        if (login.StatusCode == HttpStatusCode.Unauthorized)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var code = await db.Classrooms.Where(item => item.TeacherId == teacherId).Select(item => item.Code).SingleAsync();
            using var validLogin = await SendAsync(http, HttpMethod.Post, "/api/auth/students/login",
                new { classroomCode = code, enrollmentNumber = "S1", password });
            Assert.Equal(HttpStatusCode.OK, validLogin.StatusCode);
        }
    }

    private async Task<Guid> SeedAttemptAsync(Guid teacherId, Guid moduleId, Guid conceptId, Guid activityId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var classroom = new Classroom { TeacherId = teacherId, Name = "History test", Code = $"H{Guid.NewGuid():N}"[..20] };
        var student = new Student { Classroom = classroom, EnrollmentNumber = "H1", Name = "History student" };
        var session = new StudySession { Student = student, ModuleId = moduleId, Status = StudySessionStatus.Completed };
        db.ActivityAttempts.Add(new ActivityAttempt
        {
            Student = student, ConceptId = conceptId, StudySession = session, ActivityType = ActivityType.TrueFalse,
            RecognitionActivityId = activityId, WasCorrect = true, AttemptsUsed = 1,
            ActivitySnapshotJson = "{}",
        });
        await db.SaveChangesAsync();
        return session.Id;
    }

    [Fact]
    public async Task ExportThenImportPreservesContentAndCreatesNewDraftOwnedByCurrentTeacher()
    {
        var teacher = await CreateTeacherAsync(client, "roundtrip");
        var source = await CreateModuleAsync(client, "Transfer module");
        var ip = await SaveConceptAsync(source.Id, "IP", []);
        var dns = await SaveConceptAsync(source.Id, "DNS", [ip.Id]);

        var dnsInput = System.Text.Json.Nodes.JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(ConceptPayload("DNS", [ip.Id])))!;
        dnsInput["recognitionActivities"]!.AsArray().Add(System.Text.Json.Nodes.JsonNode.Parse(
            """{"statement":"Additional statement","isCorrect":true,"explanation":"Additional explanation"}"""));
        dnsInput["orderingActivities"]!.AsArray().Add(System.Text.Json.Nodes.JsonNode.Parse(
            """{"instruction":"Order steps","items":["First","Second"]}"""));
        await SendAsync<ConceptDetailsResponse>(client, HttpMethod.Put,
            $"/api/modules/{source.Id}/concepts/{dns.Id}", dnsInput);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var inactive = await db.RecognitionActivities.Where(item => item.ConceptId == dns.Id).OrderBy(item => item.CreatedAtUtc).FirstAsync();
            inactive.IsActive = false;
            await db.SaveChangesAsync();
        }
        await SeedAttemptAsync(teacher.Id, source.Id, dns.Id, dns.RecognitionActivities[0].Id);
        await SendAsync<ModuleDetailsResponse>(client, HttpMethod.Post, $"/api/modules/{source.Id}/publish", null);
        await SendAsync(client, HttpMethod.Post, $"/api/modules/{source.Id}/concepts/{dns.Id}/deactivate", null);
        await SendAsync(client, HttpMethod.Post, $"/api/modules/{source.Id}/concepts/{ip.Id}/deactivate", null);

        using var exported = await client.GetAsync($"/api/modules/{source.Id}/export");
        Assert.Equal(HttpStatusCode.OK, exported.StatusCode);
        Assert.StartsWith("application/json", exported.Content.Headers.ContentType!.MediaType);
        Assert.Contains("transfer-module.json", exported.Content.Headers.ContentDisposition!.FileName);
        var json = await exported.Content.ReadAsStringAsync();
        using var parsed = System.Text.Json.JsonDocument.Parse(json);
        Assert.Equal(1, parsed.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.DoesNotContain(source.Id.ToString(), json);
        Assert.DoesNotContain("teacherId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("passwordHash", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("activityAttempts", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"isActive\": false", json);
        Assert.Contains("\"orderingActivities\"", json);

        using var importedResponse = await SendRawAsync(client, "/api/modules/import", json);
        Assert.Equal(HttpStatusCode.Created, importedResponse.StatusCode);
        var imported = await importedResponse.Content.ReadFromJsonAsync<ModuleDetailsResponse>();
        Assert.NotNull(imported);
        Assert.Equal(ModuleStatus.Draft, imported!.Status);

        Assert.NotEqual(source.Id, imported.Id);
        var copiedIp = Assert.Single(imported.Concepts, item => item.Name == "IP");
        var copiedDns = Assert.Single(imported.Concepts, item => item.Name == "DNS");
        Assert.NotEqual(ip.Id, copiedIp.Id);
        Assert.NotEqual(dns.Id, copiedDns.Id);
        Assert.Equal([copiedIp.Id], copiedDns.PrerequisiteIds);
        Assert.Contains(copiedDns.RecognitionActivities, item => !item.IsActive);
        Assert.Single(copiedDns.OrderingActivities);
        Assert.Equal(6, copiedDns.FillBlankActivities.Count);
        Assert.Single(copiedDns.Keywords);


        await using var verifyScope = factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var storedCopy = await verifyDb.Modules.SingleAsync(item => item.Id == imported.Id);
        Assert.Equal(teacher.Id, storedCopy.TeacherId);
        Assert.Equal(ModuleStatus.Draft, storedCopy.Status);
        Assert.Equal(1, await verifyDb.ActivityAttempts.CountAsync(item => item.ConceptId == dns.Id));
        Assert.Equal(3, await verifyDb.ConceptClues.CountAsync(item => item.ConceptId == copiedDns.Id));
        Assert.Equal(3, await verifyDb.ConceptClues.CountAsync(item => item.ConceptId == copiedDns.Id && !item.IsActive));
    }

    [Fact]
    public async Task InvalidJsonSchemaAndContentAreRejectedAtomically()
    {
        await CreateTeacherAsync(client, "invalid-import");
        var cases = new (string Json, string ExpectedCode)[]
        {
            ("{", "invalid_json"),
            ("""{"schemaVersion":2,"module":{"externalId":"module","title":"Imported","description":null,"subject":"Networking","version":1,"concepts":[]}}""", "unsupported_schema_version"),
            (ExchangeJson(ExchangeConcept("same", []), ExchangeConcept("same", [])), "import_validation_failed"),
            (ExchangeJson([ExchangeConcept("module", [])]), "import_validation_failed"),
            (ExchangeJson(ExchangeConcept("dns", ["missing"])), "import_validation_failed"),
            (ExchangeJson(ExchangeConcept("dns", ["dns"])), "import_validation_failed"),
            (ExchangeJson(ExchangeConcept("a", ["b"]), ExchangeConcept("b", ["a"])), "import_validation_failed"),
            (ExchangeJson(ExchangeConcept("a", ["b"]), ExchangeConcept("b", ["c"]), ExchangeConcept("c", ["a"])), "import_validation_failed"),
            (ExchangeJson(ExchangeConcept("bad-fill", [], fillText: "Missing marker")), "import_validation_failed"),
            (ExchangeJson(ExchangeConcept("no-distractors", [], distractors: [])), "import_validation_failed"),
            (ExchangeJson(ExchangeConcept("bad-order", [], orderingItems: ["Only one"])), "import_validation_failed"),
            (ExchangeJson(ExchangeConcept("bad-activity", [], statement: "")), "import_validation_failed"),
            (ExchangeJson([ExchangeConcept("forged-owner", [])], includeTeacherId: true), "invalid_json"),
        };

        foreach (var (json, expectedCode) in cases)
        {
            using var response = await SendRawAsync(client, "/api/modules/import", json);
            Assert.True(response.StatusCode == HttpStatusCode.BadRequest, $"Expected {expectedCode}, got {(int)response.StatusCode} for {json}: {await response.Content.ReadAsStringAsync()}");
            Assert.Contains(expectedCode, await response.Content.ReadAsStringAsync());
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Empty(await db.Modules.ToListAsync());
        Assert.Empty(await db.Concepts.ToListAsync());
        Assert.Empty(await db.ConceptPrerequisites.ToListAsync());
        Assert.Empty(await db.ConceptKeywords.ToListAsync());
        Assert.Empty(await db.ConceptClues.ToListAsync());
        Assert.Empty(await db.RecognitionActivities.ToListAsync());
        Assert.Empty(await db.FillBlankActivities.ToListAsync());
        Assert.Empty(await db.FillBlankAnswers.ToListAsync());
        Assert.Empty(await db.FillBlankDistractors.ToListAsync());
        Assert.Empty(await db.OrderingActivities.ToListAsync());
        Assert.Empty(await db.OrderingActivityItems.ToListAsync());
    }

    [Fact]
    public async Task TransferEndpointsRequireTeacherAndEnforceModuleOwnership()
    {
        var anonymous = CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/modules/00000000-0000-0000-0000-000000000000/export")).StatusCode);
        using (var anonymousImport = await SendRawAsync(anonymous, "/api/modules/import", ExchangeJson(ExchangeConcept("anonymous", []))))
            Assert.Equal(HttpStatusCode.Unauthorized, anonymousImport.StatusCode);
        anonymous.Dispose();
        var owner = await CreateTeacherAsync(client, "export-owner");
        var module = await CreateModuleAsync(client, "Owned");
        var foreignClient = CreateClient();
        var foreignTeacher = await CreateTeacherAsync(foreignClient, "export-other");
        Assert.Equal(HttpStatusCode.NotFound,
            (await foreignClient.GetAsync($"/api/modules/{module.Id}/export")).StatusCode);
        using (var importedByForeign = await SendRawAsync(foreignClient, "/api/modules/import", ExchangeJson(ExchangeConcept("x", []))))
        {
            Assert.Equal(HttpStatusCode.Created, importedByForeign.StatusCode);
            var importedForeignModule = await importedByForeign.Content.ReadFromJsonAsync<ModuleDetailsResponse>();
            await using var ownerScope = factory.Services.CreateAsyncScope();
            var ownerDb = ownerScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal(foreignTeacher.Id, await ownerDb.Modules.Where(item => item.Id == importedForeignModule!.Id).Select(item => item.TeacherId).SingleAsync());
        }

        var studentClient = CreateClient();
        await SeedAndLoginStudentAsync(studentClient, owner.Id);
        using var studentExport = await studentClient.GetAsync($"/api/modules/{module.Id}/export");
        Assert.Equal(HttpStatusCode.Forbidden, studentExport.StatusCode);
        using var studentImport = await SendRawAsync(studentClient, "/api/modules/import", ExchangeJson(ExchangeConcept("student", [])));
        Assert.Equal(HttpStatusCode.Forbidden, studentImport.StatusCode);
        foreignClient.Dispose();
        studentClient.Dispose();
    }

    private async Task<HttpResponseMessage> SendRawAsync(HttpClient http, string path, string json)
    {
        var csrf = await http.GetFromJsonAsync<CsrfTokenResponse>("/api/auth/csrf");
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Add("X-CSRF-TOKEN", csrf!.Token);
        request.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        return await http.SendAsync(request);
    }

    private static string ExchangeJson(params object[] concepts) => ExchangeJson(concepts, false);

    private static string ExchangeJson(object[] concepts, bool includeTeacherId)
    {
        var module = new Dictionary<string, object?>
        {
            ["externalId"] = "module", ["title"] = "Imported", ["description"] = null,
            ["subject"] = "Networking", ["version"] = 3, ["concepts"] = concepts,
        };
        var root = new Dictionary<string, object?> { ["schemaVersion"] = 1, ["module"] = module };
        if (includeTeacherId) module["teacherId"] = Guid.NewGuid();
        return System.Text.Json.JsonSerializer.Serialize(root);
    }

    private static object ExchangeConcept(
        string id,
        string[] prerequisites,
        string fillText = "{{1}} supports networking",
        string[]? orderingItems = null,
        string statement = "A networking statement",
        string[]? distractors = null)
    {
        object[] ordering = orderingItems is null ? [] :
        [
            new { instruction = "Order the steps", items = orderingItems, isActive = true },
        ];
        return new
        {
            externalId = id, name = id, definition = $"{id} definition", isActive = true,
            keywords = new[] { new { value = id, isActive = true } },
            clues = new[] { new { text = $"Clue for {id}", isActive = true } },
            prerequisites,
            trueFalseActivities = new[] { new { statement, isCorrect = true, explanation = "Explanation", isActive = true } },
            fillBlankActivities = new[] { new
            {
                text = fillText, answers = new[] { new { slotNumber = 1, correctText = id } },
                distractors = distractors ?? ["other"], isActive = true,
            } },
            orderingActivities = ordering,
        };
    }
}












