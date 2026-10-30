namespace StudyPlatform.Api.Models;

public sealed class TeacherPhoto
{
    public Guid TeacherId { get; set; }
    public byte[] Data { get; set; } = [];
    public string ContentType { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public Teacher Teacher { get; set; } = null!;
}
