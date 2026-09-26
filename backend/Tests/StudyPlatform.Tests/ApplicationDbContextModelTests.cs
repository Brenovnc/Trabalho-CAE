using Microsoft.EntityFrameworkCore;
using StudyPlatform.Api.Data;
using StudyPlatform.Api.Models;
using StudyPlatform.Api.Models.Activities;
using Xunit;

namespace StudyPlatform.Tests;

public sealed class ApplicationDbContextModelTests
{
    [Fact]
    public void ClassroomCodeAndStudentEnrollmentHaveDatabaseUniqueIndexes()
    {
        using var context = CreateContext();
        var model = context.Model;

        var classroomCodeIndex = Assert.Single(
            model.FindEntityType(typeof(Classroom))!.GetIndexes(),
            index => index.Properties.Select(property => property.Name)
                .SequenceEqual(new[] { nameof(Classroom.CodeNormalized) }));
        Assert.True(classroomCodeIndex.IsUnique);

        var enrollmentIndex = Assert.Single(
            model.FindEntityType(typeof(Student))!.GetIndexes(),
            index => index.Properties.Select(property => property.Name)
                .SequenceEqual(new[] { nameof(Student.ClassroomId), nameof(Student.EnrollmentNumber) }));
        Assert.True(enrollmentIndex.IsUnique);
    }

    [Fact]
    public void ClassroomModuleUsesCompositePrimaryKey()
    {
        using var context = CreateContext();
        var key = context.Model.FindEntityType(typeof(ClassroomModule))!.FindPrimaryKey();

        Assert.NotNull(key);
        Assert.Equal(
            new[] { nameof(ClassroomModule.ClassroomId), nameof(ClassroomModule.ModuleId) },
            key.Properties.Select(property => property.Name));
    }

    [Fact]
    public void HistoricalAttemptsCannotCascadeDeleteRelatedRecords()
    {
        using var context = CreateContext();
        var attemptType = context.Model.FindEntityType(typeof(ActivityAttempt));

        Assert.NotNull(attemptType);
        Assert.All(attemptType.GetForeignKeys(), foreignKey =>
            Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior));
    }

    [Fact]
    public void PrerequisiteForeignKeysIncludeTheModuleId()
    {
        using var context = CreateContext();
        var prerequisiteType = context.Model.FindEntityType(typeof(ConceptPrerequisite));

        Assert.NotNull(prerequisiteType);
        Assert.Equal(2, prerequisiteType.GetForeignKeys().Count());
        Assert.All(prerequisiteType.GetForeignKeys(), foreignKey =>
            Assert.Equal(
                new[] { nameof(Concept.Id), nameof(Concept.ModuleId) },
                foreignKey.PrincipalKey.Properties.Select(property => property.Name)));
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=trabalho_cae;Username=test;Password=test")
            .Options;

        return new ApplicationDbContext(options);
    }
}