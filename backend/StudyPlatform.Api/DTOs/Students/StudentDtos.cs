using System.ComponentModel.DataAnnotations;

namespace StudyPlatform.Api.DTOs.Students;

public sealed class CreateStudentRequest
{
    [Required, StringLength(64, MinimumLength = 1)] public string EnrollmentNumber { get; init; } = string.Empty;
    [StringLength(120)] public string? Name { get; init; }
}

public sealed record StudentSummaryResponse(Guid Id, string EnrollmentNumber, string? Name, bool IsActive, bool IsActivated, DateTime CreatedAtUtc);
public sealed record StudentCredentialsResponse(Guid StudentId, string EnrollmentNumber, string? Name, string TemporaryAccessCode, DateTime ExpiresAtUtc);
