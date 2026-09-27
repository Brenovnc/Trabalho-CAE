using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using StudyPlatform.Api.DTOs.Common;

namespace StudyPlatform.Api.Services.Auth;

public sealed class ApiCookieAuthenticationEvents : CookieAuthenticationEvents
{
    public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context) =>
        WriteErrorAsync(context.HttpContext, StatusCodes.Status401Unauthorized,
            "unauthenticated", "É necessário autenticar-se.");

    public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context) =>
        WriteErrorAsync(context.HttpContext, StatusCodes.Status403Forbidden,
            "forbidden", "Você não tem permissão para realizar esta operação.");

    private static Task WriteErrorAsync(HttpContext context, int status, string code, string message)
    {
        context.Response.StatusCode = status;
        return context.Response.WriteAsJsonAsync(new ApiErrorResponse(
            status, code, message, new Dictionary<string, string[]>()));
    }
}
