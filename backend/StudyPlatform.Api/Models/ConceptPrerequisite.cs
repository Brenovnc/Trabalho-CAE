namespace StudyPlatform.Api.Models;

public sealed class ConceptPrerequisite
{
    public Guid ConceptId { get; set; }
    public Guid PrerequisiteConceptId { get; set; }
    public Guid ModuleId { get; set; }

    public Concept Concept { get; set; } = null!;
    public Concept PrerequisiteConcept { get; set; } = null!;
}