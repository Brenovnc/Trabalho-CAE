using Microsoft.EntityFrameworkCore;
using StudyPlatform.Api.Data;
using StudyPlatform.Api.Domain.Enums;

namespace StudyPlatform.Api.Services.Learning;

public sealed class ConceptEligibilityService(ApplicationDbContext db, TimeProvider timeProvider)
{
    public async Task<bool> CanIntroduceAsync(Guid studentId, Guid conceptId, CancellationToken cancellationToken = default)
    {
        var concept = await db.Concepts.AsNoTracking()
            .Where(x => x.Id == conceptId && x.IsActive && x.Module.Status == ModuleStatus.Published
                && x.Module.ClassroomModules.Any(cm => cm.Classroom.Status == ClassroomStatus.Active && cm.Classroom.Students.Any(s => s.Id == studentId && s.IsActive)))
            .Select(x => new { Prerequisites = x.Prerequisites.Select(p => p.PrerequisiteConceptId).ToList() })
            .SingleOrDefaultAsync(cancellationToken);
        if (concept is null) return false;
        var existingState = await db.StudentConceptStates.AsNoTracking()
            .Where(x => x.StudentId == studentId && x.ConceptId == conceptId)
            .Select(x => (LearningState?)x.LearningState)
            .SingleOrDefaultAsync(cancellationToken);
        if (existingState is not null && existingState != LearningState.New) return false;
        if (concept.Prerequisites.Count == 0) return true;
        var mastered = await db.StudentConceptStates.AsNoTracking()
            .Where(x => x.StudentId == studentId && concept.Prerequisites.Contains(x.ConceptId) && x.LearningState == LearningState.Mastered)
            .Select(x => x.ConceptId)
            .ToListAsync(cancellationToken);
        return mastered.Count == concept.Prerequisites.Distinct().Count();
    }

    public async Task<LearningState> GetLearningStateAsync(Guid studentId, Guid conceptId, CancellationToken cancellationToken = default)
    {
        return await db.StudentConceptStates.AsNoTracking()
            .Where(x => x.StudentId == studentId && x.ConceptId == conceptId)
            .Select(x => x.LearningState)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> IsDueAsync(Guid studentId, Guid conceptId, CancellationToken cancellationToken = default)
    {
        var dueAt = await db.StudentConceptStates.AsNoTracking()
            .Where(x => x.StudentId == studentId && x.ConceptId == conceptId && x.LearningState != LearningState.New && x.LearningState != LearningState.Exposure)
            .Select(x => x.DueAtUtc)
            .SingleOrDefaultAsync(cancellationToken);
        return dueAt is not null && DateTime.SpecifyKind(dueAt.Value, DateTimeKind.Utc) <= timeProvider.GetUtcNow().UtcDateTime;
    }
}
