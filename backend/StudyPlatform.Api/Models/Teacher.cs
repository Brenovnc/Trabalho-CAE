namespace StudyPlatform.Api.Models;

public sealed class Teacher
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string EmailNormalized { get; private set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? Education { get; set; }
    public string? Institution { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<Module> Modules { get; set; } = new List<Module>();
    public ICollection<Classroom> Classrooms { get; set; } = new List<Classroom>();
    public TeacherPhoto? Photo { get; set; }
}
