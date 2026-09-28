namespace StudyPlatform.Api.DTOs.Learning;

public sealed record StudentModuleResponse(
    Guid Id,
    string Title,
    string? Description,
    string Subject,
    Guid? ActiveSessionId);
