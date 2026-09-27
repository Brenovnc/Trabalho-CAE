namespace StudyPlatform.Api.DTOs.Students;

public sealed record StudentCsvPreviewResponse(
    int TotalRows,
    int ValidRows,
    int InvalidRows,
    IReadOnlyList<StudentCsvPreviewRowResponse> Rows);

public sealed record StudentCsvPreviewRowResponse(
    int LineNumber,
    string EnrollmentNumber,
    string? Name,
    bool IsValid,
    IReadOnlyList<string> Errors);

public sealed record StudentCsvImportResultResponse(
    int CreatedCount,
    IReadOnlyList<StudentCsvPreviewRowResponse> SkippedRows,
    string CredentialsCsv);
