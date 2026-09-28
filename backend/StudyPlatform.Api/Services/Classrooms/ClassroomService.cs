using Microsoft.EntityFrameworkCore;
using Npgsql;
using StudyPlatform.Api.Data;
using StudyPlatform.Api.Domain.Enums;
using StudyPlatform.Api.DTOs.Classrooms;
using StudyPlatform.Api.Exceptions;
using StudyPlatform.Api.Models;

namespace StudyPlatform.Api.Services.Classrooms;

public sealed class ClassroomService(ApplicationDbContext db, TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<ClassroomSummaryResponse>> ListAsync(Guid teacherId, CancellationToken ct) =>
        await db.Classrooms.AsNoTracking().Where(x => x.TeacherId == teacherId)
            .OrderBy(x => x.Name)
            .Select(x => new ClassroomSummaryResponse(x.Id, x.Name, x.Code, x.Status.ToString(),
                x.Students.Count(student => student.IsActive), x.ClassroomModules.Count, x.CreatedAtUtc, x.UpdatedAtUtc))
            .ToListAsync(ct);

    public async Task<ClassroomDetailsResponse> GetAsync(Guid teacherId, Guid id, CancellationToken ct) =>
        Map(await OwnedClassroom(teacherId, id).Include(x => x.ClassroomModules).ThenInclude(x => x.Module)
            .Include(x => x.Students).SingleOrDefaultAsync(ct));

    public async Task<ClassroomDetailsResponse> CreateAsync(Guid teacherId, CreateClassroomRequest request, CancellationToken ct)
    {
        Validate(request.Name, request.Code);
        var now = Now;
        var classroom = new Classroom { TeacherId = teacherId, Name = request.Name.Trim(), Code = NormalizeCode(request.Code), CreatedAtUtc = now, UpdatedAtUtc = now };
        db.Classrooms.Add(classroom);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex)) { throw CodeConflict(); }
        return Map(classroom);
    }

    public async Task<ClassroomDetailsResponse> UpdateAsync(Guid teacherId, Guid id, UpdateClassroomRequest request, CancellationToken ct)
    {
        Validate(request.Name, request.Code);
        var classroom = await OwnedClassroom(teacherId, id).SingleOrDefaultAsync(ct) ?? throw NotFound();
        classroom.Name = request.Name.Trim();
        classroom.Code = NormalizeCode(request.Code);
        classroom.UpdatedAtUtc = Now;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex)) { throw CodeConflict(); }
        return await GetAsync(teacherId, id, ct);
    }

    public async Task ArchiveAsync(Guid teacherId, Guid id, CancellationToken ct)
    {
        var classroom = await OwnedClassroom(teacherId, id).SingleOrDefaultAsync(ct) ?? throw NotFound();
        classroom.Status = ClassroomStatus.Archived;
        classroom.UpdatedAtUtc = Now;
        await db.SaveChangesAsync(ct);
    }

    public async Task AssignModuleAsync(Guid teacherId, Guid classroomId, Guid moduleId, CancellationToken ct)
    {
        var classroom = await OwnedClassroom(teacherId, classroomId).SingleOrDefaultAsync(ct) ?? throw NotFound();
        if (classroom.Status == ClassroomStatus.Archived) throw Conflict("classroom_archived", "Não é possível associar módulos a uma turma arquivada.");
        var module = await db.Modules.SingleOrDefaultAsync(x => x.Id == moduleId && x.TeacherId == teacherId, ct)
            ?? throw NotFound("module_not_found", "O módulo não foi encontrado.");
        if (module.Status != ModuleStatus.Published) throw Conflict("module_not_published", "Somente módulos publicados podem ser associados.");
        if (await db.ClassroomModules.AnyAsync(x => x.ClassroomId == classroomId && x.ModuleId == moduleId, ct))
            throw Conflict("module_already_assigned", "Este módulo já está associado à turma.");
        db.ClassroomModules.Add(new ClassroomModule { ClassroomId = classroomId, ModuleId = moduleId, AssignedAtUtc = Now });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex)) { throw Conflict("module_already_assigned", "Este módulo já está associado à turma."); }
    }

    public async Task UnassignModuleAsync(Guid teacherId, Guid classroomId, Guid moduleId, CancellationToken ct)
    {
        if (!await OwnedClassroom(teacherId, classroomId).AnyAsync(ct)) throw NotFound();
        var link = await db.ClassroomModules.SingleOrDefaultAsync(x => x.ClassroomId == classroomId && x.ModuleId == moduleId && x.Classroom.TeacherId == teacherId && x.Module.TeacherId == teacherId, ct)
            ?? throw NotFound("classroom_module_not_found", "A associação não foi encontrada.");
        db.ClassroomModules.Remove(link);
        await db.SaveChangesAsync(ct);
    }

    private IQueryable<Classroom> OwnedClassroom(Guid teacherId, Guid id) => db.Classrooms.Where(x => x.Id == id && x.TeacherId == teacherId);
    private DateTime Now => timeProvider.GetUtcNow().UtcDateTime;
    private static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();
    private static void Validate(string name, string code)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ApiException(400, "invalid_classroom_name", "O nome da turma é obrigatório.");
        if (string.IsNullOrWhiteSpace(code) || code.Trim().Length is < 3 or > 32 || !System.Text.RegularExpressions.Regex.IsMatch(code.Trim(), "^[A-Za-z0-9]+(?:-[A-Za-z0-9]+)*$"))
            throw new ApiException(400, "invalid_classroom_code", "O código deve possuir de 3 a 32 caracteres: letras, números e hífens entre grupos alfanuméricos.");
    }
    private static ClassroomDetailsResponse Map(Classroom? x)
    {
        if (x is null) throw NotFound();
        return new(x.Id, x.Name, x.Code, x.Status.ToString(), x.CreatedAtUtc, x.UpdatedAtUtc,
            x.ClassroomModules.Where(link => link.Module.TeacherId == x.TeacherId).OrderBy(link => link.Module.Title).Select(link => new ClassroomModuleResponse(
                link.ModuleId, link.Module.Title, link.Module.Subject, link.Module.Version, link.Module.Status.ToString(), link.AssignedAtUtc)).ToArray(), x.Students.Count);
    }
    private static ApiException NotFound(string code = "classroom_not_found", string message = "A turma não foi encontrada.") => new(404, code, message);
    private static ApiException CodeConflict() => Conflict("classroom_code_conflict", "Este código de turma já está em uso.");
    private static ApiException Conflict(string code, string message) => new(409, code, message);
    private static bool IsUniqueViolation(DbUpdateException exception) => exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}

