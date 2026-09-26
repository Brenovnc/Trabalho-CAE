using StudyPlatform.Api.Models.Activities;
namespace StudyPlatform.Api.Models;

public sealed class Student
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClassroomId { get; set; }
    public string EnrollmentNumber { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? PasswordHash { get; set; }
    public string? TemporaryAccessCodeHash { get; set; }
    public DateTime? TemporaryAccessCodeExpiresAtUtc { get; set; }
    public int TemporaryAccessCodeFailedAttempts { get; set; }
    public bool IsActivated { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public Classroom Classroom { get; set; } = null!;
    public ICollection<StudentConceptState> ConceptStates { get; set; } = new List<StudentConceptState>();
    public ICollection<StudySession> StudySessions { get; set; } = new List<StudySession>();
    public ICollection<ActivityAttempt> ActivityAttempts { get; set; } = new List<ActivityAttempt>();
}