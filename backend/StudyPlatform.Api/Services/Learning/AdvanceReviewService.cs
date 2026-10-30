using Microsoft.EntityFrameworkCore;
using StudyPlatform.Api.Data;
using StudyPlatform.Api.Domain.Enums;
using StudyPlatform.Api.DTOs.Learning;
using StudyPlatform.Api.Exceptions;

namespace StudyPlatform.Api.Services.Learning;

public sealed class AdvanceReviewService(
    ApplicationDbContext db,
    StudentStudyCatalogService catalog,
    TimeProvider timeProvider,
    ILogger<AdvanceReviewService> logger)
{
    public async Task<StudentModuleResponse> AdvanceAsync(Guid studentId, Guid moduleId, CancellationToken ct)
    {
        var module = await catalog.GetModuleAsync(studentId, moduleId, ct);
        if (!module.AllowAdvanceReview)
            throw new ApiException(403, "advance_review_disabled", "O professor não permitiu antecipar revisões neste módulo.");
        if (await db.StudySessions.AnyAsync(session => session.StudentId == studentId && session.ModuleId == moduleId
                && session.Status == StudySessionStatus.Active, ct))
            throw new ApiException(409, "active_study_session", "Conclua ou abandone a sessão ativa antes de antecipar revisões.");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var states = await db.StudentConceptStates
            .Where(state => state.StudentId == studentId && state.Concept.ModuleId == moduleId && state.Concept.IsActive
                && state.LearningState != LearningState.New && state.DueAtUtc != null && state.DueAtUtc > now)
            .OrderBy(state => state.DueAtUtc)
            .Take(10)
            .ToListAsync(ct);
        foreach (var state in states) state.DueAtUtc = now;
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Advanced {ReviewCount} scheduled reviews for a module.", states.Count);
        return (await catalog.GetModuleAsync(studentId, moduleId, ct)) with { AdvanceReviewPerformed = states.Count > 0 };
    }
}
