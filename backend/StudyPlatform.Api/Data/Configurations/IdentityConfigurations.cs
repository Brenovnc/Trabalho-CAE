using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyPlatform.Api.Models;

namespace StudyPlatform.Api.Data.Configurations;

public sealed class TeacherConfiguration : IEntityTypeConfiguration<Teacher>
{
    public void Configure(EntityTypeBuilder<Teacher> builder)
    {
        builder.ToTable("Teachers");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(320).IsRequired();
        builder.Property(x => x.EmailNormalized)
            .HasComputedColumnSql("lower(\"Email\")", stored: true);
        builder.Property(x => x.PasswordHash).IsRequired();
        builder.HasIndex(x => x.EmailNormalized).IsUnique();

        builder.HasMany(x => x.Modules)
            .WithOne(x => x.Teacher)
            .HasForeignKey(x => x.TeacherId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Classrooms)
            .WithOne(x => x.Teacher)
            .HasForeignKey(x => x.TeacherId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> builder)
    {
        builder.ToTable("Students", table =>
        {
            table.HasCheckConstraint(
                "CK_Students_TemporaryAccessCodeFailedAttempts_NonNegative",
                "\"TemporaryAccessCodeFailedAttempts\" >= 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EnrollmentNumber).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(120);
        builder.Property(x => x.PasswordHash);
        builder.Property(x => x.TemporaryAccessCodeHash);
        builder.HasIndex(x => new { x.ClassroomId, x.EnrollmentNumber }).IsUnique();

        builder.HasOne(x => x.Classroom)
            .WithMany(x => x.Students)
            .HasForeignKey(x => x.ClassroomId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.ConceptStates)
            .WithOne(x => x.Student)
            .HasForeignKey(x => x.StudentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.StudySessions)
            .WithOne(x => x.Student)
            .HasForeignKey(x => x.StudentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.ActivityAttempts)
            .WithOne(x => x.Student)
            .HasForeignKey(x => x.StudentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ClassroomConfiguration : IEntityTypeConfiguration<Classroom>
{
    public void Configure(EntityTypeBuilder<Classroom> builder)
    {
        builder.ToTable("Classrooms");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(160).IsRequired();
        builder.Property(x => x.Code).HasMaxLength(32).IsRequired();
        builder.Property(x => x.CodeNormalized)
            .HasComputedColumnSql("lower(\"Code\")", stored: true);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.HasIndex(x => x.CodeNormalized).IsUnique();

        builder.HasOne(x => x.Teacher)
            .WithMany(x => x.Classrooms)
            .HasForeignKey(x => x.TeacherId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Students)
            .WithOne(x => x.Classroom)
            .HasForeignKey(x => x.ClassroomId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.ClassroomModules)
            .WithOne(x => x.Classroom)
            .HasForeignKey(x => x.ClassroomId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ModuleConfiguration : IEntityTypeConfiguration<Module>
{
    public void Configure(EntityTypeBuilder<Module> builder)
    {
        builder.ToTable("Modules", table =>
        {
            table.HasCheckConstraint("CK_Modules_Version_Positive", "\"Version\" > 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(160).IsRequired();
        builder.Property(x => x.Description);
        builder.Property(x => x.Subject).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.HasIndex(x => x.TeacherId);

        builder.HasOne(x => x.Teacher)
            .WithMany(x => x.Modules)
            .HasForeignKey(x => x.TeacherId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Concepts)
            .WithOne(x => x.Module)
            .HasForeignKey(x => x.ModuleId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.ClassroomModules)
            .WithOne(x => x.Module)
            .HasForeignKey(x => x.ModuleId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.StudySessions)
            .WithOne(x => x.Module)
            .HasForeignKey(x => x.ModuleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ClassroomModuleConfiguration : IEntityTypeConfiguration<ClassroomModule>
{
    public void Configure(EntityTypeBuilder<ClassroomModule> builder)
    {
        builder.ToTable("ClassroomModules");
        builder.HasKey(x => new { x.ClassroomId, x.ModuleId });
    }
}