using System.ComponentModel.DataAnnotations;

namespace StudyPlatform.Api.DTOs.Auth;

public sealed class StudentActivationRequest
{
    [Required, StringLength(32)]
    public string ClassroomCode { get; init; } = string.Empty;

    [Required, StringLength(64)]
    public string EnrollmentNumber { get; init; } = string.Empty;

    [Required, StringLength(128)]
    public string TemporaryCode { get; init; } = string.Empty;

    [Required, StringLength(128, MinimumLength = 8)]
    public string Password { get; init; } = string.Empty;

    [Required, Compare(nameof(Password))]
    public string ConfirmPassword { get; init; } = string.Empty;
}

public sealed class StudentLoginRequest
{
    [Required, StringLength(32)]
    public string ClassroomCode { get; init; } = string.Empty;

    [Required, StringLength(64)]
    public string EnrollmentNumber { get; init; } = string.Empty;

    [Required, StringLength(128)]
    public string Password { get; init; } = string.Empty;
}
