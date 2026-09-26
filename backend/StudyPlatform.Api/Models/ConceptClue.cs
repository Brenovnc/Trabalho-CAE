namespace StudyPlatform.Api.Models;

public sealed class ConceptClue
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ConceptId { get; set; }
    public int Position { get; set; }
    public string Text { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public Concept Concept { get; set; } = null!;
}