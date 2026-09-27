using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using StudyPlatform.Api.Models;

namespace StudyPlatform.Api.Services.Students;

public sealed class TemporaryStudentAccessCodeService(
    IPasswordHasher<Student> passwordHasher,
    TimeProvider timeProvider)
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    public (string PlainText, string Hash, DateTime ExpiresAtUtc) Create(Student student)
    {
        Span<char> code = stackalloc char[6];
        for (var index = 0; index < code.Length; index++)
            code[index] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        var plainText = new string(code);
        return (plainText, passwordHasher.HashPassword(student, plainText), timeProvider.GetUtcNow().UtcDateTime.Add(Lifetime));
    }
}
