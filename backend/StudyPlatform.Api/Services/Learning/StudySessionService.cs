using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data.Common;
using StudyPlatform.Api.Data;
using StudyPlatform.Api.Domain.Enums;
using StudyPlatform.Api.DTOs.Learning;
using StudyPlatform.Api.Exceptions;
using StudyPlatform.Api.Models;
using StudyPlatform.Api.Models.Activities;

namespace StudyPlatform.Api.Services.Learning;

public sealed class StudySessionService(
    ApplicationDbContext db,
    ActivitySelectionService selection,
    LearningProgressionService progression,
    PerformanceRatingService performanceRatingService,
    TimeProvider timeProvider)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<StartStudySessionResponse> StartAsync(Guid studentId, Guid moduleId, CancellationToken ct)
    {
        await EnsureStudyAccessAsync(studentId, moduleId, ct);
        var existing = await db.StudySessions.SingleOrDefaultAsync(x => x.StudentId == studentId && x.ModuleId == moduleId && x.Status == StudySessionStatus.Active, ct);
        if (existing is not null)
        {
            var activity = await GetOrCreateCurrentAsync(existing, ct);
            return MapStart(existing, activity);
        }

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await AcquireStudentModuleLockAsync(studentId, moduleId, ct);
        existing = await db.StudySessions.SingleOrDefaultAsync(x => x.StudentId == studentId && x.ModuleId == moduleId && x.Status == StudySessionStatus.Active, ct);
        if (existing is not null)
        {
            var activity = await db.SessionActivityPresentations.Where(x => x.StudySessionId == existing.Id && x.AnsweredAtUtc == null).OrderByDescending(x => x.SequenceNumber).FirstOrDefaultAsync(ct);
            if (activity is null)
            {
                activity = await selection.PresentNextAsync(existing, ct);
                if (activity is null) Complete(existing, Now);
            }
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return MapStart(existing, activity);
        }

        var session = new StudySession { StudentId = studentId, ModuleId = moduleId, StartedAtUtc = Now };
        db.StudySessions.Add(session);
        await db.SaveChangesAsync(ct);
        var first = await selection.PresentNextAsync(session, ct);
        if (first is null) Complete(session, Now);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return MapStart(session, first);
    }

    public async Task<StudySessionResponse> GetAsync(Guid studentId, Guid sessionId, CancellationToken ct)
    {
        var session = await GetOwnedSessionAsync(studentId, sessionId, ct);
        await EnsureStudyAccessAsync(studentId, session.ModuleId, ct);
        var activity = await GetOrCreateCurrentAsync(session, ct);
        return MapSession(session, activity);
    }

    public async Task<StudySessionResponse> GetNextAsync(Guid studentId, Guid sessionId, CancellationToken ct) =>
        await GetAsync(studentId, sessionId, ct);

    public async Task<RevealHintResponse> RevealNextHintAsync(Guid studentId, Guid sessionId, Guid presentationId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        var session = await LockOwnedSessionAsync(studentId, sessionId, ct);
        await EnsureStudyAccessAsync(studentId, session.ModuleId, ct);
        EnsureActive(session);
        var presentation = await GetCurrentPresentationAsync(session.Id, presentationId, ct);
        var snapshot = selection.ReadSnapshot(presentation);
        var clues = snapshot.Type == "EXPOSURE" ? snapshot.Keywords?.Select(x => x.Value).ToArray() : snapshot.Clues?.Select(x => x.Text).ToArray();
        if (snapshot.Type is not ("EXPOSURE" or "GUESS_CONCEPT") || clues is null)
            throw new ApiException(409, "hints_not_supported", "Esta atividade não possui pistas progressivas.");
        if (presentation.RevealedClueCount >= clues.Length)
            throw new ApiException(409, "all_hints_revealed", "Todas as pistas desta atividade já foram reveladas.");
        var text = clues[presentation.RevealedClueCount++];
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new RevealHintResponse(presentation.RevealedClueCount, clues.Length, text);
    }

    public async Task<ActivityAnswerResponse> SubmitAsync(Guid studentId, Guid sessionId, SubmitActivityAnswerRequest request, CancellationToken ct)
    {
        if (request.PresentationId == Guid.Empty) throw BadRequest("presentation_required", "A apresentação da atividade é obrigatória.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        var session = await LockOwnedSessionAsync(studentId, sessionId, ct);
        await EnsureStudyAccessAsync(studentId, session.ModuleId, ct);
        EnsureActive(session);
        var presentation = await GetCurrentPresentationAsync(session.Id, request.PresentationId, ct);
        var snapshot = selection.ReadSnapshot(presentation);
        if (!string.Equals(request.Type, snapshot.Type, StringComparison.OrdinalIgnoreCase))
            throw BadRequest("activity_type_mismatch", "O tipo enviado não corresponde à atividade apresentada.");

        var now = Now;
        var responseTime = Math.Max(0, (long)(now - presentation.StartedAtUtc).TotalMilliseconds);
        var wasCorrect = Correct(snapshot, presentation, request.Answer);
        var (attemptsUsed, hintsUsed) = GetEffort(snapshot, presentation, request);
var rating = snapshot.Type == "EXPOSURE" ? (FsrsRating?)null : performanceRatingService.Infer(wasCorrect, attemptsUsed, hintsUsed, responseTime);

        var attempt = new ActivityAttempt
        {
            StudentId = studentId,
            ConceptId = presentation.ConceptId,
            StudySessionId = session.Id,
            PresentationId = presentation.Id,
            ActivityType = presentation.ActivityType,
            RecognitionActivityId = snapshot.Type == "TRUE_FALSE" ? snapshot.ActivityId : null,
            FillBlankActivityId = snapshot.Type == "FILL_BLANK" ? snapshot.ActivityId : null,
            OrderingActivityId = snapshot.Type == "ORDERING" ? snapshot.ActivityId : null,
            WasCorrect = wasCorrect,
            StartedAtUtc = presentation.StartedAtUtc,
            AnsweredAtUtc = now,
            ResponseTimeMs = responseTime,
            AttemptsUsed = attemptsUsed,
            HintsUsed = hintsUsed,
            FsrsRating = rating,
            AnswerJson = request.Answer.GetRawText(),
            ActivitySnapshotJson = JsonSerializer.Serialize(new { version = snapshot.Version, activity = snapshot, submittedAnswer = request.Answer }, JsonOptions),
            CreatedAtUtc = now,
        };
        db.ActivityAttempts.Add(attempt);
        presentation.AnsweredAtUtc = now;
        if (snapshot.Type == "EXPOSURE")
            await progression.CompleteExposureAsync(studentId, presentation.ConceptId, ct);
        else
            await progression.RecordAnswerAsync(studentId, presentation.ConceptId, session.Id, wasCorrect, attemptsUsed, hintsUsed, responseTime, ct);

        session.CompletedActivities++;
        var state = await progression.GetStateAsync(studentId, presentation.ConceptId, ct);
        attempt.FsrsRating = snapshot.Type == "EXPOSURE" ? null : state.LastFsrsRating;
        SessionActivityPresentation? next = null;
        if (session.CompletedActivities >= 10)
            Complete(session, Now);
        else
            next = await selection.PresentNextAsync(session, ct);
        if (next is null && session.Status == StudySessionStatus.Active) Complete(session, Now);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        var correctAnswer = BuildCorrectAnswer(snapshot);
        return new ActivityAnswerResponse(wasCorrect, Feedback(snapshot, wasCorrect), state.LearningState.ToString().ToUpperInvariant(),
            session.CompletedActivities, session.TotalActivities, session.Status.ToString().ToUpperInvariant(),
            next is null ? null : selection.MapPublic(next), correctAnswer);
    }

    public async Task AbandonAsync(Guid studentId, Guid sessionId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        var session = await LockOwnedSessionAsync(studentId, sessionId, ct);
        await EnsureStudyAccessAsync(studentId, session.ModuleId, ct);
        EnsureActive(session);
        session.Status = StudySessionStatus.Abandoned;
        session.CompletedAtUtc = Now;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
private async Task<SessionActivityPresentation?> GetOrCreateCurrentAsync(StudySession session, CancellationToken ct)
    {
        if (session.Status != StudySessionStatus.Active) return null;
        var current = await db.SessionActivityPresentations.Where(x => x.StudySessionId == session.Id && x.AnsweredAtUtc == null)
            .OrderByDescending(x => x.SequenceNumber).FirstOrDefaultAsync(ct);
        if (current is not null) return current;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockSessionRowAsync(session.Id, ct);
        session = await db.StudySessions.SingleAsync(x => x.Id == session.Id, ct);
        current = await db.SessionActivityPresentations.Where(x => x.StudySessionId == session.Id && x.AnsweredAtUtc == null)
            .OrderByDescending(x => x.SequenceNumber).FirstOrDefaultAsync(ct);
        if (current is null && session.Status == StudySessionStatus.Active)
        {
            current = await selection.PresentNextAsync(session, ct);
            if (current is null) Complete(session, Now);
            await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);
        return current;
    }

    private async Task<SessionActivityPresentation> GetCurrentPresentationAsync(Guid sessionId, Guid presentationId, CancellationToken ct)
    {
        var current = await db.SessionActivityPresentations.Where(x => x.StudySessionId == sessionId && x.AnsweredAtUtc == null)
            .OrderByDescending(x => x.SequenceNumber).FirstOrDefaultAsync(ct);
        if (current is null) throw new ApiException(409, "no_current_activity", "A sessão não possui atividade atual.");
        if (current.Id != presentationId) throw new ApiException(409, "stale_activity", "Esta apresentação já foi respondida ou não é a atividade atual.");
        return current;
    }

    private async Task<StudySession> LockOwnedSessionAsync(Guid studentId, Guid sessionId, CancellationToken ct)
    {
        await LockSessionRowAsync(sessionId, ct);
        return await db.StudySessions.SingleOrDefaultAsync(x => x.Id == sessionId && x.StudentId == studentId, ct)
            ?? throw NotFound();
    }

    private async Task LockSessionRowAsync(Guid sessionId, CancellationToken ct)
    {
        await ExecuteScalarLockAsync("SELECT 1 FROM \"StudySessions\" WHERE \"Id\" = @id FOR UPDATE", "id", sessionId, ct);
    }

    private async Task AcquireStudentModuleLockAsync(Guid studentId, Guid moduleId, CancellationToken ct) =>
        await ExecuteScalarLockAsync("SELECT pg_advisory_xact_lock(hashtextextended(@key, 0))", "key", $"{studentId:N}:{moduleId:N}", ct);

    private async Task ExecuteScalarLockAsync(string commandText, string parameterName, object value, CancellationToken ct)
    {
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = db.Database.CurrentTransaction!.GetDbTransaction();
        command.CommandText = commandText;
        var parameter = command.CreateParameter();
        parameter.ParameterName = parameterName;
        parameter.Value = value;
        command.Parameters.Add(parameter);
        await command.ExecuteScalarAsync(ct);
    }

    private async Task<StudySession> GetOwnedSessionAsync(Guid studentId, Guid sessionId, CancellationToken ct) =>
        await db.StudySessions.SingleOrDefaultAsync(x => x.Id == sessionId && x.StudentId == studentId, ct) ?? throw NotFound();

    private async Task EnsureStudyAccessAsync(Guid studentId, Guid moduleId, CancellationToken ct)
    {
        var allowed = await db.Students.AnyAsync(s => s.Id == studentId && s.IsActive && s.IsActivated
            && s.Classroom.Status == ClassroomStatus.Active
            && s.Classroom.ClassroomModules.Any(cm => cm.ModuleId == moduleId && cm.Module.Status == ModuleStatus.Published), ct);
        if (!allowed) throw NotFound();
    }

    private static bool Correct(ActivitySnapshot s, SessionActivityPresentation p, JsonElement answer)
    {
        if (answer.ValueKind != JsonValueKind.Object)
            throw BadRequest("invalid_answer", "A resposta deve ser um objeto JSON válido.");
        try
        {
            return s.Type switch
            {
                "EXPOSURE" => IsExposureComplete(s, p, answer),
                "TRUE_FALSE" => IsTrueFalseCorrect(s, answer),
                "FILL_BLANK" => IsFillBlankCorrect(s, answer),
                "GUESS_CONCEPT" => string.Equals(answer.Deserialize<GuessConceptAnswer>(JsonOptions)?.Text?.Trim() ?? string.Empty, s.ConceptName.Trim(), StringComparison.OrdinalIgnoreCase),
                "ORDERING" => IsOrderingCorrect(s, answer),
                _ => throw BadRequest("unsupported_activity", "Tipo de atividade não suportado."),
            };
        }
        catch (JsonException) { throw BadRequest("invalid_answer", "A resposta possui formato inválido."); }
    }

    private static bool IsExposureComplete(ActivitySnapshot s, SessionActivityPresentation p, JsonElement answer)
    {
        var submitted = answer.Deserialize<ExposureAnswer>(JsonOptions);
        if (submitted?.Completed != true || p.RevealedClueCount != (s.Keywords?.Count ?? 0))
            throw BadRequest("exposure_not_completed", "Revele todas as palavras-chave antes de concluir a exposição.");
        return true;
    }

    private static bool IsTrueFalseCorrect(ActivitySnapshot s, JsonElement answer)
    {
        if (answer.ValueKind != JsonValueKind.Object || !answer.TryGetProperty("choice", out var choice) || choice.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            throw BadRequest("invalid_true_false_answer", "Informe true ou false como resposta.");
        return choice.GetBoolean() == s.IsCorrect;
    }

    private static bool IsFillBlankCorrect(ActivitySnapshot s, JsonElement answer)
    {
        var submitted = answer.Deserialize<FillBlankAnswerPayload>(JsonOptions)?.Answers;
        var slots = s.Slots ?? [];
        if (submitted is null || submitted.Count != slots.Count || submitted.Any(x => x is null || x.Text is null) || submitted.Select(x => x.SlotNumber).Distinct().Count() != submitted.Count || submitted.Any(x => !slots.Any(slot => slot.SlotNumber == x.SlotNumber)))
            throw BadRequest("invalid_fill_blank_answer", "Envie exatamente uma resposta para cada lacuna, sem extras ou duplicatas.");
        return slots.All(slot => string.Equals(submitted.Single(x => x.SlotNumber == slot.SlotNumber).Text.Trim(), slot.CorrectText.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsOrderingCorrect(ActivitySnapshot s, JsonElement answer)
    {
        var submitted = answer.Deserialize<OrderingAnswer>(JsonOptions)?.ItemIds;
        var expected = (s.Items ?? []).OrderBy(x => x.Position).Select(x => x.Id).ToArray();
        if (submitted is null || submitted.Count != expected.Length || submitted.Distinct().Count() != submitted.Count || submitted.Any(id => !expected.Contains(id)))
            throw BadRequest("invalid_ordering_answer", "A sequência deve conter cada item exatamente uma vez.");
        return submitted.SequenceEqual(expected);
    }

    private static (int Attempts, int Hints) GetEffort(ActivitySnapshot snapshot, SessionActivityPresentation presentation, SubmitActivityAnswerRequest request)
    {
        if (snapshot.Type is "TRUE_FALSE" or "FILL_BLANK" or "ORDERING" or "EXPOSURE") return (1, 0);
        var attempts = Math.Clamp(request.AttemptsUsed ?? 1, 1, 3);
        var hints = request.HintsUsed ?? 0;
        if (hints < 0 || hints > presentation.RevealedClueCount)
            throw BadRequest("invalid_hint_count", "A quantidade de pistas deve corresponder às pistas reveladas.");
        return (attempts, hints);
    }

    private static JsonElement? BuildCorrectAnswer(ActivitySnapshot s)
    {
        object? answer = s.Type switch
        {
            "EXPOSURE" => null,
            "TRUE_FALSE" => new { choice = s.IsCorrect, explanation = s.Explanation },
            "FILL_BLANK" => new { answers = (s.Slots ?? []).Select(x => new { slotNumber = x.SlotNumber, text = x.CorrectText }).ToArray() },
            "GUESS_CONCEPT" => new { text = s.ConceptName },
            "ORDERING" => new { itemIds = (s.Items ?? []).OrderBy(x => x.Position).Select(x => x.Id).ToArray() },
            _ => null,
        };
        if (answer is null) return null;
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(answer, JsonOptions));
        return doc.RootElement.Clone();
    }

    private static string Feedback(ActivitySnapshot s, bool correct) => s.Type == "TRUE_FALSE" && correct
        ? s.Explanation ?? "Resposta correta."
        : correct ? "Resposta correta." : "Resposta incorreta.";

    private async Task<StudySessionResponse> MapSessionAsync(StudySession session, CancellationToken ct)
    {
        var current = session.Status == StudySessionStatus.Active
            ? await db.SessionActivityPresentations.Where(x => x.StudySessionId == session.Id && x.AnsweredAtUtc == null).OrderByDescending(x => x.SequenceNumber).FirstOrDefaultAsync(ct)
            : null;
        return MapSession(session, current);
    }

    private StudySessionResponse MapSession(StudySession session, SessionActivityPresentation? activity) =>
        new(session.Id, session.Status.ToString().ToUpperInvariant(), session.TotalActivities, session.CompletedActivities,
            session.StartedAtUtc, session.CompletedAtUtc, activity is null ? null : selection.MapPublic(activity));

    private StartStudySessionResponse MapStart(StudySession session, SessionActivityPresentation? activity) =>
        new(session.Id, session.Status.ToString().ToUpperInvariant(), session.TotalActivities, session.CompletedActivities,
            activity is null ? null : selection.MapPublic(activity));

    private static void Complete(StudySession session, DateTime now)
    {
        session.Status = StudySessionStatus.Completed;
        session.CompletedAtUtc = session.CompletedAtUtc ?? now;
        session.TotalActivities = session.CompletedActivities;
    }

    private DateTime Now => timeProvider.GetUtcNow().UtcDateTime;
    private static void EnsureActive(StudySession session) { if (session.Status != StudySessionStatus.Active) throw new ApiException(409, "session_not_active", "A sessão não está ativa."); }
    private static ApiException NotFound() => new(404, "study_session_not_found", "A sessão ou módulo não foi encontrado.");
    private static ApiException BadRequest(string code, string message) => new(400, code, message);
}
