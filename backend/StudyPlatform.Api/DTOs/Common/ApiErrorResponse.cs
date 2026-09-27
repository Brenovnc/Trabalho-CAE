using Microsoft.AspNetCore.Mvc;

namespace StudyPlatform.Api.DTOs.Common;

public sealed record ApiErrorResponse(
    int Status,
    string Code,
    string Message,
    IReadOnlyDictionary<string, string[]> Errors);
