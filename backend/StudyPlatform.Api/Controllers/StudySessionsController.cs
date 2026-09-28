using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyPlatform.Api.DTOs.Auth;
using StudyPlatform.Api.DTOs.Learning;
using StudyPlatform.Api.Services.Learning;

namespace StudyPlatform.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthPolicies.Student)]
[Route("api/student")]
public sealed class StudySessionsController(StudySessionService sessions, StudentStudyCatalogService catalog) : ControllerBase
{
    [HttpGet("modules")]
    public async Task<ActionResult<IReadOnlyList<StudentModuleResponse>>> ListModules(CancellationToken ct) =>
        Ok(await catalog.ListModulesAsync(StudentId, ct));

    [HttpGet("modules/{moduleId:guid}")]
    public async Task<ActionResult<StudentModuleResponse>> GetModule(Guid moduleId, CancellationToken ct) =>
        Ok(await catalog.GetModuleAsync(StudentId, moduleId, ct));

    [HttpPost("modules/{moduleId:guid}/sessions")]
    public async Task<ActionResult<StartStudySessionResponse>> Start(Guid moduleId, CancellationToken ct) =>
        Ok(await sessions.StartAsync(StudentId, moduleId, ct));

    [HttpPost("modules/{moduleId:guid}/free-practice/sessions")]
    public async Task<ActionResult<StartStudySessionResponse>> StartFreePractice(Guid moduleId, StartFreePracticeRequest request, CancellationToken ct) =>
        Ok(await sessions.StartAsync(StudentId, moduleId, ct, StudyPlatform.Api.Domain.Enums.StudySessionMode.FreePractice, request.ConceptId));

    [HttpPost("modules/{moduleId:guid}/reset-progress")]
    public async Task<IActionResult> ResetProgress(Guid moduleId, CancellationToken ct)
    {
        await sessions.ResetModuleProgressAsync(StudentId, moduleId, ct);
        return NoContent();
    }

    [HttpGet("sessions/{sessionId:guid}")]
    public async Task<ActionResult<StudySessionResponse>> Get(Guid sessionId, CancellationToken ct) =>
        Ok(await sessions.GetAsync(StudentId, sessionId, ct));

    [HttpGet("sessions/{sessionId:guid}/next")]
    public async Task<ActionResult<StudySessionResponse>> Next(Guid sessionId, CancellationToken ct) =>
        Ok(await sessions.GetNextAsync(StudentId, sessionId, ct));

    [HttpPost("sessions/{sessionId:guid}/activities/{presentationId:guid}/reveal")]
    public async Task<ActionResult<RevealHintResponse>> Reveal(Guid sessionId, Guid presentationId, CancellationToken ct) =>
        Ok(await sessions.RevealNextHintAsync(StudentId, sessionId, presentationId, ct));

    [HttpPost("sessions/{sessionId:guid}/answer")]
    public async Task<ActionResult<ActivityAnswerResponse>> Answer(Guid sessionId, SubmitActivityAnswerRequest request, CancellationToken ct) =>
        Ok(await sessions.SubmitAsync(StudentId, sessionId, request, ct));

    [HttpPost("sessions/{sessionId:guid}/abandon")]
    public async Task<IActionResult> Abandon(Guid sessionId, CancellationToken ct)
    {
        await sessions.AbandonAsync(StudentId, sessionId, ct);
        return NoContent();
    }

    private Guid StudentId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
