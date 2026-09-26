using StudyPlatform.Api.Models;

namespace StudyPlatform.Api.Models.Activities;

public sealed class FillBlankActivity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ConceptId { get; set; }
    public string Text { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public Concept Concept { get; set; } = null!;
    public ICollection<FillBlankAnswer> Answers { get; set; } = new List<FillBlankAnswer>();
    public ICollection<FillBlankDistractor> Distractors { get; set; } = new List<FillBlankDistractor>();
    public ICollection<ActivityAttempt> Attempts { get; set; } = new List<ActivityAttempt>();
}