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

    public async Task<TeacherDashboardStatisticsResponse> GetDashboardAsync(Guid teacherId, int days, string? from, string? to, Guid? classroomId, Guid? moduleId, string? activityType, CancellationToken ct)
    {
        var range = ParseRange(days, from, to);
        var filterType = ParseActivityType(activityType);
        var classroomsQuery = db.Classrooms.AsNoTracking()
            .Where(item => item.TeacherId == teacherId && item.Status == ClassroomStatus.Active)
            .Select(item => new { item.Id, item.Name });
        if (classroomId.HasValue)
        {
            var owned = await db.Classrooms.AnyAsync(item => item.Id == classroomId.Value && item.TeacherId == teacherId, ct);
            if (!owned) throw NotFound("A turma selecionada não pertence à sua conta.");
            classroomsQuery = classroomsQuery.Where(item => item.Id == classroomId.Value);
        }
        if (moduleId.HasValue)
        {
            var matchingClassroomIds = await db.ClassroomModules.AsNoTracking().Where(link => link.ModuleId == moduleId.Value
                && link.Module.Status == ModuleStatus.Published && link.Classroom.TeacherId == teacherId
                && link.Classroom.Status == ClassroomStatus.Active).Select(link => link.ClassroomId).ToArrayAsync(ct);
            classroomsQuery = classroomsQuery.Where(item => matchingClassroomIds.Contains(item.Id));
        }
        var classrooms = await classroomsQuery.ToListAsync(ct);
        var scopes = new List<ClassroomData>();
        foreach (var classroom in classrooms)
            scopes.Add(await GetClassroomDataAsync(classroom.Id, moduleId, true, ct));
        var students = scopes.SelectMany(scope => scope.Students).DistinctBy(student => student.Id).ToArray();
        var concepts = scopes.SelectMany(scope => scope.Concepts).DistinctBy(concept => concept.Id).ToArray();
        var states = await GetStatesAsync(students.Select(item => item.Id).ToArray(), concepts.Select(item => item.Id).ToArray(), ct);
        var distribution = Distribution(students, concepts, states);
        var modules = scopes.SelectMany(scope => scope.ModuleIds).Distinct().ToArray();
        var attempts = await GetAttemptsAsync(students.Select(item => item.Id).ToArray(), modules, range, ct);
        var filteredAttempts = FilterAttempts(attempts, filterType);
        var comparison = scopes.Select(scope =>
        {
            var progress = scope.Students.Select(student => Percent(
                scope.Concepts.Count(concept => states.GetValueOrDefault((student.Id, concept.Id)) == LearningState.Mastered),
                scope.Concepts.Count)).ToArray();
            return new ClassroomComparisonStatistics(scope.ClassroomId, scope.ClassroomName,
                scope.Students.Count, progress.Length == 0 ? 0 : progress.Average());
        }).ToArray();
        return new TeacherDashboardStatisticsResponse(distribution, Daily(filteredAttempts, range), comparison);
    }

    public async Task<ClassroomStatisticsResponse> GetClassroomAsync(Guid teacherId, Guid classroomId, int days, string? from, string? to, Guid? moduleId, string? activityType, int minAttempts, CancellationToken ct)
    {
        var range = ParseRange(days, from, to);
        var filterType = ParseActivityType(activityType);
        if (minAttempts is not (3 or 5 or 10)) throw InvalidFilter("O mínimo de tentativas deve ser 3, 5 ou 10.");
        var owner = await db.Classrooms.AnyAsync(item => item.Id == classroomId && item.TeacherId == teacherId, ct);
        if (!owner) throw NotFound("A turma não foi encontrada.");
        var scope = await GetClassroomDataAsync(classroomId, moduleId, false, ct);
        var states = await GetStatesAsync(scope.Students.Select(item => item.Id).ToArray(), scope.Concepts.Select(item => item.Id).ToArray(), ct);
        var attempts = await GetAttemptsAsync(scope.Students.Select(item => item.Id).ToArray(), scope.ModuleIds, range, ct);
        var filteredAttempts = FilterAttempts(attempts, filterType);
        return new ClassroomStatisticsResponse(Distribution(scope.Students, scope.Concepts, states),
            Daily(filteredAttempts, range), TypeStatistics(attempts),
            ErrorConcepts(filteredAttempts, minAttempts), ProgressBands(scope.Students, scope.Concepts, states));
    }

    public async Task<StudentStatisticsResponse> GetStudentAsync(Guid teacherId, Guid classroomId, Guid studentId,
        int days, string? from, string? to, Guid? moduleId, string? activityType, CancellationToken ct)
    {
        var range = ParseRange(days, from, to);
        var filterType = ParseActivityType(activityType);
        var student = await db.Students.AsNoTracking()
            .Where(item => item.Id == studentId && item.ClassroomId == classroomId && item.Classroom.TeacherId == teacherId)
            .Select(item => new StudentInfo(item.Id)).SingleOrDefaultAsync(ct)
            ?? throw NotFound("O aluno não foi encontrado nesta turma.");
        var moduleIds = await GetPublishedModuleIdsAsync(classroomId, moduleId, ct);
        var concepts = moduleIds.Length == 0 ? [] : await db.Concepts.AsNoTracking()
            .Where(concept => concept.IsActive && moduleIds.Contains(concept.ModuleId))
            .Select(concept => new ConceptInfo(concept.Id, concept.Name)).ToListAsync(ct);
        var states = await GetStatesAsync([student.Id], concepts.Select(item => item.Id).ToArray(), ct);
        var attempts = await GetAttemptsAsync([student.Id], moduleIds, range, ct);
        var filteredAttempts = FilterAttempts(attempts, filterType);
        return new StudentStatisticsResponse(Distribution([student], concepts, states),
            Daily(filteredAttempts, range), TypeStatistics(attempts), ErrorConcepts(filteredAttempts, 3));
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

    private async Task<List<AttemptInfo>> GetAttemptsAsync(Guid[] studentIds, Guid[] moduleIds, DateRange range, CancellationToken ct)
    {
        var start = range.Start.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var end = range.End.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        if (studentIds.Length == 0 || moduleIds.Length == 0) return [];
        return await db.ActivityAttempts.AsNoTracking()
            .Where(attempt => studentIds.Contains(attempt.StudentId) && moduleIds.Contains(attempt.Concept.ModuleId)
                && attempt.StudySession.Mode == StudySessionMode.Normal
                && attempt.AnsweredAtUtc >= start && attempt.AnsweredAtUtc < end)
            .Select(attempt => new AttemptInfo(attempt.ConceptId, attempt.Concept.Name, attempt.ActivityType,
                attempt.AnsweredAtUtc, attempt.WasCorrect))
            .ToListAsync(ct);
    }

    private IReadOnlyList<DailyLearningStatistics> Daily(List<AttemptInfo> attempts, DateRange range)
    {
        var count = range.End.DayNumber - range.Start.DayNumber + 1;
        return Enumerable.Range(0, count).Select(offset =>
        {
            var date = range.Start.AddDays(offset);
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

    private static IReadOnlyList<ErrorConceptStatistics> ErrorConcepts(List<AttemptInfo> attempts, int minAttempts) =>
        attempts.Where(item => ScoredTypes.Contains(item.Type)).GroupBy(item => new { item.ConceptId, item.Name })
            .Select(group => new { group.Key.ConceptId, group.Key.Name, Attempts = group.Count(), Errors = group.Count(item => !item.WasCorrect) })
            .Where(item => item.Attempts >= minAttempts)
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
    private DateRange ParseRange(int days, string? from, string? to)
    {
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        if ((from is null) != (to is null)) throw InvalidFilter("Informe as duas datas, De e Até.");
        if (from is not null && to is not null)
        {
            if (!DateOnly.TryParseExact(from, "yyyy-MM-dd", out var start) || !DateOnly.TryParseExact(to, "yyyy-MM-dd", out var requestedEnd))
                throw InvalidFilter("As datas devem usar o formato AAAA-MM-DD.");
            var end = requestedEnd > today ? today : requestedEnd;
            if (start > end) throw InvalidFilter("A data inicial deve ser anterior ou igual à final.");
            if (end.DayNumber - start.DayNumber + 1 > 180) throw InvalidFilter("O intervalo não pode ultrapassar 180 dias.");
            return new DateRange(start, end);
        }
        if (days is not (7 or 30 or 90 or 180)) throw InvalidFilter("O período deve ser 7, 30, 90 ou 180 dias.");
        return new DateRange(today.AddDays(-(days - 1)), today);
    }
    private static ActivityType? ParseActivityType(string? value)
    {
        if (value is null) return null;
        if (Enum.TryParse<ActivityType>(value, true, out var parsed) && ScoredTypes.Contains(parsed)) return parsed;
        throw InvalidFilter("O tipo de atividade deve ser TRUE_FALSE, FILL_BLANK, GUESS_CONCEPT ou ORDERING.");
    }
    private static List<AttemptInfo> FilterAttempts(List<AttemptInfo> attempts, ActivityType? type) =>
        type is null ? attempts : attempts.Where(item => item.Type == type.Value).ToList();
    private static ApiException InvalidFilter(string message) => new(400, "invalid_statistics_filter", message);
    private static ApiException NotFound(string message) => new(404, "statistics_scope_not_found", message);
    private sealed record StudentInfo(Guid Id);
    private sealed record ConceptInfo(Guid Id, string Name);
    private sealed record AttemptInfo(Guid ConceptId, string Name, ActivityType Type, DateTime AnsweredAtUtc, bool WasCorrect);
    private sealed record DateRange(DateOnly Start, DateOnly End);
    private sealed record ClassroomData(Guid ClassroomId, string ClassroomName, Guid[] ModuleIds, List<StudentInfo> Students, List<ConceptInfo> Concepts);
}
