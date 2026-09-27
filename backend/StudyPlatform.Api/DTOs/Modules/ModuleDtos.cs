using System.ComponentModel.DataAnnotations;
using StudyPlatform.Api.Domain.Enums;
using StudyPlatform.Api.DTOs.Concepts;

namespace StudyPlatform.Api.DTOs.Modules;

public class CreateModuleRequest
{
    [Required, StringLength(160, MinimumLength = 1)] public string Title { get; init; } = string.Empty;
    [StringLength(4000)] public string? Description { get; init; }
    [Required, StringLength(120, MinimumLength = 1)] public string Subject { get; init; } = string.Empty;
    [Range(1, int.MaxValue)] public int Version { get; init; } = 1;
}

public sealed class UpdateModuleRequest : CreateModuleRequest { }

public sealed record ModuleSummaryResponse(
    Guid Id, string Title, string? Description, string Subject, int Version,
    ModuleStatus Status, DateTime CreatedAtUtc, DateTime UpdatedAtUtc, int ActiveConceptCount);

public sealed record ModuleDetailsResponse(
    Guid Id, string Title, string? Description, string Subject, int Version,
    ModuleStatus Status, DateTime CreatedAtUtc, DateTime UpdatedAtUtc,
    IReadOnlyList<ConceptDetailsResponse> Concepts);

public sealed record PublicationIssue(Guid? ConceptId, string Code, string Message);
public sealed record PublicationValidationResponse(bool IsValid, IReadOnlyList<PublicationIssue> Errors);
