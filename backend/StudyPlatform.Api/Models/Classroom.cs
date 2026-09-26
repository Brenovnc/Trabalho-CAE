namespace StudyPlatform.Api.Models;

public sealed class Classroom
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TeacherId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string CodeNormalized { get; private set; } = string.Empty;
    public Domain.Enums.ClassroomStatus Status { get; set; } = Domain.Enums.ClassroomStatus.Active;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public Teacher Teacher { get; set; } = null!;
    public ICollection<Student> Students { get; set; } = new List<Student>();
    public ICollection<ClassroomModule> ClassroomModules { get; set; } = new List<ClassroomModule>();
}