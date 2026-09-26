using StudyPlatform.Api.Models;

namespace StudyPlatform.Api.Models.Activities;

public sealed class RecognitionActivity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ConceptId { get; set; }
    public string Statement { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
    public string Explanation { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public Concept Concept { get; set; } = null!;
    public ICollection<ActivityAttempt> Attempts { get; set; } = new List<ActivityAttempt>();
}