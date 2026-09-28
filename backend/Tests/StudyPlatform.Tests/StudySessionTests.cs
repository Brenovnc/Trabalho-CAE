using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using StudyPlatform.Api.Data;
using StudyPlatform.Api.Domain.Enums;
using StudyPlatform.Api.DTOs.Learning;
using StudyPlatform.Api.Exceptions;
using StudyPlatform.Api.Models;
using StudyPlatform.Api.Models.Activities;
using StudyPlatform.Api.Services.Learning;
using Xunit;
namespace StudyPlatform.Tests;

[Collection("LearningDatabase")]
public sealed class StudySessionTests : IAsyncLifetime
{
    private const string DatabaseName = "trabalho_cae_learning_test";
    private readonly string connectionString = BuildConnectionString();
    private readonly LearningDbFactory factory;
    private SeededIds ids = null!;
    private readonly ManualTimeProvider clock = new(new DateTimeOffset(2026, 9, 27, 12, 0, 0, TimeSpan.Zero));

    public StudySessionTests() => factory = new LearningDbFactory(connectionString);

    public async Task InitializeAsync()
    {
        await factory.ResetDatabaseAsync();
        ids = await SeedAsync(includeOrdering: true);
    }

    public async Task DisposeAsync() => await factory.DisposeAsync();

    [Fact]
    public async Task ExposureIsResumableRequiresAllKeywordsAndDuplicateSubmissionCannotCreateSecondAttempt()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var service = ResolveService(scope.ServiceProvider);
        var start = await service.StartAsync(ids.StudentId, ids.ModuleId, default);
        Assert.Equal("EXPOSURE", start.Activity!.Type);
        Assert.Empty(start.Activity.Payload.GetProperty("revealedKeywords").EnumerateArray());
        Assert.DoesNotContain("domain name", start.Activity.Payload.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("IP address", start.Activity.Payload.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(start.Activity.StartedAtUtc, start.Activity.StartedAtUtc);
        Assert.Equal(2, start.Activity.Payload.GetProperty("keywordCount").GetInt32());

        var resumed = await service.StartAsync(ids.StudentId, ids.ModuleId, default);
        Assert.Equal(start.SessionId, resumed.SessionId);
        Assert.Equal(start.Activity.PresentationId, resumed.Activity!.PresentationId);
        await Assert.ThrowsAsync<ApiException>(() => service.SubmitAsync(ids.StudentId, start.SessionId,
            Request(start.Activity, "EXPOSURE", new { completed = true }), default));
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().ActivityAttempts.ToListAsync());

        await service.RevealNextHintAsync(ids.StudentId, start.SessionId, start.Activity.PresentationId, default);
        await Assert.ThrowsAsync<ApiException>(() => service.SubmitAsync(ids.StudentId, start.SessionId,
            Request(start.Activity, "EXPOSURE", new { completed = true }), default));
        await service.RevealNextHintAsync(ids.StudentId, start.SessionId, start.Activity.PresentationId, default);
        var result = await service.SubmitAsync(ids.StudentId, start.SessionId,
            Request(start.Activity, "EXPOSURE", new { completed = true }), default);
        Assert.True(result.WasCorrect);
        Assert.Equal("RECOGNITION", result.LearningState);
        Assert.Equal("TRUE_FALSE", result.NextActivity!.Type);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var attempt = await db.ActivityAttempts.SingleAsync();
        Assert.True(attempt.WasCorrect);
        Assert.Null(attempt.FsrsRating);
        Assert.Equal(1, (await db.StudySessions.SingleAsync()).CompletedActivities);
        await Assert.ThrowsAsync<ApiException>(() => service.SubmitAsync(ids.StudentId, start.SessionId,
            Request(start.Activity, "EXPOSURE", new { completed = true }), default));
        Assert.Single(await db.ActivityAttempts.ToListAsync());
    }

    [Fact]
    public async Task FreePracticeIgnoresPrerequisitesAndNeverChangesOfficialStateAndResetPreservesHistory()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var prerequisite = await db.Concepts.SingleAsync(concept => concept.Id == ids.ConceptId);
        var dependent = new Concept { ModuleId = ids.ModuleId, Name = "Blocked in normal mode", Definition = "Dependent concept." };
        dependent.Keywords.Add(new ConceptKeyword { Value = "dependent" });
        db.Concepts.Add(dependent);
        db.ConceptPrerequisites.Add(new ConceptPrerequisite { Concept = dependent, PrerequisiteConceptId = prerequisite.Id, ModuleId = ids.ModuleId });
        var classroomId = await db.Students.Where(student => student.Id == ids.StudentId).Select(student => student.ClassroomId).SingleAsync();
        var teacherId = await db.Modules.Where(module => module.Id == ids.ModuleId).Select(module => module.TeacherId).SingleAsync();
        var otherStudent = new Student { ClassroomId = classroomId, EnrollmentNumber = $"O{Guid.NewGuid():N}"[..8], IsActive = true, IsActivated = true, PasswordHash = "unused" };
        var otherModule = new Module { TeacherId = teacherId, Title = "Other module", Subject = "Network", Status = ModuleStatus.Published };
        var otherConcept = new Concept { Module = otherModule, Name = "Other module concept", Definition = "Still preserved." };
        db.AddRange(otherStudent, otherModule, new ClassroomModule { ClassroomId = classroomId, Module = otherModule }, otherConcept);
        await db.SaveChangesAsync();
        db.StudentConceptStates.AddRange(
            new StudentConceptState { StudentId = otherStudent.Id, ConceptId = dependent.Id, LearningState = LearningState.Mastered },
            new StudentConceptState { StudentId = ids.StudentId, ConceptId = otherConcept.Id, LearningState = LearningState.Recognition });
        var historicalSession = new StudySession { StudentId = ids.StudentId, ModuleId = ids.ModuleId, Status = StudySessionStatus.Completed, CompletedAtUtc = clock.GetUtcNow().UtcDateTime };
        db.StudySessions.Add(historicalSession);
        var initial = new StudentConceptState
        {
            StudentId = ids.StudentId, Concept = dependent, LearningState = LearningState.GuidedRecall,
            FreeRecallSuccessCount = 1, LastAttemptAtUtc = clock.GetUtcNow().UtcDateTime.AddDays(-2),
            LastSuccessAtUtc = clock.GetUtcNow().UtcDateTime.AddDays(-3), LastFailureAtUtc = clock.GetUtcNow().UtcDateTime.AddDays(-4),
            LastFreeRecallSuccessAtUtc = clock.GetUtcNow().UtcDateTime.AddDays(-5), LastFreeRecallSuccessSessionId = historicalSession.Id,
            FsrsState = "Review:-", Difficulty = 4.2, Stability = 12.5, DueAtUtc = clock.GetUtcNow().UtcDateTime.AddDays(2),
            LastReviewAtUtc = clock.GetUtcNow().UtcDateTime.AddDays(-1), ElapsedDays = 1, ScheduledDays = 3, Repetitions = 4, Lapses = 1,
            LastFsrsRating = FsrsRating.Hard,
        };
        db.StudentConceptStates.Add(initial);
        await db.SaveChangesAsync();
        var before = StateSnapshot(initial);

        var service = ResolveService(scope.ServiceProvider);
        var practice = await service.StartAsync(ids.StudentId, ids.ModuleId, default, StudySessionMode.FreePractice, dependent.Id);
        Assert.Equal("FREE_PRACTICE", practice.Mode);
        Assert.Equal(dependent.Id, practice.Activity!.ConceptId);
        Assert.Equal("EXPOSURE", practice.Activity.Type);
        Assert.Equal("ACTIVE", practice.Status);
        var current = practice.Activity;
        await service.RevealNextHintAsync(ids.StudentId, practice.SessionId, current.PresentationId, default);
        var result = await service.SubmitAsync(ids.StudentId, practice.SessionId, Request(current, "EXPOSURE", new { completed = true }), default);
        Assert.True(result.WasCorrect);
        Assert.Equal("FREE_PRACTICE", result.LearningState);
        Assert.Equal(before, StateSnapshot(await db.StudentConceptStates.SingleAsync(state => state.StudentId == ids.StudentId && state.ConceptId == dependent.Id)));
        var attempt = await db.ActivityAttempts.SingleAsync();
        Assert.Null(attempt.FsrsRating);
        Assert.Equal(StudySessionMode.FreePractice, (await db.StudySessions.SingleAsync(session => session.Id == practice.SessionId)).Mode);

        var activePractice = await service.StartAsync(ids.StudentId, ids.ModuleId, default, StudySessionMode.FreePractice);
        Assert.Equal("ACTIVE", activePractice.Status);
        await service.ResetModuleProgressAsync(ids.StudentId, ids.ModuleId, default);
        Assert.Empty(await db.StudentConceptStates.Where(state => state.StudentId == ids.StudentId && state.Concept.ModuleId == ids.ModuleId).ToListAsync());
        Assert.Equal(StudySessionStatus.Abandoned, (await db.StudySessions.SingleAsync(session => session.Id == activePractice.SessionId)).Status);
        Assert.Equal(StudySessionStatus.Completed, (await db.StudySessions.SingleAsync(session => session.Id == historicalSession.Id)).Status);
        Assert.Single(await db.ActivityAttempts.ToListAsync());
        Assert.Equal(LearningState.Mastered, (await db.StudentConceptStates.SingleAsync(state => state.StudentId == otherStudent.Id && state.ConceptId == dependent.Id)).LearningState);
        Assert.Equal(LearningState.Recognition, (await db.StudentConceptStates.SingleAsync(state => state.StudentId == ids.StudentId && state.ConceptId == otherConcept.Id)).LearningState);
    }

    private static object StateSnapshot(StudentConceptState state) => new
    {
        state.LearningState, state.FreeRecallSuccessCount, state.LastAttemptAtUtc, state.LastSuccessAtUtc,
        state.LastFailureAtUtc, state.LastFreeRecallSuccessAtUtc, state.LastFreeRecallSuccessSessionId,
        state.FsrsState, state.Difficulty, state.Stability, state.DueAtUtc, state.LastReviewAtUtc,
        state.ElapsedDays, state.ScheduledDays, state.Repetitions, state.Lapses, state.LastFsrsRating,
    };

    [Fact]
    public async Task TrueFalseAndFillBlankAreGradedByBackendAndProgressionIsIncremental()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var service = ResolveService(scope.ServiceProvider);
        var session = await service.StartAsync(ids.StudentId, ids.ModuleId, default);
        var activity = session.Activity!;
        await service.RevealNextHintAsync(ids.StudentId, session.SessionId, activity.PresentationId, default);
        await service.RevealNextHintAsync(ids.StudentId, session.SessionId, activity.PresentationId, default);
        var exposure = await service.SubmitAsync(ids.StudentId, session.SessionId, Request(activity, "EXPOSURE", new { completed = true }), default);
        activity = exposure.NextActivity!;
        Assert.False(activity.Payload.TryGetProperty("isCorrect", out _));
        var recognized = await service.SubmitAsync(ids.StudentId, session.SessionId, Request(activity, "TRUE_FALSE", new { choice = true }), default);
        Assert.True(recognized.WasCorrect);
        Assert.Equal("GUIDEDRECALL", recognized.LearningState.Replace("_", ""));
        activity = recognized.NextActivity!;
        Assert.Equal("FILL_BLANK", activity.Type);
        Assert.False(activity.Payload.TryGetProperty("correctAnswers", out _));
        var slots = activity.Payload.GetProperty("slotNumbers").EnumerateArray().Select(x => x.GetInt32()).ToArray();
        Assert.Equal(2, slots.Length);
        var fill = await service.SubmitAsync(ids.StudentId, session.SessionId, Request(activity, "FILL_BLANK", new
        {
            answers = new[] { new { slotNumber = slots[0], text = " dns " }, new { slotNumber = slots[1], text = "IP addresses" } },
        }), default);
        Assert.True(fill.WasCorrect);
        Assert.Equal("FREERECALL", fill.LearningState.Replace("_", ""));
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(3, await db.ActivityAttempts.CountAsync());
        Assert.All(await db.ActivityAttempts.ToListAsync(), item => Assert.NotNull(item.ActivitySnapshotJson));
    }

    [Fact]
    public async Task FillBlankRejectsMissingExtraAndDuplicateSlotsAndUsesWholeAnswerOnly()
    {
        var newIds = await SeedAsync(addSecondConcept: true, state: LearningState.GuidedRecall, includeTF: false, includeFill: true, includeOrdering: false);
        await using var scope = factory.Services.CreateAsyncScope();
        var service = ResolveService(scope.ServiceProvider);
        var start = await service.StartAsync(ids.StudentId, newIds.SecondModuleId, default);
        var activity = start.Activity!;
        var oneSlot = new { answers = new[] { new { slotNumber = 1, text = "DNS" } } };
        await Assert.ThrowsAsync<ApiException>(() => service.SubmitAsync(ids.StudentId, start.SessionId, Request(activity, "FILL_BLANK", oneSlot), default));
        var wrong = await service.SubmitAsync(ids.StudentId, start.SessionId, Request(activity, "FILL_BLANK", new
        {
            answers = new[] { new { slotNumber = 1, text = "DNS" }, new { slotNumber = 2, text = "wrong" } },
        }), default);
        Assert.False(wrong.WasCorrect);
        Assert.Equal("RECOGNITION", wrong.LearningState);
        Assert.Equal(FsrsRating.Again, (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().StudentConceptStates.SingleAsync()).LastFsrsRating);
    }

    [Fact]
    public async Task GuessConceptUsesTrimmedCaseInsensitiveAnswerAndBoundedClientEffort()
    {
        var second = await SeedAsync(addSecondConcept: true, state: LearningState.FreeRecall, includeTF: false, includeFill: false, includeOrdering: false);
        await using var scope = factory.Services.CreateAsyncScope();
        var service = ResolveService(scope.ServiceProvider);
        var start = await service.StartAsync(ids.StudentId, second.SecondModuleId, default);
        var activity = start.Activity!;
        Assert.Equal("GUESS_CONCEPT", activity.Type);
        Assert.Equal("first clue", activity.Payload.GetProperty("clues")[0].GetString());
        var response = await service.SubmitAsync(ids.StudentId, start.SessionId,
            Request(activity, "GUESS_CONCEPT", new { text = " dns " }, attempts: 2, hints: 1), default);
        Assert.True(response.WasCorrect);
        Assert.Equal("DNS", response.CorrectAnswer!.Value.GetProperty("text").GetString());
        var state = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().StudentConceptStates.SingleAsync();
        Assert.Equal(1, state.FreeRecallSuccessCount);
        Assert.Equal(FsrsRating.Hard, state.LastFsrsRating);
        Assert.NotNull(state.DueAtUtc);
        Assert.Single(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().ActivityAttempts.ToListAsync());
    }

    [Fact]
    public async Task OrderingUsesItemIdsAndExactSequenceAndNoCandidateCompletesShortSession()
    {
        var seed = await SeedAsync(addSecondConcept: true, state: LearningState.Recognition, includeTF: false, includeFill: false, includeOrdering: true);
        await using var scope = factory.Services.CreateAsyncScope();
        var service = ResolveService(scope.ServiceProvider);
        var start = await service.StartAsync(ids.StudentId, seed.SecondModuleId, default);
        var activity = start.Activity!;
        Assert.Equal("ORDERING", activity.Type);
        var dbForOrdering = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var orderingId = await dbForOrdering.OrderingActivities.Where(x => x.ConceptId == seed.SecondConceptId).Select(x => x.Id).SingleAsync();
        var itemIds = await dbForOrdering.OrderingActivityItems.Where(x => x.OrderingActivityId == orderingId).OrderBy(x => x.Position).Select(x => x.Id).ToArrayAsync();
        var correct = await service.SubmitAsync(ids.StudentId, start.SessionId,
            Request(activity, "ORDERING", new { itemIds }), default);
        Assert.True(correct.WasCorrect);
        Assert.Equal("COMPLETED", correct.SessionStatus);
        Assert.Equal(1, correct.CompletedActivities);
        Assert.Equal(1, correct.TotalActivities);
        var session = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().StudySessions.SingleAsync(x => x.Id == start.SessionId);
        Assert.NotNull(session.CompletedAtUtc);
    }

    [Fact]
    public async Task PrerequisitesBlockNewConceptsUntilEveryRequirementIsMastered()
    {
        var seed = await SeedAsync(addSecondConcept: true, state: null, includeTF: true, includeFill: true, includeOrdering: false);
        await using (var prerequisiteScope = factory.Services.CreateAsyncScope())
        {
            var prerequisiteDb = prerequisiteScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var prerequisite = new Concept { ModuleId = seed.SecondModuleId, Name = "Prerequisite", Definition = "Must be mastered first." };
            prerequisiteDb.Concepts.Add(prerequisite);
            prerequisiteDb.ConceptPrerequisites.Add(new ConceptPrerequisite
            {
                ConceptId = seed.SecondConceptId,
                PrerequisiteConceptId = prerequisite.Id,
                ModuleId = seed.SecondModuleId,
            });
            await prerequisiteDb.SaveChangesAsync();
        }
        await using var scope = factory.Services.CreateAsyncScope();
        var service = ResolveService(scope.ServiceProvider);
        var start = await service.StartAsync(ids.StudentId, seed.SecondModuleId, default);
        Assert.Equal("COMPLETED", start.Status);
        Assert.Null(start.Activity);
        Assert.Equal(0, start.TotalActivities);
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().SessionActivityPresentations.ToListAsync());
    }

    [Fact]
    public async Task MasteredConceptIsNotSelectedBeforeDueAndReturnsForMaintenanceWhenDue()
    {
        var seed = await SeedAsync(addSecondConcept: true, state: LearningState.Mastered, includeTF: false, includeFill: false, includeOrdering: false);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var state = await db.StudentConceptStates.SingleAsync();
            state.DueAtUtc = clock.GetUtcNow().UtcDateTime.AddHours(1);
            await db.SaveChangesAsync();
            var service = ResolveService(scope.ServiceProvider);
            var future = await service.StartAsync(ids.StudentId, seed.SecondModuleId, default);
            Assert.Equal("COMPLETED", future.Status);
            Assert.Null(future.Activity);
        }

        await factory.ResetDatabaseAsync();
        ids = await SeedAsync(addSecondConcept: true, state: LearningState.Mastered, includeTF: false, includeFill: false, includeOrdering: false);
        await using var dueScope = factory.Services.CreateAsyncScope();
        var dueDb = dueScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var dueState = await dueDb.StudentConceptStates.SingleAsync();
        dueState.DueAtUtc = clock.GetUtcNow().UtcDateTime.AddMinutes(-1);
        await dueDb.SaveChangesAsync();
        var dueSession = await ResolveService(dueScope.ServiceProvider).StartAsync(ids.StudentId, ids.SecondModuleId, default);
        Assert.Equal("GUESS_CONCEPT", dueSession.Activity!.Type);
    }
    [Fact]
    public async Task SessionAccessRequiresStudentRoleAndCsrfAndTeacherCannotStartOne()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var teacher = await db.Teachers.SingleAsync();
        teacher.PasswordHash = new PasswordHasher<Teacher>().HashPassword(teacher, "TeacherPass123!");
        await db.SaveChangesAsync();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true, AllowAutoRedirect = false });
        var csrfResponse = await client.GetAsync("/api/auth/csrf");
        csrfResponse.EnsureSuccessStatusCode();
        var token = (await csrfResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;
        using var teacherLogin = new HttpRequestMessage(HttpMethod.Post, "/api/auth/teachers/login")
        {
            Content = JsonContent.Create(new { email = "session@example.test", password = "TeacherPass123!" }),
        };
        teacherLogin.Headers.Add("X-CSRF-TOKEN", token);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(teacherLogin)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync($"/api/student/modules/{ids.ModuleId}/sessions", JsonContent.Create(new { }))).StatusCode);

        using var withoutCsrf = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout") { Content = JsonContent.Create(new { }) };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(withoutCsrf)).StatusCode);

        var student = await db.Students.SingleAsync();
        student.PasswordHash = new PasswordHasher<Student>().HashPassword(student, "StudentPass123!");
        await db.SaveChangesAsync();
        using var studentClient = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true, AllowAutoRedirect = false });
        var studentCsrf = await studentClient.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        var studentToken = studentCsrf.GetProperty("token").GetString()!;
        using var studentLogin = new HttpRequestMessage(HttpMethod.Post, "/api/auth/students/login")
        {
            Content = JsonContent.Create(new { classroomCode = ids.ClassroomCode, enrollmentNumber = ids.EnrollmentNumber, password = "StudentPass123!" }),
        };
        studentLogin.Headers.Add("X-CSRF-TOKEN", studentToken);
        Assert.Equal(HttpStatusCode.OK, (await studentClient.SendAsync(studentLogin)).StatusCode);
        studentToken = (await studentClient.GetFromJsonAsync<JsonElement>("/api/auth/csrf")).GetProperty("token").GetString()!;

        async Task<HttpResponseMessage> PostStudentAsync(string path, object body)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
            request.Headers.Add("X-CSRF-TOKEN", studentToken);
            return await studentClient.SendAsync(request);
        }

        var startResponse = await PostStudentAsync($"/api/student/modules/{ids.ModuleId}/sessions", new { });
        Assert.Equal(HttpStatusCode.OK, startResponse.StatusCode);
        var start = await startResponse.Content.ReadFromJsonAsync<JsonElement>();
        var sessionId = start.GetProperty("sessionId").GetGuid();
        var activity = start.GetProperty("activity");
        var presentationId = activity.GetProperty("presentationId").GetGuid();
        Assert.Equal("EXPOSURE", activity.GetProperty("type").GetString());
        await PostStudentAsync($"/api/student/sessions/{sessionId}/activities/{presentationId}/reveal", new { });
        await PostStudentAsync($"/api/student/sessions/{sessionId}/activities/{presentationId}/reveal", new { });
        var exposureAnswer = await PostStudentAsync($"/api/student/sessions/{sessionId}/answer", new
        {
            presentationId,
            type = "EXPOSURE",
            answer = new { completed = true },
            attemptsUsed = 1,
            hintsUsed = 0,
        });
        Assert.Equal(HttpStatusCode.OK, exposureAnswer.StatusCode);
        var exposureResult = await exposureAnswer.Content.ReadFromJsonAsync<JsonElement>();
        var tf = exposureResult.GetProperty("nextActivity");
        var tfPresentation = tf.GetProperty("presentationId").GetGuid();
        var duplicateBody = new { presentationId = tfPresentation, type = "TRUE_FALSE", answer = new { choice = true }, attemptsUsed = 1, hintsUsed = 0 };
        var duplicateResponses = await Task.WhenAll(
            PostStudentAsync($"/api/student/sessions/{sessionId}/answer", duplicateBody),
            PostStudentAsync($"/api/student/sessions/{sessionId}/answer", duplicateBody));
        Assert.Contains(duplicateResponses, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Contains(duplicateResponses, response => response.StatusCode == HttpStatusCode.Conflict);
        using var resetWithoutCsrf = new HttpRequestMessage(HttpMethod.Post, $"/api/student/modules/{ids.ModuleId}/reset-progress") { Content = JsonContent.Create(new { }) };
        Assert.Equal(HttpStatusCode.BadRequest, (await studentClient.SendAsync(resetWithoutCsrf)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await PostStudentAsync($"/api/student/modules/{ids.ModuleId}/reset-progress", new { })).StatusCode);
        await using var verifyScope = factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(2, await verifyDb.ActivityAttempts.CountAsync());
        Assert.Equal(StudySessionStatus.Abandoned, (await verifyDb.StudySessions.SingleAsync()).Status);
    }

    private async Task<SeededIds> SeedAsync(bool addSecondConcept = false, LearningState? state = null, bool includeTF = true, bool includeFill = true, bool includeOrdering = false)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var teacher = await db.Teachers.SingleOrDefaultAsync();
        var classroom = await db.Classrooms.SingleOrDefaultAsync();
        var student = await db.Students.SingleOrDefaultAsync();
        var module = await db.Modules.SingleOrDefaultAsync();
        var concept = module is null ? null : await db.Concepts.SingleOrDefaultAsync(x => x.ModuleId == module.Id);
        if (teacher is null)
        {
            teacher = new Teacher { Name = "Session Teacher", Email = "session@example.test", PasswordHash = "unused" };
            classroom = new Classroom { Teacher = teacher, Name = "Session class", Code = $"SESSION-{Guid.NewGuid():N}"[..15] };
            student = new Student { Classroom = classroom, EnrollmentNumber = $"S{Guid.NewGuid():N}"[..8], Name = "Student", IsActive = true, IsActivated = true, PasswordHash = "unused" };
            module = new Module { Teacher = teacher, Title = "Session module", Subject = "Network", Status = ModuleStatus.Published };
            concept = MakeConcept(module, includeTF, includeFill, includeOrdering);
            db.AddRange(teacher, classroom, student, module, concept, new ClassroomModule { Classroom = classroom, Module = module });
            await db.SaveChangesAsync();
        }

        Module? secondModule = null;
        Concept? secondConcept = null;
        if (addSecondConcept)
        {
            secondModule = new Module { TeacherId = teacher!.Id, Title = $"Session module {Guid.NewGuid():N}"[..20], Subject = "Network", Status = ModuleStatus.Published };
            secondConcept = MakeConcept(secondModule, includeTF, includeFill, includeOrdering);
            db.Modules.Add(secondModule);
            db.Concepts.Add(secondConcept);
            db.ClassroomModules.Add(new ClassroomModule { ClassroomId = classroom!.Id, Module = secondModule });
            await db.SaveChangesAsync();
        }
        if (state is not null)
            db.StudentConceptStates.Add(new StudentConceptState { StudentId = student!.Id, ConceptId = (secondConcept ?? concept!).Id, LearningState = state.Value });

        await db.SaveChangesAsync();
        return new SeededIds(student!.Id, module!.Id, concept!.Id, secondModule?.Id ?? Guid.Empty, secondConcept?.Id ?? Guid.Empty, classroom!.Code, student.EnrollmentNumber);
    }

    private static Concept MakeConcept(Module module, bool includeTF, bool includeFill, bool includeOrdering)
    {
        var concept = new Concept { Module = module, Name = "DNS", Definition = "DNS maps a domain name to an IP address." };
        concept.Keywords.Add(new ConceptKeyword { Value = "domain name" });
        concept.Keywords.Add(new ConceptKeyword { Value = "IP address" });
        concept.Clues.Add(new ConceptClue { Position = 0, Text = "first clue" });
        concept.Clues.Add(new ConceptClue { Position = 1, Text = "second clue" });
        if (includeTF)
            for (var i = 0; i < 3; i++) concept.RecognitionActivities.Add(new RecognitionActivity { Statement = $"Statement {i}", IsCorrect = true, Explanation = "Correct because DNS resolves names." });
        if (includeFill)
            for (var i = 0; i < 3; i++)
            {
                var fill = new FillBlankActivity { Text = "{1} maps to {2}." };
                fill.Answers.Add(new FillBlankAnswer { SlotNumber = 1, CorrectText = "DNS" });
                fill.Answers.Add(new FillBlankAnswer { SlotNumber = 2, CorrectText = "IP addresses" });
                fill.Distractors.Add(new FillBlankDistractor { Text = "HTTP" });
                concept.FillBlankActivities.Add(fill);
            }
        if (includeOrdering)
        {
            var ordering = new OrderingActivity { Instruction = "Arrange the steps" };
            ordering.Items.Add(new OrderingActivityItem { Position = 0, Text = "Query" });
            ordering.Items.Add(new OrderingActivityItem { Position = 1, Text = "Resolve" });
            concept.OrderingActivities.Add(ordering);
        }
        return concept;
    }

    private StudySessionService ResolveService(IServiceProvider services)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        var eligibility = new ConceptEligibilityService(db, clock);
        var progression = new LearningProgressionService(db, new FsrsService(), new PerformanceRatingService(), eligibility, clock);
        var selection = new ActivitySelectionService(db, new ActivityCompatibilityService(), eligibility, progression, clock);
        return new StudySessionService(db, selection, progression, new PerformanceRatingService(), clock);
    }

    private static SubmitActivityAnswerRequest Request(PresentedActivityResponse activity, string type, object answer, int? attempts = null, int? hints = null)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(answer));
        return new SubmitActivityAnswerRequest(activity.PresentationId, type, document.RootElement.Clone(), attempts, hints);
    }

    private static string BuildConnectionString()
    {
        var raw = Environment.GetEnvironmentVariable("STUDYPLATFORM_LEARNING_TEST_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=trabalho_cae_learning_test;Username=trabalho_cae;Password=local_dev_password";
        var builder = new NpgsqlConnectionStringBuilder(raw);
        if (builder.Database != DatabaseName) throw new InvalidOperationException($"Tests require the dedicated {DatabaseName} database.");
        return builder.ConnectionString;
    }

    private sealed record SeededIds(Guid StudentId, Guid ModuleId, Guid ConceptId, Guid SecondModuleId, Guid SecondConceptId, string ClassroomCode, string EnrollmentNumber);
}
