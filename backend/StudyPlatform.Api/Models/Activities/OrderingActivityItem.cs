namespace StudyPlatform.Api.Models.Activities;

public sealed class OrderingActivityItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderingActivityId { get; set; }
    public int Position { get; set; }
    public string Text { get; set; } = string.Empty;

    public OrderingActivity Activity { get; set; } = null!;
}