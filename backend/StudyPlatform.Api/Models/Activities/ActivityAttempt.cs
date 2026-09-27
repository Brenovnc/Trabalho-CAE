using StudyPlatform.Api.Domain.Enums;
using StudyPlatform.Api.Models;

namespace StudyPlatform.Api.Models.Activities;

public sealed class ActivityAttempt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StudentId { get; set; }
    public Guid ConceptId { get; set; }
    public Guid StudySessionId { get; set; }
    public Guid? PresentationId { get; set; }
    public ActivityType ActivityType { get; set; }
    public Guid? RecognitionActivityId { get; set; }
    public Guid? FillBlankActivityId { get; set; }
    public Guid? OrderingActivityId { get; set; }
    public bool WasCorrect { get; set; }
    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime AnsweredAtUtc { get; set; } = DateTime.UtcNow;
    public long? ResponseTimeMs { get; set; }
    public int AttemptsUsed { get; set; }
    public int HintsUsed { get; set; }
    public FsrsRating? FsrsRating { get; set; }
    public string? AnswerJson { get; set; }
    public required string ActivitySnapshotJson { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public Student Student { get; set; } = null!;
    public Concept Concept { get; set; } = null!;
    public StudySession StudySession { get; set; } = null!;
    public SessionActivityPresentation? Presentation { get; set; }
    public RecognitionActivity? RecognitionActivity { get; set; }
    public FillBlankActivity? FillBlankActivity { get; set; }
    public OrderingActivity? OrderingActivity { get; set; }
}