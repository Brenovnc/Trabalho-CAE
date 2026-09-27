using Microsoft.AspNetCore.Antiforgery;
using StudyPlatform.Api.DTOs.Common;

namespace StudyPlatform.Api.Middleware;

public sealed class CsrfValidationMiddleware(RequestDelegate next, IAntiforgery antiforgery)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (IsStateChangingMethod(context.Request.Method) &&
            context.Request.Path.StartsWithSegments("/api") &&
            !await antiforgery.IsRequestValidAsync(context))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new ApiErrorResponse(
                StatusCodes.Status400BadRequest,
                "csrf_validation_failed",
                "Token de segurança ausente ou inválido. Atualize a página e tente novamente.",
                new Dictionary<string, string[]>()));
            return;
        }

        await next(context);
    }

    private static bool IsStateChangingMethod(string method) =>
        HttpMethods.IsPost(method) || HttpMethods.IsPut(method) ||
        HttpMethods.IsPatch(method) || HttpMethods.IsDelete(method);
}
