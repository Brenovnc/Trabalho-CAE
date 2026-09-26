using StudyPlatform.Api.Domain.Enums;

namespace StudyPlatform.Api.Models;

public sealed class StudentConceptState
{
    public Guid StudentId { get; set; }
    public Guid ConceptId { get; set; }
    public LearningState LearningState { get; set; } = LearningState.New;
    public int FreeRecallSuccessCount { get; set; }
    public DateTime? LastAttemptAtUtc { get; set; }
    public DateTime? LastSuccessAtUtc { get; set; }
    public DateTime? LastFailureAtUtc { get; set; }
    public DateTime? LastFreeRecallSuccessAtUtc { get; set; }
    public Guid? LastFreeRecallSuccessSessionId { get; set; }

    // FSRS card data is stored here; algorithm/library behavior belongs in FsrsService.
    public string? FsrsState { get; set; }
    public double? Difficulty { get; set; }
    public double? Stability { get; set; }
    public DateTime? DueAtUtc { get; set; }
    public DateTime? LastReviewAtUtc { get; set; }
    public double? ElapsedDays { get; set; }
    public double? ScheduledDays { get; set; }
    public int Repetitions { get; set; }
    public int Lapses { get; set; }
    public FsrsRating? LastFsrsRating { get; set; }

    public Student Student { get; set; } = null!;
    public Concept Concept { get; set; } = null!;
    public StudySession? LastFreeRecallSuccessSession { get; set; }
}