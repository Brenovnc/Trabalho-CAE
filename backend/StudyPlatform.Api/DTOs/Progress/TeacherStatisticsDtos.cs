namespace StudyPlatform.Api.DTOs.Progress;

public sealed record ConceptDistributionStatistics(int Mastered, int Learning, int NotStarted);
public sealed record DailyLearningStatistics(DateOnly Date, int Activities, double? CorrectRate);
public sealed record ActivityTypeStatistics(string Type, int Attempts, int CorrectAttempts, double? CorrectRate);
public sealed record ErrorConceptStatistics(Guid ConceptId, string Name, int Attempts, double ErrorRate);
public sealed record ClassroomComparisonStatistics(Guid ClassroomId, string Name, int ActiveStudents, double AverageProgress);
public sealed record StudentProgressBandStatistics(string Range, int Students);
public sealed record TeacherDashboardStatisticsResponse(
    ConceptDistributionStatistics Distribution,
    IReadOnlyList<DailyLearningStatistics> Daily,
    IReadOnlyList<ClassroomComparisonStatistics> Classrooms);
public sealed record ClassroomStatisticsResponse(
    ConceptDistributionStatistics Distribution,
    IReadOnlyList<DailyLearningStatistics> Daily,
    IReadOnlyList<ActivityTypeStatistics> ActivityTypes,
    IReadOnlyList<ErrorConceptStatistics> MostErrors,
    IReadOnlyList<StudentProgressBandStatistics> ProgressBands);
public sealed record StudentStatisticsResponse(
    ConceptDistributionStatistics Distribution,
    IReadOnlyList<DailyLearningStatistics> Daily,
    IReadOnlyList<ActivityTypeStatistics> ActivityTypes,
    IReadOnlyList<ErrorConceptStatistics> MostErrors);
