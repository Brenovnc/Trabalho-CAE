namespace StudyPlatform.Api.Models.Activities;

public sealed class FillBlankAnswer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FillBlankActivityId { get; set; }
    public int SlotNumber { get; set; }
    public string CorrectText { get; set; } = string.Empty;

    public FillBlankActivity Activity { get; set; } = null!;
}