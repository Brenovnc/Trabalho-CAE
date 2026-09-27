using Microsoft.EntityFrameworkCore;
using StudyPlatform.Api.Data;
using StudyPlatform.Api.Domain.Enums;
using StudyPlatform.Api.Models;

namespace StudyPlatform.Api.Services.Learning;

public sealed class LearningProgressionService(
    ApplicationDbContext db,
    FsrsService fsrs,
    PerformanceRatingService performance,
    ConceptEligibilityService eligibility,
    TimeProvider timeProvider)
{
    public async Task<StudentConceptState> GetStateAsync(Guid studentId, Guid conceptId, CancellationToken cancellationToken = default)
    {
        var existing = await db.StudentConceptStates.AsNoTracking()
            .SingleOrDefaultAsync(x => x.StudentId == studentId && x.ConceptId == conceptId, cancellationToken);
        return existing ?? new StudentConceptState { StudentId = studentId, ConceptId = conceptId };
    }

    public async Task<StudentConceptState> MarkPresentedAsync(Guid studentId, Guid conceptId, CancellationToken cancellationToken = default)
    {
        var initialState = await GetStateAsync(studentId, conceptId, cancellationToken);
        if (initialState.LearningState == LearningState.New &&
            !await eligibility.CanIntroduceAsync(studentId, conceptId, cancellationToken))
            throw new InvalidOperationException("Concept is not eligible for introduction.");

        var state = await GetOrCreateTrackedStateAsync(studentId, conceptId, cancellationToken);
        if (state.LearningState == LearningState.New)
        {
            state.LearningState = LearningState.Exposure;
            state.LastAttemptAtUtc = UtcNow();
            await db.SaveChangesAsync(cancellationToken);
        }
        return state;
    }

    public async Task<StudentConceptState> CompleteExposureAsync(Guid studentId, Guid conceptId, CancellationToken cancellationToken = default)
    {
        var current = await GetStateAsync(studentId, conceptId, cancellationToken);
        if (current.LearningState != LearningState.Exposure)
            throw new InvalidOperationException("Exposure can only be completed from EXPOSURE.");

        var state = await GetOrCreateTrackedStateAsync(studentId, conceptId, cancellationToken);
        state.LearningState = LearningState.Recognition;
        state.LastAttemptAtUtc = UtcNow();
        await db.SaveChangesAsync(cancellationToken);
        return state;
    }

    public async Task<StudentConceptState> RecordAnswerAsync(
        Guid studentId, Guid conceptId, Guid sessionId, bool wasCorrect, int attemptsUsed, int hintsUsed,
        long responseTimeMs, CancellationToken cancellationToken = default)
    {
        var now = UtcNow();
        var conceptModuleId = await db.Concepts.Where(x => x.Id == conceptId)
            .Select(x => (Guid?)x.ModuleId).SingleOrDefaultAsync(cancellationToken);
        if (conceptModuleId is null) throw new KeyNotFoundException("Concept not found.");

        var validSession = await db.StudySessions.AnyAsync(
            x => x.Id == sessionId && x.StudentId == studentId && x.ModuleId == conceptModuleId,
            cancellationToken);
        if (!validSession)
            throw new InvalidOperationException("The session does not belong to this student and module.");

        var current = await GetStateAsync(studentId, conceptId, cancellationToken);
        if (current.LearningState is LearningState.New or LearningState.Exposure)
            throw new InvalidOperationException("The concept is not in a response state.");

        var state = await GetOrCreateTrackedStateAsync(studentId, conceptId, cancellationToken);
        var rating = performance.Infer(wasCorrect, attemptsUsed, hintsUsed, responseTimeMs);
        state.LastAttemptAtUtc = now;
        if (wasCorrect) state.LastSuccessAtUtc = now;
        else state.LastFailureAtUtc = now;

        switch (state.LearningState)
        {
            case LearningState.Recognition:
                if (wasCorrect) state.LearningState = LearningState.GuidedRecall;
                break;
            case LearningState.GuidedRecall:
                state.LearningState = wasCorrect ? LearningState.FreeRecall : LearningState.Recognition;
                break;
            case LearningState.FreeRecall:
                if (!wasCorrect)
                {
                    state.LearningState = LearningState.GuidedRecall;
                    ResetFreeRecallSuccesses(state);
                    break;
                }

                var priorDueAt = state.DueAtUtc;
                var differentSession = state.LastFreeRecallSuccessSessionId != sessionId;
                var scheduledReviewWasDue = priorDueAt is not null && Utc(priorDueAt.Value) <= now;
                if (state.FreeRecallSuccessCount == 0)
                {
                    state.FreeRecallSuccessCount = 1;
                    state.LastFreeRecallSuccessSessionId = sessionId;
                    state.LastFreeRecallSuccessAtUtc = now;
                }
                else if (state.FreeRecallSuccessCount == 1 && differentSession && scheduledReviewWasDue)
                {
                    state.FreeRecallSuccessCount = 2;
                    state.LearningState = LearningState.Mastered;
                    state.LastFreeRecallSuccessSessionId = sessionId;
                    state.LastFreeRecallSuccessAtUtc = now;
                }
                break;
            case LearningState.Mastered:
                if (!wasCorrect)
                {
                    state.LearningState = LearningState.FreeRecall;
                    ResetFreeRecallSuccesses(state);
                }
                break;
            default:
                throw new InvalidOperationException("Unsupported learning transition.");
        }

        var schedule = fsrs.Schedule(state, rating, now);
        state.FsrsState = schedule.State;
        state.Difficulty = schedule.Difficulty;
        state.Stability = schedule.Stability;
        state.DueAtUtc = schedule.DueAtUtc;
        state.LastReviewAtUtc = schedule.LastReviewAtUtc;
        state.ElapsedDays = schedule.ElapsedDays;
        state.ScheduledDays = schedule.ScheduledDays;
        state.Repetitions = schedule.Repetitions;
        state.Lapses = schedule.Lapses;
        state.LastFsrsRating = rating;
        await db.SaveChangesAsync(cancellationToken);
        return state;
    }

    private async Task<StudentConceptState> GetOrCreateTrackedStateAsync(Guid studentId, Guid conceptId, CancellationToken cancellationToken)
    {
        var state = await db.StudentConceptStates.SingleOrDefaultAsync(
            x => x.StudentId == studentId && x.ConceptId == conceptId, cancellationToken);
        if (state is not null) return state;

        var exists = await db.Students.AnyAsync(x => x.Id == studentId && x.IsActive, cancellationToken)
            && await db.Concepts.AnyAsync(x => x.Id == conceptId, cancellationToken);
        if (!exists) throw new KeyNotFoundException("Active student or concept not found.");

        state = new StudentConceptState { StudentId = studentId, ConceptId = conceptId };
        db.StudentConceptStates.Add(state);
        return state;
    }

    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;
    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static void ResetFreeRecallSuccesses(StudentConceptState state)
    {
        state.FreeRecallSuccessCount = 0;
        state.LastFreeRecallSuccessSessionId = null;
    }
}
