using Microsoft.EntityFrameworkCore;
using StudyPlatform.Api.Data;
using StudyPlatform.Api.Domain.Enums;
using StudyPlatform.Api.DTOs.Progress;
using StudyPlatform.Api.Exceptions;
using StudyPlatform.Api.Models.Activities;

namespace StudyPlatform.Api.Services.Progress;

public sealed class TeacherStatisticsService(ApplicationDbContext db, TimeProvider clock)
{
    private static readonly ActivityType[] ScoredTypes =
        [ActivityType.TrueFalse, ActivityType.FillBlank, ActivityType.GuessConcept, ActivityType.Ordering];

    public async Task<TeacherDashboardStatisticsResponse> GetDashboardAsync(Guid teacherId, int days, CancellationToken ct)
    {
        ValidateDays(days);
        var classrooms = await db.Classrooms.AsNoTracking()
            .Where(item => item.TeacherId == teacherId && item.Status == ClassroomStatus.Active)
            .Select(item => new { item.Id, item.Name }).ToListAsync(ct);
        var scopes = new List<ClassroomData>();
        foreach (var classroom in classrooms)
            scopes.Add(await GetClassroomDataAsync(classroom.Id, null, true, ct));
        var students = scopes.SelectMany(scope => scope.Students).DistinctBy(student => student.Id).ToArray();
        var concepts = scopes.SelectMany(scope => scope.Concepts).DistinctBy(concept => concept.Id).ToArray();
        var states = await GetStatesAsync(students.Select(item => item.Id).ToArray(), concepts.Select(item => item.Id).ToArray(), ct);
        var distribution = Distribution(students, concepts, states);
        var modules = scopes.SelectMany(scope => scope.ModuleIds).Distinct().ToArray();
        var attempts = await GetAttemptsAsync(students.Select(item => item.Id).ToArray(), modules, days, ct);
        var comparison = scopes.Select(scope =>
        {
            var progress = scope.Students.Select(student => Percent(
                scope.Concepts.Count(concept => states.GetValueOrDefault((student.Id, concept.Id)) == LearningState.Mastered),
                scope.Concepts.Count)).ToArray();
            return new ClassroomComparisonStatistics(scope.ClassroomId, scope.ClassroomName,
                scope.Students.Count, progress.Length == 0 ? 0 : progress.Average());
        }).ToArray();
        return new TeacherDashboardStatisticsResponse(distribution, Daily(attempts, days), comparison);
    }

    public async Task<ClassroomStatisticsResponse> GetClassroomAsync(Guid teacherId, Guid classroomId, int days, Guid? moduleId, CancellationToken ct)
    {
        ValidateDays(days);
        var owner = await db.Classrooms.AnyAsync(item => item.Id == classroomId && item.TeacherId == teacherId, ct);
        if (!owner) throw NotFound("A turma não foi encontrada.");
        var scope = await GetClassroomDataAsync(classroomId, moduleId, false, ct);
        var states = await GetStatesAsync(scope.Students.Select(item => item.Id).ToArray(), scope.Concepts.Select(item => item.Id).ToArray(), ct);
        var attempts = await GetAttemptsAsync(scope.Students.Select(item => item.Id).ToArray(), scope.ModuleIds, days, ct);
        return new ClassroomStatisticsResponse(Distribution(scope.Students, scope.Concepts, states),
            Daily(attempts, days), TypeStatistics(attempts),
            ErrorConcepts(attempts), ProgressBands(scope.Students, scope.Concepts, states));
    }

    public async Task<StudentStatisticsResponse> GetStudentAsync(Guid teacherId, Guid classroomId, Guid studentId,
        int days, Guid? moduleId, CancellationToken ct)
    {
        ValidateDays(days);
        var student = await db.Students.AsNoTracking()
            .Where(item => item.Id == studentId && item.ClassroomId == classroomId && item.Classroom.TeacherId == teacherId)
            .Select(item => new StudentInfo(item.Id)).SingleOrDefaultAsync(ct)
            ?? throw NotFound("O aluno não foi encontrado nesta turma.");
        var moduleIds = await GetPublishedModuleIdsAsync(classroomId, moduleId, ct);
        var concepts = moduleIds.Length == 0 ? [] : await db.Concepts.AsNoTracking()
            .Where(concept => concept.IsActive && moduleIds.Contains(concept.ModuleId))
            .Select(concept => new ConceptInfo(concept.Id, concept.Name)).ToListAsync(ct);
        var states = await GetStatesAsync([student.Id], concepts.Select(item => item.Id).ToArray(), ct);
        var attempts = await GetAttemptsAsync([student.Id], moduleIds, days, ct);
        return new StudentStatisticsResponse(Distribution([student], concepts, states),
            Daily(attempts, days), TypeStatistics(attempts), ErrorConcepts(attempts));
    }

    private async Task<ClassroomData> GetClassroomDataAsync(Guid classroomId, Guid? requestedModuleId, bool activeStudentsOnly, CancellationToken ct)
    {
        var classroom = await db.Classrooms.AsNoTracking().Where(item => item.Id == classroomId)
            .Select(item => new { item.Id, item.Name }).SingleOrDefaultAsync(ct)
            ?? throw NotFound("A turma não foi encontrada.");
        var moduleIds = await GetPublishedModuleIdsAsync(classroomId, requestedModuleId, ct);
        var studentsQuery = db.Students.AsNoTracking().Where(student => student.ClassroomId == classroomId);
        if (activeStudentsOnly) studentsQuery = studentsQuery.Where(student => student.IsActive);
        var students = await studentsQuery.Select(student => new StudentInfo(student.Id)).ToListAsync(ct);
        var concepts = moduleIds.Length == 0 ? [] : await db.Concepts.AsNoTracking()
            .Where(concept => concept.IsActive && moduleIds.Contains(concept.ModuleId))
            .Select(concept => new ConceptInfo(concept.Id, concept.Name)).ToListAsync(ct);
        return new ClassroomData(classroom.Id, classroom.Name, moduleIds, students, concepts);
    }

    private async Task<Guid[]> GetPublishedModuleIdsAsync(Guid classroomId, Guid? requestedModuleId, CancellationToken ct)
    {
        var query = db.ClassroomModules.AsNoTracking().Where(link => link.ClassroomId == classroomId
            && link.Module.Status == ModuleStatus.Published);
        if (requestedModuleId.HasValue) query = query.Where(link => link.ModuleId == requestedModuleId.Value);
        var moduleIds = await query.Select(link => link.ModuleId).ToArrayAsync(ct);
        if (requestedModuleId.HasValue && moduleIds.Length == 0)
            throw NotFound("O módulo não está publicado e associado a esta turma.");
        return moduleIds;
    }

    private async Task<Dictionary<(Guid StudentId, Guid ConceptId), LearningState>> GetStatesAsync(Guid[] studentIds, Guid[] conceptIds, CancellationToken ct)
    {
        if (studentIds.Length == 0 || conceptIds.Length == 0) return [];
        var rows = await db.StudentConceptStates.AsNoTracking()
            .Where(state => studentIds.Contains(state.StudentId) && conceptIds.Contains(state.ConceptId))
            .Select(state => new { state.StudentId, state.ConceptId, state.LearningState }).ToListAsync(ct);
        return rows.ToDictionary(row => (row.StudentId, row.ConceptId), row => row.LearningState);
    }

    private async Task<List<AttemptInfo>> GetAttemptsAsync(Guid[] studentIds, Guid[] moduleIds, int days, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var start = today.AddDays(-(days - 1)).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var end = today.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        if (studentIds.Length == 0 || moduleIds.Length == 0) return [];
        return await db.ActivityAttempts.AsNoTracking()
            .Where(attempt => studentIds.Contains(attempt.StudentId) && moduleIds.Contains(attempt.Concept.ModuleId)
                && attempt.StudySession.Mode == StudySessionMode.Normal
                && attempt.AnsweredAtUtc >= start && attempt.AnsweredAtUtc < end)
            .Select(attempt => new AttemptInfo(attempt.ConceptId, attempt.Concept.Name, attempt.ActivityType,
                attempt.AnsweredAtUtc, attempt.WasCorrect))
            .ToListAsync(ct);
    }

    private IReadOnlyList<DailyLearningStatistics> Daily(List<AttemptInfo> attempts, int days)
    {
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        return Enumerable.Range(0, days).Select(offset =>
        {
            var date = today.AddDays(offset - days + 1);
            var daily = attempts.Where(item => DateOnly.FromDateTime(item.AnsweredAtUtc) == date).ToArray();
            var scored = daily.Where(item => ScoredTypes.Contains(item.Type)).ToArray();
            return new DailyLearningStatistics(date, daily.Length,
                scored.Length == 0 ? null : Percent(scored.Count(item => item.WasCorrect), scored.Length));
        }).ToArray();
    }

    private static IReadOnlyList<ActivityTypeStatistics> TypeStatistics(List<AttemptInfo> attempts) =>
        ScoredTypes.Select(type =>
        {
            var matching = attempts.Where(item => item.Type == type).ToArray();
            var label = type switch
            {
                ActivityType.TrueFalse => "TRUE_FALSE",
                ActivityType.FillBlank => "FILL_BLANK",
                ActivityType.GuessConcept => "GUESS_CONCEPT",
                ActivityType.Ordering => "ORDERING",
                _ => type.ToString().ToUpperInvariant(),
            };
            return new ActivityTypeStatistics(label, matching.Length, matching.Count(item => item.WasCorrect),
                matching.Length == 0 ? null : Percent(matching.Count(item => item.WasCorrect), matching.Length));
        }).ToArray();

    private static IReadOnlyList<ErrorConceptStatistics> ErrorConcepts(List<AttemptInfo> attempts) =>
        attempts.Where(item => ScoredTypes.Contains(item.Type)).GroupBy(item => new { item.ConceptId, item.Name })
            .Select(group => new { group.Key.ConceptId, group.Key.Name, Attempts = group.Count(), Errors = group.Count(item => !item.WasCorrect) })
            .Where(item => item.Attempts >= 3)
            .OrderByDescending(item => item.Errors / (double)item.Attempts).ThenByDescending(item => item.Attempts)
            .Take(5).Select(item => new ErrorConceptStatistics(item.ConceptId, item.Name, item.Attempts,
                Percent(item.Errors, item.Attempts))).ToArray();

    private static ConceptDistributionStatistics Distribution(IReadOnlyList<StudentInfo> students, IReadOnlyList<ConceptInfo> concepts,
        Dictionary<(Guid StudentId, Guid ConceptId), LearningState> states)
    {
        var mastered = 0;
        var learning = 0;
        var notStarted = 0;
        foreach (var student in students)
            foreach (var concept in concepts)
            {
                var state = states.GetValueOrDefault((student.Id, concept.Id), LearningState.New);
                if (state == LearningState.Mastered) mastered++;
                else if (state is LearningState.Exposure or LearningState.Recognition or LearningState.GuidedRecall or LearningState.FreeRecall) learning++;
                else notStarted++;
            }
        return new ConceptDistributionStatistics(mastered, learning, notStarted);
    }

    private static IReadOnlyList<StudentProgressBandStatistics> ProgressBands(IReadOnlyList<StudentInfo> students,
        IReadOnlyList<ConceptInfo> concepts, Dictionary<(Guid StudentId, Guid ConceptId), LearningState> states)
    {
        var bands = new[] { 0, 0, 0, 0 };
        foreach (var student in students)
        {
            var progress = Percent(concepts.Count(concept => states.GetValueOrDefault((student.Id, concept.Id)) == LearningState.Mastered), concepts.Count);
            bands[progress <= 25 ? 0 : progress <= 50 ? 1 : progress <= 75 ? 2 : 3]++;
        }
        return new[] { "0–25%", "26–50%", "51–75%", "76–100%" }.Select((range, index) =>
            new StudentProgressBandStatistics(range, bands[index])).ToArray();
    }

    private static int Percent(int value, int total) => total == 0 ? 0 : (int)Math.Round((double)value * 100 / total, MidpointRounding.AwayFromZero);
    private static void ValidateDays(int days)
    {
        if (days is not (7 or 30 or 90)) throw new ApiException(400, "invalid_statistics_days", "O período deve ser 7, 30 ou 90 dias.");
    }
    private static ApiException NotFound(string message) => new(404, "statistics_scope_not_found", message);
    private sealed record StudentInfo(Guid Id);
    private sealed record ConceptInfo(Guid Id, string Name);
    private sealed record AttemptInfo(Guid ConceptId, string Name, ActivityType Type, DateTime AnsweredAtUtc, bool WasCorrect);
    private sealed record ClassroomData(Guid ClassroomId, string ClassroomName, Guid[] ModuleIds, List<StudentInfo> Students, List<ConceptInfo> Concepts);
}
