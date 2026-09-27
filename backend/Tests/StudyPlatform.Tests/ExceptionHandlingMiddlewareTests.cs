using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using StudyPlatform.Api.DTOs.Common;
using StudyPlatform.Api.Exceptions;
using StudyPlatform.Api.Middleware;
using System.Text.Json;
using Xunit;

namespace StudyPlatform.Tests;

public sealed class ExceptionHandlingMiddlewareTests
{
    [Fact]
    public async Task KnownApiExceptionReturnsItsStatusAndConsistentBody()
    {
        var context = CreateContext();
        var exception = new ApiException(
            StatusCodes.Status409Conflict,
            "conflict",
            "A resource conflicts with the current state.");
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw exception,
            NullLogger<ExceptionHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);
        var response = await ReadResponseAsync(context);

        Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);
        Assert.Equal("conflict", response.Code);
        Assert.Equal(exception.Message, response.Message);
        Assert.Empty(response.Errors);
    }

    [Fact]
    public async Task UnexpectedExceptionDoesNotExposeInternalDetails()
    {
        var context = CreateContext();
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new InvalidOperationException("secret database detail"),
            NullLogger<ExceptionHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);
        var body = await ReadRawResponseAsync(context);
        var response = JsonSerializer.Deserialize<ApiErrorResponse>(body,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.Contains("internal_error", body);
        Assert.DoesNotContain("secret database detail", body);
        Assert.DoesNotContain("StackTrace", body);
        Assert.Equal("Ocorreu um erro interno.", response!.Message);
    }

    private static DefaultHttpContext CreateContext()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<ApiErrorResponse> ReadResponseAsync(HttpContext context)
    {
        var body = await ReadRawResponseAsync(context);
        return JsonSerializer.Deserialize<ApiErrorResponse>(body,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }

    private static async Task<string> ReadRawResponseAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        return await reader.ReadToEndAsync();
    }
}

