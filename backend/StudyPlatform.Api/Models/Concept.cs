using StudyPlatform.Api.Models.Activities;

namespace StudyPlatform.Api.Models;

public sealed class Concept
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ModuleId { get; set; }
    public string? ExternalId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Definition { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public Module Module { get; set; } = null!;
    public ICollection<ConceptPrerequisite> Prerequisites { get; set; } = new List<ConceptPrerequisite>();
    public ICollection<ConceptPrerequisite> RequiredBy { get; set; } = new List<ConceptPrerequisite>();
    public ICollection<ConceptKeyword> Keywords { get; set; } = new List<ConceptKeyword>();
    public ICollection<ConceptClue> Clues { get; set; } = new List<ConceptClue>();
    public ICollection<RecognitionActivity> RecognitionActivities { get; set; } = new List<RecognitionActivity>();
    public ICollection<FillBlankActivity> FillBlankActivities { get; set; } = new List<FillBlankActivity>();
    public ICollection<OrderingActivity> OrderingActivities { get; set; } = new List<OrderingActivity>();
    public ICollection<StudentConceptState> StudentStates { get; set; } = new List<StudentConceptState>();
    public ICollection<ActivityAttempt> ActivityAttempts { get; set; } = new List<ActivityAttempt>();
}