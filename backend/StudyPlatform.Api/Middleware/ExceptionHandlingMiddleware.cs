using StudyPlatform.Api.DTOs.Common;
using StudyPlatform.Api.Exceptions;

namespace StudyPlatform.Api.Middleware;

public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ApiException exception)
        {
            await WriteErrorAsync(context, exception.Status, exception.Code,
                exception.Message, exception.Errors);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unexpected error while processing the request.");
            await WriteErrorAsync(context, StatusCodes.Status500InternalServerError,
                "internal_error", "Ocorreu um erro interno.",
                new Dictionary<string, string[]>());
        }
    }

    private static async Task WriteErrorAsync(
        HttpContext context,
        int status,
        string code,
        string message,
        IReadOnlyDictionary<string, string[]> errors)
    {
        if (context.Response.HasStarted)
        {
            throw new InvalidOperationException("The response has already started.");
        }

        context.Response.Clear();
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json; charset=utf-8";
        await context.Response.WriteAsJsonAsync(
            new ApiErrorResponse(status, code, message, errors),
            cancellationToken: context.RequestAborted);
    }
}
