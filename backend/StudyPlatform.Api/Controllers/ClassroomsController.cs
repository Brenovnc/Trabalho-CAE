using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyPlatform.Api.DTOs.Auth;
using StudyPlatform.Api.DTOs.Classrooms;
using StudyPlatform.Api.DTOs.Students;
using StudyPlatform.Api.DTOs.Progress;
using StudyPlatform.Api.Services.Classrooms;
using StudyPlatform.Api.Services.Progress;
using StudyPlatform.Api.Services.Students;

namespace StudyPlatform.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthPolicies.Teacher)]
[Route("api/classrooms")]
public sealed class ClassroomsController(ClassroomService classrooms, StudentManagementService students, TeacherProgressService progress) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ClassroomSummaryResponse>>> List(CancellationToken ct) => Ok(await classrooms.ListAsync(TeacherId, ct));

    [HttpPost]
    public async Task<ActionResult<ClassroomDetailsResponse>> Create(CreateClassroomRequest request, CancellationToken ct)
    {
        var created = await classrooms.CreateAsync(TeacherId, request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ClassroomDetailsResponse>> Get(Guid id, CancellationToken ct) => Ok(await classrooms.GetAsync(TeacherId, id, ct));

    [HttpGet("{classroomId:guid}/progress")]
    public async Task<ActionResult<ClassroomProgressResponse>> GetProgress(Guid classroomId, CancellationToken ct) =>
        Ok(await progress.GetClassroomProgressAsync(TeacherId, classroomId, ct));

    [HttpGet("{classroomId:guid}/students/{studentId:guid}/progress")]
    public async Task<ActionResult<StudentProgressResponse>> GetStudentProgress(Guid classroomId, Guid studentId, CancellationToken ct) =>
        Ok(await progress.GetStudentProgressAsync(TeacherId, classroomId, studentId, ct));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ClassroomDetailsResponse>> Update(Guid id, UpdateClassroomRequest request, CancellationToken ct) => Ok(await classrooms.UpdateAsync(TeacherId, id, request, ct));

    [HttpPost("{id:guid}/archive")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct) { await classrooms.ArchiveAsync(TeacherId, id, ct); return NoContent(); }

    [HttpPost("{classroomId:guid}/modules/{moduleId:guid}")]
    public async Task<IActionResult> AssignModule(Guid classroomId, Guid moduleId, CancellationToken ct) { await classrooms.AssignModuleAsync(TeacherId, classroomId, moduleId, ct); return NoContent(); }

    [HttpPost("{classroomId:guid}/modules")]
    public async Task<IActionResult> AssignModule(Guid classroomId, AssignModuleRequest request, CancellationToken ct) { await classrooms.AssignModuleAsync(TeacherId, classroomId, request.ModuleId, ct); return NoContent(); }

    [HttpDelete("{classroomId:guid}/modules/{moduleId:guid}")]
    public async Task<IActionResult> UnassignModule(Guid classroomId, Guid moduleId, CancellationToken ct) { await classrooms.UnassignModuleAsync(TeacherId, classroomId, moduleId, ct); return NoContent(); }

    [HttpGet("{classroomId:guid}/students")]
    public async Task<ActionResult<IReadOnlyList<StudentSummaryResponse>>> ListStudents(Guid classroomId, CancellationToken ct) => Ok(await students.ListAsync(TeacherId, classroomId, ct));

    [HttpPost("{classroomId:guid}/students")]
    public async Task<ActionResult<StudentCredentialsResponse>> CreateStudent(Guid classroomId, CreateStudentRequest request, CancellationToken ct)
    {
        var created = await students.CreateAsync(TeacherId, classroomId, request, ct);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    [HttpPost("{classroomId:guid}/students/{studentId:guid}/reset-access")]
    public async Task<ActionResult<StudentCredentialsResponse>> ResetStudentAccess(Guid classroomId, Guid studentId, CancellationToken ct) => Ok(await students.ResetAccessAsync(TeacherId, classroomId, studentId, ct));

    [HttpPost("{classroomId:guid}/students/{studentId:guid}/deactivate")]
    public async Task<IActionResult> DeactivateStudent(Guid classroomId, Guid studentId, CancellationToken ct) { await students.DeactivateAsync(TeacherId, classroomId, studentId, ct); return NoContent(); }

    private Guid TeacherId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
