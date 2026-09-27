using StudyPlatform.Api.Domain.Enums;
using StudyPlatform.Api.Models.Activities;

namespace StudyPlatform.Api.Models;

/// <summary>Durable snapshot of the activity currently or previously shown in a session.</summary>
public sealed class SessionActivityPresentation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StudySessionId { get; set; }
    public int SequenceNumber { get; set; }
    public Guid ConceptId { get; set; }
    public ActivityType ActivityType { get; set; }
    public Guid? ActivityId { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime? AnsweredAtUtc { get; set; }
    public int RevealedClueCount { get; set; }
    /// <summary>Private grading snapshot. Only safe fields are mapped to the public response.</summary>
    public required string SnapshotJson { get; set; }

    public StudySession StudySession { get; set; } = null!;
    public Concept Concept { get; set; } = null!;
    public ICollection<ActivityAttempt> Attempts { get; set; } = new List<ActivityAttempt>();
}
