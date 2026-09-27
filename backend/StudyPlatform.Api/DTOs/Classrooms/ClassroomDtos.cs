using System.ComponentModel.DataAnnotations;

namespace StudyPlatform.Api.DTOs.Classrooms;

public class CreateClassroomRequest
{
    [Required, StringLength(160, MinimumLength = 1)] public string Name { get; init; } = string.Empty;
    [Required, StringLength(32, MinimumLength = 3), RegularExpression("^[A-Za-z0-9]+(?:-[A-Za-z0-9]+)*$")] public string Code { get; init; } = string.Empty;
}

public sealed class UpdateClassroomRequest : CreateClassroomRequest { }

public sealed record ClassroomSummaryResponse(Guid Id, string Name, string Code, string Status, int StudentCount, int ModuleCount, DateTime CreatedAtUtc, DateTime UpdatedAtUtc);
public sealed record ClassroomModuleResponse(Guid Id, string Title, string Subject, int Version, string Status, DateTime AssignedAtUtc);
public sealed record ClassroomDetailsResponse(Guid Id, string Name, string Code, string Status, DateTime CreatedAtUtc, DateTime UpdatedAtUtc, IReadOnlyList<ClassroomModuleResponse> Modules, int StudentCount);
public sealed record AssignModuleRequest([Required] Guid ModuleId);

