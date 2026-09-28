using Microsoft.EntityFrameworkCore;
using StudyPlatform.Api.Data;
using StudyPlatform.Api.Domain.Enums;
using StudyPlatform.Api.DTOs.Progress;
using StudyPlatform.Api.Exceptions;

namespace StudyPlatform.Api.Services.Progress;

public sealed class TeacherProgressService(ApplicationDbContext db, TimeProvider timeProvider)
{
    public async Task<TeacherDashboardResponse> GetDashboardAsync(Guid teacherId, CancellationToken ct)
    {
        var modules = await db.Modules.AsNoTracking().CountAsync(module => module.TeacherId == teacherId && module.Status != ModuleStatus.Archived, ct);
        var activeClassrooms = await db.Classrooms.AsNoTracking().CountAsync(classroom => classroom.TeacherId == teacherId && classroom.Status == ClassroomStatus.Active, ct);
        var activeStudents = await db.Students.AsNoTracking().CountAsync(student => student.IsActive && student.Classroom.TeacherId == teacherId, ct);
        return new(modules, activeClassrooms, activeStudents);
    }

    public async Task<ClassroomProgressResponse> GetClassroomProgressAsync(Guid teacherId, Guid classroomId, CancellationToken ct)
    {
        var classroom = await db.Classrooms.AsNoTracking().Where(item => item.Id == classroomId && item.TeacherId == teacherId)
            .Select(item => new { item.Id, item.Name }).SingleOrDefaultAsync(ct)
            ?? throw NotFound("A turma não foi encontrada.");
        var students = await db.Students.AsNoTracking().Where(student => student.ClassroomId == classroomId)
            .OrderBy(student => student.EnrollmentNumber)
            .Select(student => new StudentRow(student.Id, student.EnrollmentNumber, student.Name, student.IsActive, student.LastAccessAtUtc)).ToListAsync(ct);
        var conceptRows = await db.Concepts.AsNoTracking()
            .Where(concept => concept.IsActive && concept.Module.Status == ModuleStatus.Published &&
                db.ClassroomModules.Any(link => link.ClassroomId == classroomId && link.ModuleId == concept.ModuleId))
            .OrderBy(concept => concept.Module.Title).ThenBy(concept => concept.Id)
            .Select(concept => new { concept.Id, concept.ModuleId, ModuleTitle = concept.Module.Title, concept.Module.Subject })
            .ToListAsync(ct);
        var concepts = conceptRows.Select(concept => new ConceptRow(concept.Id, concept.ModuleId, concept.ModuleTitle, concept.Subject)).ToArray();
        var totals = await GetStateTotalsAsync(students.Select(student => student.Id).ToArray(), concepts.Select(concept => concept.ConceptId).ToArray(), ct);
        var totalConcepts = concepts.Length;
        var rows = students.Select(student =>
        {
            totals.TryGetValue(student.Id, out var total);
            var mastered = total?.Mastered ?? 0;
            var learning = total?.Learning ?? 0;
            return new ClassroomStudentProgressResponse(student.Id, student.EnrollmentNumber, student.Name, student.IsActive,
                mastered, learning, totalConcepts - mastered - learning, total?.PendingReviews ?? 0, Percent(mastered, totalConcepts), student.LastAccessAtUtc);
        }).ToArray();
        return new(classroom.Id, classroom.Name, totalConcepts, rows);
    }

    public async Task<StudentProgressResponse> GetStudentProgressAsync(Guid teacherId, Guid classroomId, Guid studentId, CancellationToken ct)
    {
        var student = await db.Students.AsNoTracking()
            .Where(item => item.Id == studentId && item.ClassroomId == classroomId && item.Classroom.TeacherId == teacherId)
            .Select(item => new StudentRow(item.Id, item.EnrollmentNumber, item.Name, item.IsActive, item.LastAccessAtUtc)).SingleOrDefaultAsync(ct)
            ?? throw NotFound("O aluno não foi encontrado nesta turma.");
        var modules = await db.ClassroomModules.AsNoTracking()
            .Where(link => link.ClassroomId == classroomId && link.Module.Status == ModuleStatus.Published)
            .OrderBy(link => link.Module.Title).Select(link => new ModuleRow(link.ModuleId, link.Module.Title, link.Module.Subject)).ToListAsync(ct);
        var moduleIds = modules.Select(module => module.ModuleId).ToArray();
        var concepts = moduleIds.Length == 0 ? [] : await db.Concepts.AsNoTracking()
            .Where(concept => moduleIds.Contains(concept.ModuleId) && concept.IsActive)
            .Select(concept => new ConceptRow(concept.Id, concept.ModuleId, string.Empty, string.Empty)).ToListAsync(ct);
        var conceptIds = concepts.Select(concept => concept.ConceptId).ToArray();
        var states = conceptIds.Length == 0 ? [] : await db.StudentConceptStates.AsNoTracking()
            .Where(state => state.StudentId == studentId && conceptIds.Contains(state.ConceptId))
            .Select(state => new { state.ConceptId, state.LearningState, state.DueAtUtc }).ToListAsync(ct);
        var byConcept = states.ToDictionary(state => state.ConceptId);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var moduleProgress = modules.Select(module =>
        {
            var moduleConcepts = concepts.Where(concept => concept.ModuleId == module.ModuleId).ToArray();
            var moduleStates = moduleConcepts.Select(concept => byConcept.GetValueOrDefault(concept.ConceptId)).Where(state => state is not null).ToArray();
            var mastered = moduleStates.Count(state => state!.LearningState == LearningState.Mastered);
            var learning = moduleStates.Count(state => IsLearning(state!.LearningState));
            var pending = moduleStates.Count(state => state!.LearningState != LearningState.New && state.DueAtUtc is not null && state.DueAtUtc <= now);
            return new StudentModuleProgressResponse(module.ModuleId, module.Title, module.Subject, moduleConcepts.Length,
                mastered, learning, moduleConcepts.Length - mastered - learning, pending, Percent(mastered, moduleConcepts.Length));
        }).ToArray();
        return new(student.Id, student.EnrollmentNumber, student.Name, student.IsActive, moduleProgress);
    }

    private async Task<Dictionary<Guid, StateTotals>> GetStateTotalsAsync(Guid[] studentIds, Guid[] conceptIds, CancellationToken ct)
    {
        if (studentIds.Length == 0 || conceptIds.Length == 0) return [];
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var totals = await db.StudentConceptStates.AsNoTracking().Where(state => studentIds.Contains(state.StudentId) && conceptIds.Contains(state.ConceptId))
            .GroupBy(state => state.StudentId)
            .Select(group => new StateTotals(group.Key,
                group.Count(state => state.LearningState == LearningState.Mastered),
                group.Count(state => state.LearningState == LearningState.Exposure || state.LearningState == LearningState.Recognition ||
                    state.LearningState == LearningState.GuidedRecall || state.LearningState == LearningState.FreeRecall),
                group.Count(state => state.LearningState != LearningState.New && state.DueAtUtc != null && state.DueAtUtc <= now)))
            .ToListAsync(ct);
        return totals.ToDictionary(total => total.StudentId);
    }

    private static bool IsLearning(LearningState state) => state is LearningState.Exposure or LearningState.Recognition or LearningState.GuidedRecall or LearningState.FreeRecall;
    private static int Percent(int mastered, int activeConcepts) => activeConcepts == 0 ? 0 : (int)Math.Round((double)mastered * 100 / activeConcepts, MidpointRounding.AwayFromZero);
    private static ApiException NotFound(string message) => new(404, "classroom_or_student_not_found", message);
    private sealed record StudentRow(Guid Id, string EnrollmentNumber, string? Name, bool IsActive, DateTime? LastAccessAtUtc);
    private sealed record ModuleRow(Guid ModuleId, string Title, string Subject);
    private sealed record ConceptRow(Guid ConceptId, Guid ModuleId, string ModuleTitle, string Subject);
    private sealed record StateTotals(Guid StudentId, int Mastered, int Learning, int PendingReviews);
}
