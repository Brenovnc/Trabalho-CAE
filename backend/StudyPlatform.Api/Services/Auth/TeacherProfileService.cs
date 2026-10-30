using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StudyPlatform.Api.Data;
using StudyPlatform.Api.DTOs.Teachers;
using StudyPlatform.Api.Exceptions;
using StudyPlatform.Api.Models;

namespace StudyPlatform.Api.Services.Auth;

public sealed class TeacherProfileService(ApplicationDbContext db, IPasswordHasher<Teacher> hasher, TimeProvider clock)
{
    public async Task<TeacherProfileResponse> GetAsync(Guid id, CancellationToken ct)
    {
        var teacher = await db.Teachers.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new { x.Name, x.Email, x.Education, x.Institution, PhotoAt = db.TeacherPhotos.Where(p => p.TeacherId == id).Select(p => (DateTime?)p.UpdatedAtUtc).FirstOrDefault() })
            .SingleOrDefaultAsync(ct) ?? throw new ApiException(404, "teacher_not_found", "Perfil não encontrado.");
        return new(teacher.Name, teacher.Email, teacher.Education, teacher.Institution, teacher.PhotoAt.HasValue, teacher.PhotoAt?.Ticks);
    }

    public async Task<Teacher> UpdateAsync(Guid id, UpdateTeacherProfileRequest request, CancellationToken ct)
    {
        var teacher = await FindAsync(id, ct);
        teacher.Name = request.Name.Trim();
        teacher.Education = BlankToNull(request.Education);
        teacher.Institution = BlankToNull(request.Institution);
        teacher.UpdatedAtUtc = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);
        return teacher;
    }

    public async Task ChangePasswordAsync(Guid id, ChangeTeacherPasswordRequest request, CancellationToken ct)
    {
        var teacher = await FindAsync(id, ct);
        if (hasher.VerifyHashedPassword(teacher, teacher.PasswordHash, request.CurrentPassword) == PasswordVerificationResult.Failed)
            throw new ApiException(400, "invalid_current_password", "A senha atual está incorreta.");
        if (request.NewPassword == request.CurrentPassword)
            throw new ApiException(400, "password_unchanged", "A nova senha deve ser diferente da senha atual.");
        teacher.PasswordHash = hasher.HashPassword(teacher, request.NewPassword);
        teacher.UpdatedAtUtc = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);
    }

    public async Task SavePhotoAsync(Guid id, byte[] bytes, string type, CancellationToken ct)
    {
        var photo = await db.TeacherPhotos.SingleOrDefaultAsync(p => p.TeacherId == id, ct);
        if (photo is null) db.TeacherPhotos.Add(new TeacherPhoto { TeacherId = id, Data = bytes, ContentType = type, UpdatedAtUtc = clock.GetUtcNow().UtcDateTime });
        else { photo.Data = bytes; photo.ContentType = type; photo.UpdatedAtUtc = clock.GetUtcNow().UtcDateTime; }
        await db.SaveChangesAsync(ct);
    }

    public Task<TeacherPhoto?> PhotoAsync(Guid id, CancellationToken ct) => db.TeacherPhotos.AsNoTracking().SingleOrDefaultAsync(p => p.TeacherId == id, ct);
    public async Task DeletePhotoAsync(Guid id, CancellationToken ct) { await db.TeacherPhotos.Where(p => p.TeacherId == id).ExecuteDeleteAsync(ct); }
    private async Task<Teacher> FindAsync(Guid id, CancellationToken ct) => await db.Teachers.SingleOrDefaultAsync(x => x.Id == id, ct)
        ?? throw new ApiException(404, "teacher_not_found", "Perfil não encontrado.");
    private static string? BlankToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
