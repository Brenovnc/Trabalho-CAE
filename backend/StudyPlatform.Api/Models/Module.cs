using StudyPlatform.Api.Domain.Enums;

namespace StudyPlatform.Api.Models;

public sealed class Module
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TeacherId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Subject { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
    public ModuleStatus Status { get; set; } = ModuleStatus.Draft;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public Teacher Teacher { get; set; } = null!;
    public ICollection<ClassroomModule> ClassroomModules { get; set; } = new List<ClassroomModule>();
    public ICollection<Concept> Concepts { get; set; } = new List<Concept>();
    public ICollection<StudySession> StudySessions { get; set; } = new List<StudySession>();
}