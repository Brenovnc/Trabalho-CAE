using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using StudyPlatform.Api.DTOs.Auth;
using StudyPlatform.Api.Models;

namespace StudyPlatform.Api.Services.Auth;

public static class AuthPrincipalFactory
{
    public static ClaimsPrincipal ForTeacher(Teacher teacher) => Create(
    [
        new Claim(ClaimTypes.NameIdentifier, teacher.Id.ToString()),
        new Claim(ClaimTypes.Name, teacher.Name),
        new Claim(ClaimTypes.Email, teacher.Email),
        new Claim(ClaimTypes.Role, AuthRoles.Teacher),
    ]);

    public static ClaimsPrincipal ForStudent(Student student) => Create(
    [
        new Claim(ClaimTypes.NameIdentifier, student.Id.ToString()),
        new Claim(ClaimTypes.Name, student.Name ?? string.Empty),
        new Claim(ClaimTypes.Role, AuthRoles.Student),
        new Claim(AuthClaimTypes.ClassroomId, student.ClassroomId.ToString()),
        new Claim(AuthClaimTypes.EnrollmentNumber, student.EnrollmentNumber),
    ]);

    private static ClaimsPrincipal Create(IEnumerable<Claim> claims) => new(
        new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme,
            ClaimTypes.Name, ClaimTypes.Role));
}
