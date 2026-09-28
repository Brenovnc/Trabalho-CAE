using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using StudyPlatform.Api.Data;
using StudyPlatform.Api.DTOs.Auth;
using StudyPlatform.Api.DTOs.Common;
using StudyPlatform.Api.Domain.Enums;

namespace StudyPlatform.Api.Services.Auth;

public sealed class ApiCookieAuthenticationEvents(ApplicationDbContext dbContext, TimeProvider timeProvider) : CookieAuthenticationEvents
{
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        if (context.Principal?.FindFirstValue(ClaimTypes.Role) != AuthRoles.Student) return;
        var studentId = context.Principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var student = Guid.TryParse(studentId, out var id)
            ? await dbContext.Students.Include(item => item.Classroom).SingleOrDefaultAsync(item => item.Id == id)
            : null;
        if (student is { IsActive: true, IsActivated: true } && student.Classroom.Status == ClassroomStatus.Active)
        {
            student.LastAccessAtUtc = timeProvider.GetUtcNow().UtcDateTime;
            await dbContext.SaveChangesAsync(context.HttpContext.RequestAborted);
            return;
        }

        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }

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
