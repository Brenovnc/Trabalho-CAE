using StudyPlatform.Api.Models;

namespace StudyPlatform.Api.Models.Activities;

public sealed class OrderingActivity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ConceptId { get; set; }
    public string Instruction { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public Concept Concept { get; set; } = null!;
    public ICollection<OrderingActivityItem> Items { get; set; } = new List<OrderingActivityItem>();
    public ICollection<ActivityAttempt> Attempts { get; set; } = new List<ActivityAttempt>();
}