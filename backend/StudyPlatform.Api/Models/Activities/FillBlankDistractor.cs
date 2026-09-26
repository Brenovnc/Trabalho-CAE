namespace StudyPlatform.Api.Models.Activities;

public sealed class FillBlankDistractor
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FillBlankActivityId { get; set; }
    public string Text { get; set; } = string.Empty;

    public FillBlankActivity Activity { get; set; } = null!;
}