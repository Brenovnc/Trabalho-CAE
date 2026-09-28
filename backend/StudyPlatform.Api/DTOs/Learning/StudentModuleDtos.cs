namespace StudyPlatform.Api.DTOs.Learning;

public sealed record StudentModuleResponse(
    Guid Id,
    string Title,
    string? Description,
    string Subject,
    Guid? ActiveSessionId,
    int ActiveConcepts = 0,
    int MasteredConcepts = 0,
    int LearningConcepts = 0,
    int NotStartedConcepts = 0,
    int PendingReviews = 0,
    int ProgressPercent = 0,
    DateTime? NextReviewAtUtc = null,
    string? ActiveSessionMode = null,
    IReadOnlyList<StudentModuleConceptOption>? Concepts = null);

public sealed record StudentModuleConceptOption(Guid Id, string Name);
