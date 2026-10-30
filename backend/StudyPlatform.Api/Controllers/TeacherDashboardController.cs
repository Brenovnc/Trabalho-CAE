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
public sealed class TeacherDashboardController(TeacherProgressService progress, TeacherStatisticsService statistics) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<TeacherDashboardResponse>> Get(CancellationToken ct) => Ok(await progress.GetDashboardAsync(TeacherId, ct));
    [HttpGet("statistics")]
    public async Task<ActionResult<TeacherDashboardStatisticsResponse>> GetStatistics([FromQuery] int days = 30, [FromQuery] string? from = null,
        [FromQuery] string? to = null, [FromQuery] Guid? classroomId = null, [FromQuery] Guid? moduleId = null,
        [FromQuery] string? activityType = null, CancellationToken ct = default) =>
        Ok(await statistics.GetDashboardAsync(TeacherId, days, from, to, classroomId, moduleId, activityType, ct));
    private Guid TeacherId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
