using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyPlatform.Api.DTOs.Auth;
using StudyPlatform.Api.DTOs.Students;
using StudyPlatform.Api.Exceptions;
using StudyPlatform.Api.Services.Students;

namespace StudyPlatform.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthPolicies.Teacher)]
[Route("api/classrooms/{classroomId:guid}/students/import")]
public sealed class StudentCsvImportController(StudentCsvImportService imports) : ControllerBase
{
    [HttpPost("preview")]
    [RequestSizeLimit(1_200_000)]
    public async Task<ActionResult<StudentCsvPreviewResponse>> Preview(Guid classroomId, IFormFile? file, CancellationToken ct)
    {
        var bytes = await ReadCsv(file, ct);
        return Ok(await imports.PreviewAsync(TeacherId, classroomId, bytes, ct));
    }

    [HttpPost("confirm")]
    [RequestSizeLimit(1_200_000)]
    public async Task<ActionResult<StudentCsvImportResultResponse>> Confirm(Guid classroomId, IFormFile? file, CancellationToken ct)
    {
        var bytes = await ReadCsv(file, ct);
        return Ok(await imports.ConfirmAsync(TeacherId, classroomId, bytes, ct));
    }

    private static async Task<byte[]> ReadCsv(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) throw new ApiException(400, "csv_file_required", "Selecione um arquivo CSV.");
        if (!string.Equals(Path.GetExtension(file.FileName), ".csv", StringComparison.OrdinalIgnoreCase)) throw new ApiException(400, "invalid_csv_extension", "Selecione um arquivo com extensão .csv.");
        if (file.Length > CsvStudentParser.MaxFileBytes) throw new ApiException(413, "csv_file_too_large", "O arquivo CSV deve ter no máximo 1 MiB.");
        await using var stream = new MemoryStream((int)file.Length);
        await file.CopyToAsync(stream, ct);
        return stream.ToArray();
    }

    private Guid TeacherId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
