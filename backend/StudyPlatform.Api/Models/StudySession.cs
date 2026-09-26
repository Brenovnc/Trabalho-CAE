using StudyPlatform.Api.Models.Activities;
using StudyPlatform.Api.Domain.Enums;

namespace StudyPlatform.Api.Models;

public sealed class StudySession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StudentId { get; set; }
    public Guid ModuleId { get; set; }
    public StudySessionStatus Status { get; set; } = StudySessionStatus.Active;
    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; set; }
    public int TotalActivities { get; set; }
    public int CompletedActivities { get; set; }

    public Student Student { get; set; } = null!;
    public Module Module { get; set; } = null!;
    public ICollection<ActivityAttempt> Attempts { get; set; } = new List<ActivityAttempt>();
    public ICollection<StudentConceptState> LastFreeRecallSuccessStates { get; set; } = new List<StudentConceptState>();
}