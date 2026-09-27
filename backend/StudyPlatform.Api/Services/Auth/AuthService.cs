using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using StudyPlatform.Api.Data;
using StudyPlatform.Api.DTOs.Auth;
using StudyPlatform.Api.Exceptions;
using StudyPlatform.Api.Models;
using StudyPlatform.Api.Domain.Enums;

namespace StudyPlatform.Api.Services.Auth;

public sealed class AuthService(
    ApplicationDbContext dbContext,
    IPasswordHasher<Teacher> teacherPasswordHasher,
    IPasswordHasher<Student> studentPasswordHasher,
    TimeProvider timeProvider)
{
    private const int MaximumInvalidCodeAttempts = 5;

    public async Task<Teacher> RegisterTeacherAsync(
        TeacherRegisterRequest request,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim();
        var normalizedEmail = email.ToLowerInvariant();
        var emailExists = await dbContext.Teachers
            .AnyAsync(teacher => teacher.EmailNormalized == normalizedEmail, cancellationToken);

        if (emailExists)
        {
            throw EmailConflict();
        }

        var teacher = new Teacher
        {
            Name = request.Name.Trim(),
            Email = email,
        };
        teacher.PasswordHash = teacherPasswordHasher.HashPassword(teacher, request.Password);
        dbContext.Teachers.Add(teacher);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return teacher;
        }
        catch (DbUpdateException exception) when (IsDuplicateEmail(exception))
        {
            throw EmailConflict();
        }
    }

    public async Task<Teacher> AuthenticateTeacherAsync(
        TeacherLoginRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var teacher = await dbContext.Teachers.SingleOrDefaultAsync(
            candidate => candidate.EmailNormalized == normalizedEmail, cancellationToken);

        if (teacher is null || teacherPasswordHasher.VerifyHashedPassword(
                teacher, teacher.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
        {
            throw InvalidCredentials();
        }

        return teacher;
    }

    public async Task<Student> ActivateStudentAsync(
        StudentActivationRequest request,
        CancellationToken cancellationToken)
    {
        var classroomCode = request.ClassroomCode.Trim().ToLowerInvariant();
        var enrollmentNumber = request.EnrollmentNumber.Trim();
        var student = await dbContext.Students
            .Include(candidate => candidate.Classroom)
            .SingleOrDefaultAsync(candidate =>
                candidate.Classroom.CodeNormalized == classroomCode &&
                candidate.Classroom.Status == ClassroomStatus.Active &&
                candidate.EnrollmentNumber == enrollmentNumber,
                cancellationToken);

        if (student is null || student.IsActivated || !student.IsActive ||
            student.TemporaryAccessCodeHash is null ||
            student.TemporaryAccessCodeFailedAttempts >= MaximumInvalidCodeAttempts ||
            student.TemporaryAccessCodeExpiresAtUtc is null ||
            student.TemporaryAccessCodeExpiresAtUtc <= timeProvider.GetUtcNow().UtcDateTime)
        {
            throw InvalidActivation();
        }

        var normalizedCode = request.TemporaryCode.Trim().ToUpperInvariant();
        var codeResult = studentPasswordHasher.VerifyHashedPassword(
            student, student.TemporaryAccessCodeHash, normalizedCode);

        if (codeResult == PasswordVerificationResult.Failed)
        {
            await dbContext.Students
                .Where(candidate => candidate.Id == student.Id &&
                    !candidate.IsActivated && candidate.IsActive &&
                    candidate.TemporaryAccessCodeHash == student.TemporaryAccessCodeHash &&
                    candidate.TemporaryAccessCodeFailedAttempts < MaximumInvalidCodeAttempts)
                .ExecuteUpdateAsync(update => update.SetProperty(
                    candidate => candidate.TemporaryAccessCodeFailedAttempts,
                    candidate => candidate.TemporaryAccessCodeFailedAttempts + 1), cancellationToken);

            throw InvalidActivation();
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var passwordHash = studentPasswordHasher.HashPassword(student, request.Password);
        var activatedRows = await dbContext.Students
            .Where(candidate => candidate.Id == student.Id &&
                !candidate.IsActivated && candidate.IsActive &&
                candidate.TemporaryAccessCodeHash == student.TemporaryAccessCodeHash &&
                candidate.TemporaryAccessCodeFailedAttempts < MaximumInvalidCodeAttempts &&
                candidate.TemporaryAccessCodeExpiresAtUtc > now)
            .ExecuteUpdateAsync(update => update
                .SetProperty(candidate => candidate.PasswordHash, passwordHash)
                .SetProperty(candidate => candidate.TemporaryAccessCodeHash, (string?)null)
                .SetProperty(candidate => candidate.TemporaryAccessCodeExpiresAtUtc, (DateTime?)null)
                .SetProperty(candidate => candidate.IsActivated, true)
                .SetProperty(candidate => candidate.UpdatedAtUtc, now), cancellationToken);

        if (activatedRows != 1)
        {
            throw InvalidActivation();
        }

        student.IsActivated = true;
        student.TemporaryAccessCodeHash = null;
        student.TemporaryAccessCodeExpiresAtUtc = null;
        return student;
    }

    public async Task<Student> AuthenticateStudentAsync(
        StudentLoginRequest request,
        CancellationToken cancellationToken)
    {
        var classroomCode = request.ClassroomCode.Trim().ToLowerInvariant();
        var enrollmentNumber = request.EnrollmentNumber.Trim();
        var student = await dbContext.Students
            .SingleOrDefaultAsync(candidate =>
                candidate.Classroom.CodeNormalized == classroomCode &&
                candidate.Classroom.Status == ClassroomStatus.Active &&
                candidate.EnrollmentNumber == enrollmentNumber,
                cancellationToken);

        if (student is null || !student.IsActive || !student.IsActivated ||
            student.PasswordHash is null ||
            studentPasswordHasher.VerifyHashedPassword(
                student, student.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
        {
            throw InvalidCredentials();
        }

        return student;
    }

    private static ApiException EmailConflict() => new(
        StatusCodes.Status409Conflict,
        "email_already_registered",
        "Já existe uma conta com este e-mail.");

    private static ApiException InvalidCredentials() => new(
        StatusCodes.Status401Unauthorized,
        "invalid_credentials",
        "Credenciais inválidas.");

    private static ApiException InvalidActivation() => new(
        StatusCodes.Status401Unauthorized,
        "invalid_activation",
        "Dados de ativação inválidos ou código expirado.");

    private static bool IsDuplicateEmail(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "IX_Teachers_EmailNormalized",
        };
}
