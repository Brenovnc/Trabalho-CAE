namespace StudyPlatform.Api.Models;

public sealed class ClassroomModule
{
    public Guid ClassroomId { get; set; }
    public Guid ModuleId { get; set; }
    public DateTime AssignedAtUtc { get; set; } = DateTime.UtcNow;

    public Classroom Classroom { get; set; } = null!;
    public Module Module { get; set; } = null!;
}