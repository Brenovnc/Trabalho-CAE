using Microsoft.EntityFrameworkCore;
using StudyPlatform.Api.Data;
using StudyPlatform.Api.Domain.Enums;
using StudyPlatform.Api.DTOs.Learning;
using StudyPlatform.Api.Exceptions;

namespace StudyPlatform.Api.Services.Learning;

public sealed class StudentStudyCatalogService(ApplicationDbContext db)
{
    public async Task<IReadOnlyList<StudentModuleResponse>> ListModulesAsync(Guid studentId, CancellationToken ct)
    {
        var classroomId = await GetActiveClassroomIdAsync(studentId, ct);
        return await db.ClassroomModules.AsNoTracking()
            .Where(assignment => assignment.ClassroomId == classroomId
                && assignment.Module.Status == ModuleStatus.Published)
            .OrderBy(assignment => assignment.Module.Title)
            .Select(assignment => new StudentModuleResponse(
                assignment.Module.Id,
                assignment.Module.Title,
                assignment.Module.Description,
                assignment.Module.Subject,
                db.StudySessions
                    .Where(session => session.StudentId == studentId
                        && session.ModuleId == assignment.ModuleId
                        && session.Status == StudySessionStatus.Active)
                    .Select(session => (Guid?)session.Id)
                    .FirstOrDefault()))
            .ToListAsync(ct);
    }

    public async Task<StudentModuleResponse> GetModuleAsync(Guid studentId, Guid moduleId, CancellationToken ct)
    {
        var classroomId = await GetActiveClassroomIdAsync(studentId, ct);
        return await db.ClassroomModules.AsNoTracking()
            .Where(assignment => assignment.ClassroomId == classroomId
                && assignment.ModuleId == moduleId
                && assignment.Module.Status == ModuleStatus.Published)
            .Select(assignment => new StudentModuleResponse(
                assignment.Module.Id,
                assignment.Module.Title,
                assignment.Module.Description,
                assignment.Module.Subject,
                db.StudySessions
                    .Where(session => session.StudentId == studentId
                        && session.ModuleId == assignment.ModuleId
                        && session.Status == StudySessionStatus.Active)
                    .Select(session => (Guid?)session.Id)
                    .FirstOrDefault()))
            .SingleOrDefaultAsync(ct)
            ?? throw new ApiException(404, "student_module_not_found", "O módulo não foi encontrado.");
    }

    private async Task<Guid> GetActiveClassroomIdAsync(Guid studentId, CancellationToken ct) =>
        await db.Students.AsNoTracking()
            .Where(student => student.Id == studentId
                && student.IsActive
                && student.IsActivated
                && student.Classroom.Status == ClassroomStatus.Active)
            .Select(student => (Guid?)student.ClassroomId)
            .SingleOrDefaultAsync(ct)
        ?? throw new ApiException(404, "student_module_not_found", "O módulo não foi encontrado.");
}
