namespace StudyPlatform.Api.DTOs.Progress;

public sealed record TeacherDashboardResponse(int Modules, int ActiveClassrooms, int ActiveStudents);
public sealed record ClassroomProgressResponse(Guid ClassroomId, string ClassroomName, int TotalActiveConcepts, IReadOnlyList<ClassroomStudentProgressResponse> Students);
public sealed record ClassroomStudentProgressResponse(Guid StudentId, string EnrollmentNumber, string? Name, bool IsActive, int MasteredConcepts, int LearningConcepts, int NotStartedConcepts, int PendingReviews, int ProgressPercent, DateTime? LastAccessAtUtc);
public sealed record StudentProgressResponse(Guid StudentId, string EnrollmentNumber, string? Name, bool IsActive, IReadOnlyList<StudentModuleProgressResponse> Modules);
public sealed record StudentModuleProgressResponse(Guid ModuleId, string Title, string Subject, int ActiveConcepts, int MasteredConcepts, int LearningConcepts, int NotStartedConcepts, int PendingReviews, int ProgressPercent);
