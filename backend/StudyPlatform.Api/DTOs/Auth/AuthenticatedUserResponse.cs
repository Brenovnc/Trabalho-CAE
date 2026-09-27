using System.Security.Claims;
using StudyPlatform.Api.Models;

namespace StudyPlatform.Api.DTOs.Auth;

public sealed record AuthenticatedUserResponse(
    Guid Id,
    string Role,
    string? Name,
    string? Email,
    Guid? ClassroomId,
    string? EnrollmentNumber)
{
    public static AuthenticatedUserResponse FromTeacher(Teacher teacher) =>
        new(teacher.Id, AuthRoles.Teacher, teacher.Name, teacher.Email, null, null);

    public static AuthenticatedUserResponse FromStudent(Student student) =>
        new(student.Id, AuthRoles.Student, student.Name, null,
            student.ClassroomId, student.EnrollmentNumber);

    public static AuthenticatedUserResponse FromPrincipal(ClaimsPrincipal principal)
    {
        var id = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var role = principal.FindFirstValue(ClaimTypes.Role)!;
        var name = principal.FindFirstValue(ClaimTypes.Name);

        return role switch
        {
            AuthRoles.Teacher => new(id, role, name, principal.FindFirstValue(ClaimTypes.Email), null, null),
            AuthRoles.Student => new(id, role, name, null,
                Guid.Parse(principal.FindFirstValue(AuthClaimTypes.ClassroomId)!),
                principal.FindFirstValue(AuthClaimTypes.EnrollmentNumber)),
            _ => throw new InvalidOperationException("Unknown authenticated user role."),
        };
    }
}

public static class AuthRoles
{
    public const string Teacher = "TEACHER";
    public const string Student = "STUDENT";
}

public static class AuthPolicies
{
    public const string Authenticated = "Authenticated";
    public const string Teacher = "Teacher";
    public const string Student = "Student";
}

public static class AuthClaimTypes
{
    public const string ClassroomId = "classroom_id";
    public const string EnrollmentNumber = "enrollment_number";
}
