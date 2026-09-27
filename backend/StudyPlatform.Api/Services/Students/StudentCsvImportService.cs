using System.Text;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using StudyPlatform.Api.Data;
using StudyPlatform.Api.Domain.Enums;
using StudyPlatform.Api.DTOs.Students;
using StudyPlatform.Api.Exceptions;
using StudyPlatform.Api.Models;

namespace StudyPlatform.Api.Services.Students;

public sealed class StudentCsvImportService(
    ApplicationDbContext db,
    CsvStudentParser parser,
    TemporaryStudentAccessCodeService codes,
    TimeProvider timeProvider)
{
    private const int EnrollmentMaxLength = 64;
    private const int NameMaxLength = 120;

    public async Task<StudentCsvPreviewResponse> PreviewAsync(Guid teacherId, Guid classroomId, byte[] file, CancellationToken ct)
    {
        var classroom = await RequireClassroom(teacherId, classroomId, ct);
        EnsureActive(classroom);
        var rows = await ValidateRows(classroomId, parser.Parse(file), ct);
        return Summarize(rows);
    }

    public async Task<StudentCsvImportResultResponse> ConfirmAsync(Guid teacherId, Guid classroomId, byte[] file, CancellationToken ct)
    {
        var classroom = await RequireClassroom(teacherId, classroomId, ct);
        EnsureActive(classroom);
        var validated = (await ValidateRows(classroomId, parser.Parse(file), ct)).ToList();
        var createdCredentials = new List<(string Enrollment, string? Name, string Code)>();

        foreach (var row in validated.Where(row => row.IsValid).ToArray())
        {
            // Refresh the existence check immediately before each insert; the unique index remains the race-condition guard.
            if (await db.Students.AnyAsync(x => x.ClassroomId == classroomId && x.EnrollmentNumber == row.EnrollmentNumber, ct))
            {
                Replace(row, "Esta matrícula já existe na turma.");
                continue;
            }

            var student = new Student
            {
                ClassroomId = classroomId,
                EnrollmentNumber = row.EnrollmentNumber,
                Name = row.Name,
                IsActive = true,
                IsActivated = false,
                PasswordHash = null,
                TemporaryAccessCodeFailedAttempts = 0,
                CreatedAtUtc = Now,
                UpdatedAtUtc = Now,
            };
            var credential = codes.Create(student);
            student.TemporaryAccessCodeHash = credential.Hash;
            student.TemporaryAccessCodeExpiresAtUtc = credential.ExpiresAtUtc;
            db.Students.Add(student);
            try
            {
                await db.SaveChangesAsync(ct);
                createdCredentials.Add((student.EnrollmentNumber, student.Name, credential.PlainText));
            }
            catch (DbUpdateException ex) when (IsEnrollmentUniqueViolation(ex))
            {
                db.Entry(student).State = EntityState.Detached;
                Replace(row, "Esta matrícula já foi cadastrada durante a importação.");
            }
        }

        return new StudentCsvImportResultResponse(
            createdCredentials.Count,
            validated.Where(row => !row.IsValid).Select(ToResponse).ToArray(),
            BuildCredentialsCsv(createdCredentials));
    }

    private async Task<Classroom> RequireClassroom(Guid teacherId, Guid classroomId, CancellationToken ct) =>
        await db.Classrooms.SingleOrDefaultAsync(x => x.Id == classroomId && x.TeacherId == teacherId, ct)
        ?? throw new ApiException(404, "classroom_not_found", "A turma não foi encontrada.");

    private static void EnsureActive(Classroom classroom)
    {
        if (classroom.Status != ClassroomStatus.Active)
            throw new ApiException(409, "classroom_archived", "Não é possível importar alunos em uma turma arquivada.");
    }

    private async Task<IReadOnlyList<ValidatedRow>> ValidateRows(Guid classroomId, IReadOnlyList<ParsedStudentCsvRow> parsed, CancellationToken ct)
    {
        var existing = await db.Students.AsNoTracking().Where(x => x.ClassroomId == classroomId)
            .Select(x => x.EnrollmentNumber).ToListAsync(ct);
        var existingSet = existing.ToHashSet(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var rows = new List<ValidatedRow>(parsed.Count);
        foreach (var source in parsed)
        {
            var errors = new List<string>();
            if (source.StructuralError is not null) errors.Add(source.StructuralError);
            if (string.IsNullOrWhiteSpace(source.EnrollmentNumber)) errors.Add("Matrícula é obrigatória.");
            else
            {
                if (source.EnrollmentNumber.Length > EnrollmentMaxLength) errors.Add($"Matrícula deve ter no máximo {EnrollmentMaxLength} caracteres.");
                if (!seen.Add(source.EnrollmentNumber)) errors.Add("Matrícula duplicada neste arquivo.");
                if (existingSet.Contains(source.EnrollmentNumber)) errors.Add("Esta matrícula já existe na turma.");
            }
            if (source.Name?.Length > NameMaxLength) errors.Add($"Nome deve ter no máximo {NameMaxLength} caracteres.");
            rows.Add(new ValidatedRow(source.LineNumber, source.EnrollmentNumber, source.Name, errors));
        }
        return rows;
    }

    private static StudentCsvPreviewResponse Summarize(IReadOnlyList<ValidatedRow> rows)
    {
        var responses = rows.Select(ToResponse).ToArray();
        var valid = responses.Count(x => x.IsValid);
        return new StudentCsvPreviewResponse(responses.Length, valid, responses.Length - valid, responses);
    }

    private static StudentCsvPreviewRowResponse ToResponse(ValidatedRow row) =>
        new(row.LineNumber, row.EnrollmentNumber, row.Name, row.Errors.Count == 0, row.Errors.ToArray());

    private static void Replace(ValidatedRow row, string error) => row.Errors.Add(error);

    private static string BuildCredentialsCsv(IEnumerable<(string Enrollment, string? Name, string Code)> rows)
    {
        var output = new StringBuilder("matricula,nome,codigo_temporario\r\n");
        foreach (var row in rows)
            output.Append(Escape(row.Enrollment)).Append(',').Append(Escape(row.Name ?? string.Empty)).Append(',').Append(Escape(row.Code)).Append("\r\n");
        return output.ToString();
    }

    private static string Escape(string value) => value.IndexOfAny([',', '"', '\r', '\n']) < 0
        ? value
        : $"\"{value.Replace("\"", "\"\"")}\"";

    private static bool IsEnrollmentUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_Students_ClassroomId_EnrollmentNumber" };

    private DateTime Now => timeProvider.GetUtcNow().UtcDateTime;
    private sealed record ValidatedRow(int LineNumber, string EnrollmentNumber, string? Name, List<string> Errors)
    {
        public bool IsValid => Errors.Count == 0;
        public ValidatedRow(int lineNumber, string enrollmentNumber, string? name, IEnumerable<string> errors)
            : this(lineNumber, enrollmentNumber, name, errors.ToList()) { }
    }
}
