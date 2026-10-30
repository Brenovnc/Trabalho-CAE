using Microsoft.EntityFrameworkCore;
using StudyPlatform.Api.Data;
using StudyPlatform.Api.Domain.Enums;
using StudyPlatform.Api.DTOs.Learning;
using StudyPlatform.Api.Exceptions;

namespace StudyPlatform.Api.Services.Learning;

public sealed class StudentStudyCatalogService(ApplicationDbContext db, TimeProvider? timeProvider = null)
{
    public async Task<StudyPlatform.Api.Models.TeacherPhoto?> GetTeacherPhotoAsync(Guid studentId, Guid moduleId, CancellationToken ct)
    {
        var classroomId = await GetActiveClassroomIdAsync(studentId, ct);
        var teacherId = await db.ClassroomModules.AsNoTracking()
            .Where(link => link.ClassroomId == classroomId && link.ModuleId == moduleId && link.Module.Status == ModuleStatus.Published)
            .Select(link => (Guid?)link.Module.TeacherId).SingleOrDefaultAsync(ct)
            ?? throw new ApiException(404, "student_module_not_found", "O módulo não foi encontrado.");
        return await db.TeacherPhotos.AsNoTracking().SingleOrDefaultAsync(photo => photo.TeacherId == teacherId, ct);
    }
    public async Task<IReadOnlyList<StudentModuleResponse>> ListModulesAsync(Guid studentId, CancellationToken ct)
    {
        var classroomId = await GetActiveClassroomIdAsync(studentId, ct);
        var modules = await db.ClassroomModules.AsNoTracking()
            .Where(assignment => assignment.ClassroomId == classroomId && assignment.Module.Status == ModuleStatus.Published)
            .OrderBy(assignment => assignment.Module.Title)
            .Select(assignment => new { assignment.Module.Id, assignment.Module.Title, assignment.Module.Description, assignment.Module.Subject,
                assignment.Module.CrosswordEnabled, assignment.Module.CrosswordAllowAllWords, assignment.Module.AllowAdvanceReview,
                TeacherName = assignment.Module.Teacher.Name, TeacherInstitution = assignment.Module.Teacher.Institution,
                TeacherPhotoVersion = db.TeacherPhotos.Where(photo => photo.TeacherId == assignment.Module.TeacherId).Select(photo => (DateTime?)photo.UpdatedAtUtc).FirstOrDefault() })
            .ToListAsync(ct);
        var results = new List<StudentModuleResponse>(modules.Count);
        foreach (var module in modules)
            results.Add(await BuildResponseAsync(studentId, module.Id, module.Title, module.Description, module.Subject,
                module.CrosswordEnabled, module.CrosswordAllowAllWords, module.AllowAdvanceReview, module.TeacherName, module.TeacherInstitution, module.TeacherPhotoVersion?.Ticks, ct));
        return results;
    }

    public async Task<StudentModuleResponse> GetModuleAsync(Guid studentId, Guid moduleId, CancellationToken ct)
    {
        var classroomId = await GetActiveClassroomIdAsync(studentId, ct);
        var module = await db.ClassroomModules.AsNoTracking()
            .Where(assignment => assignment.ClassroomId == classroomId && assignment.ModuleId == moduleId
                && assignment.Module.Status == ModuleStatus.Published)
            .Select(assignment => new { assignment.Module.Id, assignment.Module.Title, assignment.Module.Description, assignment.Module.Subject,
                assignment.Module.CrosswordEnabled, assignment.Module.CrosswordAllowAllWords, assignment.Module.AllowAdvanceReview,
                TeacherName = assignment.Module.Teacher.Name, TeacherInstitution = assignment.Module.Teacher.Institution,
                TeacherPhotoVersion = db.TeacherPhotos.Where(photo => photo.TeacherId == assignment.Module.TeacherId).Select(photo => (DateTime?)photo.UpdatedAtUtc).FirstOrDefault() })
            .SingleOrDefaultAsync(ct)
            ?? throw new ApiException(404, "student_module_not_found", "O módulo não foi encontrado.");
        return await BuildResponseAsync(studentId, module.Id, module.Title, module.Description, module.Subject,
            module.CrosswordEnabled, module.CrosswordAllowAllWords, module.AllowAdvanceReview, module.TeacherName, module.TeacherInstitution, module.TeacherPhotoVersion?.Ticks, ct);
    }

    private async Task<StudentModuleResponse> BuildResponseAsync(Guid studentId, Guid moduleId, string title, string? description, string subject,
        bool crosswordEnabled, bool crosswordAllowAllWords, bool allowAdvanceReview, string teacherName, string? teacherInstitution, long? teacherPhotoVersion, CancellationToken ct)
    {
        var concepts = await db.Concepts.AsNoTracking().Where(concept => concept.ModuleId == moduleId && concept.IsActive)
            .OrderBy(concept => concept.Name).Select(concept => new StudentModuleConceptOption(concept.Id, concept.Name)).ToListAsync(ct);
        var conceptIds = concepts.Select(concept => concept.Id).ToArray();
        var states = conceptIds.Length == 0 ? [] : await db.StudentConceptStates.AsNoTracking()
            .Where(state => state.StudentId == studentId && conceptIds.Contains(state.ConceptId))
            .Select(state => new { state.ConceptId, state.LearningState, state.DueAtUtc }).ToListAsync(ct);
        var activeConceptCount = concepts.Count;
        var mastered = states.Count(state => state.LearningState == LearningState.Mastered);
        var learning = states.Count(state => state.LearningState is LearningState.Exposure or LearningState.Recognition or LearningState.GuidedRecall or LearningState.FreeRecall);
        var notStarted = activeConceptCount - mastered - learning;
        var now = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
        var pending = states.Count(state => state.LearningState != LearningState.New && state.DueAtUtc is not null && state.DueAtUtc <= now);
        var nextReview = states.Where(state => state.LearningState != LearningState.New && state.DueAtUtc > now)
            .Select(state => state.DueAtUtc).Min();
        var activeSession = await db.StudySessions.AsNoTracking()
            .Where(session => session.StudentId == studentId && session.ModuleId == moduleId && session.Status == StudySessionStatus.Active)
            .Select(session => new { session.Id, session.Mode }).FirstOrDefaultAsync(ct);
        var progress = activeConceptCount == 0 ? 0 : (int)Math.Round((double)mastered * 100 / activeConceptCount, MidpointRounding.AwayFromZero);
        return new StudentModuleResponse(moduleId, title, description, subject, activeSession?.Id,
            activeConceptCount, mastered, learning, notStarted, pending, progress, nextReview,
            activeSession?.Mode == StudySessionMode.FreePractice ? "FREE_PRACTICE" : activeSession is null ? null : "NORMAL", concepts,
            crosswordEnabled, crosswordEnabled && crosswordAllowAllWords, allowAdvanceReview, null, teacherName, teacherInstitution, teacherPhotoVersion);
    }

    private async Task<Guid> GetActiveClassroomIdAsync(Guid studentId, CancellationToken ct) =>
        await db.Students.AsNoTracking()
            .Where(student => student.Id == studentId && student.IsActive && student.IsActivated && student.Classroom.Status == ClassroomStatus.Active)
            .Select(student => (Guid?)student.ClassroomId).SingleOrDefaultAsync(ct)
        ?? throw new ApiException(404, "student_module_not_found", "O módulo não foi encontrado.");
}
