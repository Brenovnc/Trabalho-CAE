using System.ComponentModel.DataAnnotations;

namespace StudyPlatform.Api.DTOs.Teachers;

public sealed record TeacherProfileResponse(string Name, string Email, string? Education, string? Institution, bool HasPhoto, long? PhotoVersion);
public sealed class UpdateTeacherProfileRequest
{
    [Required, StringLength(120, MinimumLength = 1)] public string Name { get; init; } = "";
    [StringLength(160)] public string? Education { get; init; }
    [StringLength(160)] public string? Institution { get; init; }
}
public sealed class ChangeTeacherPasswordRequest
{
    [Required] public string CurrentPassword { get; init; } = "";
    [Required, StringLength(128, MinimumLength = 8)] public string NewPassword { get; init; } = "";
    [Required, Compare(nameof(NewPassword))] public string ConfirmNewPassword { get; init; } = "";
}
