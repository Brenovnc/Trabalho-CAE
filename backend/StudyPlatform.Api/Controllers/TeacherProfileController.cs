using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyPlatform.Api.DTOs.Auth;
using StudyPlatform.Api.DTOs.Teachers;
using StudyPlatform.Api.Services.Auth;

namespace StudyPlatform.Api.Controllers;

[ApiController, Authorize(Policy = AuthPolicies.Teacher), Route("api/teacher/profile")]
public sealed class TeacherProfileController(TeacherProfileService profiles, TimeProvider clock) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<TeacherProfileResponse>> Get(CancellationToken ct) => Ok(await profiles.GetAsync(TeacherId, ct));

    [HttpPut]
    public async Task<ActionResult<TeacherProfileResponse>> Update(UpdateTeacherProfileRequest request, CancellationToken ct)
    {
        var teacher = await profiles.UpdateAsync(TeacherId, request, ct);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, AuthPrincipalFactory.ForTeacher(teacher), new AuthenticationProperties { IsPersistent = true, ExpiresUtc = clock.GetUtcNow().AddHours(8), AllowRefresh = false });
        return Ok(await profiles.GetAsync(TeacherId, ct));
    }

    [HttpPut("password")]
    public async Task<IActionResult> Password(ChangeTeacherPasswordRequest request, CancellationToken ct) { await profiles.ChangePasswordAsync(TeacherId, request, ct); return NoContent(); }

    [HttpPut("photo")]
    [RequestSizeLimit(2 * 1024 * 1024)]
    public async Task<IActionResult> Photo(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length is <= 0 or > 2 * 1024 * 1024) return BadRequest(new { code = "invalid_photo_size", message = "A imagem deve ter até 2 MiB." });
        await using var stream = new MemoryStream(); await file.CopyToAsync(stream, ct); var bytes = stream.ToArray();
        var type = bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) ? "image/png"
            : bytes.Length >= 3 && bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[2] == 0xff ? "image/jpeg"
            : bytes.Length >= 12 && bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) && bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8) ? "image/webp" : null;
        if (type is null) return BadRequest(new { code = "invalid_photo_format", message = "Use uma imagem PNG, JPEG ou WebP válida." });
        await profiles.SavePhotoAsync(TeacherId, bytes, type, ct); return NoContent();
    }

    [HttpDelete("photo")]
    public async Task<IActionResult> DeletePhoto(CancellationToken ct) { await profiles.DeletePhotoAsync(TeacherId, ct); return NoContent(); }

    [HttpGet("photo")]
    public async Task<IActionResult> GetPhoto(CancellationToken ct)
    {
        var photo = await profiles.PhotoAsync(TeacherId, ct); if (photo is null) return NotFound();
        Response.Headers.CacheControl = "private, max-age=86400"; return File(photo.Data, photo.ContentType);
    }
    private Guid TeacherId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
