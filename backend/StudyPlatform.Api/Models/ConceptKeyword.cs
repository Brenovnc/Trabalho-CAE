namespace StudyPlatform.Api.Models;

public sealed class ConceptKeyword
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ConceptId { get; set; }
    public string Value { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public Concept Concept { get; set; } = null!;
}