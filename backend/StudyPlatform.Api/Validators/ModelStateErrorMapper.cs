using Microsoft.AspNetCore.Mvc.ModelBinding;
using StudyPlatform.Api.DTOs.Common;

namespace StudyPlatform.Api.Validators;

public static class ModelStateErrorMapper
{
    public static ApiErrorResponse CreateResponse(ModelStateDictionary modelState)
    {
        var errors = modelState
            .Where(entry => entry.Value?.Errors.Count > 0)
            .ToDictionary(
                entry => string.IsNullOrWhiteSpace(entry.Key) ? "request" : entry.Key,
                entry => entry.Value!.Errors
                    .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage)
                        ? "O valor informado é inválido."
                        : error.ErrorMessage)
                    .ToArray());

        return new ApiErrorResponse(
            Status: StatusCodes.Status400BadRequest,
            Code: "validation_error",
            Message: "Um ou mais campos são inválidos.",
            Errors: errors);
    }
}
