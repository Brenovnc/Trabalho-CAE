using Microsoft.EntityFrameworkCore;
using Npgsql;
using StudyPlatform.Api.Data;
using StudyPlatform.Api.DTOs.Students;
using StudyPlatform.Api.Exceptions;
using StudyPlatform.Api.Models;

namespace StudyPlatform.Api.Services.Students;

public sealed class StudentManagementService(
    ApplicationDbContext db,
    TemporaryStudentAccessCodeService codes,
    TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<StudentSummaryResponse>> ListAsync(Guid teacherId, Guid classroomId, CancellationToken ct)
    {
        await RequireOwnedClassroom(teacherId, classroomId, ct);
        return await db.Students.AsNoTracking().Where(x => x.ClassroomId == classroomId)
            .OrderBy(x => x.EnrollmentNumber)
            .Select(x => new StudentSummaryResponse(x.Id, x.EnrollmentNumber, x.Name, x.IsActive, x.IsActivated, x.CreatedAtUtc)).ToListAsync(ct);
    }

    public async Task<StudentCredentialsResponse> CreateAsync(Guid teacherId, Guid classroomId, CreateStudentRequest request, CancellationToken ct)
    {
        var classroom = await RequireOwnedClassroom(teacherId, classroomId, ct);
        if (classroom.Status != Domain.Enums.ClassroomStatus.Active) throw Conflict("classroom_archived", "Não é possível cadastrar alunos em uma turma arquivada.");
        if (string.IsNullOrWhiteSpace(request.EnrollmentNumber)) throw BadRequest("invalid_enrollment_number", "A matrícula é obrigatória.");
        var enrollment = request.EnrollmentNumber.Trim();
        if (await db.Students.AnyAsync(x => x.ClassroomId == classroomId && x.EnrollmentNumber == enrollment, ct)) throw EnrollmentConflict();
        var student = new Student { ClassroomId = classroomId, EnrollmentNumber = enrollment, Name = CleanName(request.Name), IsActive = true, IsActivated = false, PasswordHash = null, CreatedAtUtc = Now, UpdatedAtUtc = Now };
        var credential = codes.Create(student);
        student.TemporaryAccessCodeHash = credential.Hash;
        student.TemporaryAccessCodeExpiresAtUtc = credential.ExpiresAtUtc;
        student.TemporaryAccessCodeFailedAttempts = 0;
        db.Students.Add(student);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex)) { throw EnrollmentConflict(); }
        return new(student.Id, student.EnrollmentNumber, student.Name, credential.PlainText, credential.ExpiresAtUtc);
    }

    public async Task<StudentCredentialsResponse> ResetAccessAsync(Guid teacherId, Guid classroomId, Guid studentId, CancellationToken ct)
    {
        var student = await OwnedStudent(teacherId, classroomId, studentId).SingleOrDefaultAsync(ct) ?? throw StudentNotFound();
        if (!student.IsActive) throw Conflict("student_inactive", "Não é possível redefinir o acesso de um aluno desativado.");
        if (student.Classroom.Status != Domain.Enums.ClassroomStatus.Active) throw Conflict("classroom_archived", "Não é possível redefinir acessos em uma turma arquivada.");
        var credential = codes.Create(student);
        student.PasswordHash = null;
        student.IsActivated = false;
        student.TemporaryAccessCodeHash = credential.Hash;
        student.TemporaryAccessCodeExpiresAtUtc = credential.ExpiresAtUtc;
        student.TemporaryAccessCodeFailedAttempts = 0;
        student.UpdatedAtUtc = Now;
        await db.SaveChangesAsync(ct);
        return new(student.Id, student.EnrollmentNumber, student.Name, credential.PlainText, credential.ExpiresAtUtc);
    }

    public async Task DeactivateAsync(Guid teacherId, Guid classroomId, Guid studentId, CancellationToken ct)
    {
        var student = await OwnedStudent(teacherId, classroomId, studentId).SingleOrDefaultAsync(ct) ?? throw StudentNotFound();
        student.IsActive = false;
        student.PasswordHash = null;
        student.TemporaryAccessCodeHash = null;
        student.TemporaryAccessCodeExpiresAtUtc = null;
        student.UpdatedAtUtc = Now;
        await db.SaveChangesAsync(ct);
    }

    private async Task<Classroom> RequireOwnedClassroom(Guid teacherId, Guid classroomId, CancellationToken ct) =>
        await db.Classrooms.SingleOrDefaultAsync(x => x.Id == classroomId && x.TeacherId == teacherId, ct) ?? throw new ApiException(404, "classroom_not_found", "A turma não foi encontrada.");
    private IQueryable<Student> OwnedStudent(Guid teacherId, Guid classroomId, Guid studentId) =>
        db.Students.Include(x => x.Classroom).Where(x => x.Id == studentId && x.ClassroomId == classroomId && x.Classroom.TeacherId == teacherId);
    private DateTime Now => timeProvider.GetUtcNow().UtcDateTime;
    private static string? CleanName(string? name) => string.IsNullOrWhiteSpace(name) ? null : name.Trim();
    private static ApiException StudentNotFound() => new(404, "student_not_found", "O aluno não foi encontrado nesta turma.");
    private static ApiException EnrollmentConflict() => new(409, "enrollment_already_exists", "Esta matrícula já existe na turma.");
    private static ApiException BadRequest(string code, string message) => new(400, code, message);
    private static ApiException Conflict(string code, string message) => new(409, code, message);
    private static bool IsUniqueViolation(DbUpdateException exception) => exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}

