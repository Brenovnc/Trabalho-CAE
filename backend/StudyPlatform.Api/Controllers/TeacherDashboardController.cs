using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyPlatform.Api.DTOs.Auth;
using StudyPlatform.Api.DTOs.Progress;
using StudyPlatform.Api.Services.Progress;

namespace StudyPlatform.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthPolicies.Teacher)]
[Route("api/teacher/dashboard")]
public sealed class TeacherDashboardController(TeacherProgressService progress) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<TeacherDashboardResponse>> Get(CancellationToken ct) => Ok(await progress.GetDashboardAsync(TeacherId, ct));
    private Guid TeacherId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
