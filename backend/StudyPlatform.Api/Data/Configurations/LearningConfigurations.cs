using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyPlatform.Api.Models;

namespace StudyPlatform.Api.Data.Configurations;

public sealed class StudentConceptStateConfiguration : IEntityTypeConfiguration<StudentConceptState>
{
    public void Configure(EntityTypeBuilder<StudentConceptState> builder)
    {
        builder.ToTable("StudentConceptStates", table =>
        {
            table.HasCheckConstraint("CK_StudentConceptStates_FreeRecallSuccessCount_NonNegative", "\"FreeRecallSuccessCount\" >= 0");
            table.HasCheckConstraint("CK_StudentConceptStates_Repetitions_NonNegative", "\"Repetitions\" >= 0");
            table.HasCheckConstraint("CK_StudentConceptStates_Lapses_NonNegative", "\"Lapses\" >= 0");
            table.HasCheckConstraint("CK_StudentConceptStates_Difficulty_NonNegative", "\"Difficulty\" IS NULL OR \"Difficulty\" >= 0");
            table.HasCheckConstraint("CK_StudentConceptStates_Stability_NonNegative", "\"Stability\" IS NULL OR \"Stability\" >= 0");
        });
        builder.HasKey(x => new { x.StudentId, x.ConceptId });
        builder.Property(x => x.LearningState).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.FsrsState).HasMaxLength(24);
        builder.Property(x => x.LastFsrsRating).HasConversion<string>().HasMaxLength(12);
        builder.HasIndex(x => x.DueAtUtc);
        builder.HasIndex(x => x.ConceptId);
        builder.HasOne(x => x.LastFreeRecallSuccessSession).WithMany(x => x.LastFreeRecallSuccessStates)
            .HasForeignKey(x => x.LastFreeRecallSuccessSessionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class StudySessionConfiguration : IEntityTypeConfiguration<StudySession>
{
    public void Configure(EntityTypeBuilder<StudySession> builder)
    {
        builder.ToTable("StudySessions", table => table.HasCheckConstraint(
            "CK_StudySessions_ActivityCounts",
            "\"TotalActivities\" >= 0 AND \"TotalActivities\" <= 10 AND \"CompletedActivities\" >= 0 AND \"CompletedActivities\" <= \"TotalActivities\""));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.HasIndex(x => new { x.StudentId, x.ModuleId, x.StartedAtUtc });
        builder.HasMany(x => x.Attempts).WithOne(x => x.StudySession).HasForeignKey(x => x.StudySessionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Presentations).WithOne(x => x.StudySession).HasForeignKey(x => x.StudySessionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class SessionActivityPresentationConfiguration : IEntityTypeConfiguration<SessionActivityPresentation>
{
    public void Configure(EntityTypeBuilder<SessionActivityPresentation> builder)
    {
        builder.ToTable("SessionActivityPresentations", table =>
        {
            table.HasCheckConstraint("CK_SessionActivityPresentations_Sequence_Positive", "\"SequenceNumber\" > 0");
            table.HasCheckConstraint("CK_SessionActivityPresentations_RevealedClues_NonNegative", "\"RevealedClueCount\" >= 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ActivityType).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(x => x.SnapshotJson).HasColumnType("jsonb").IsRequired();
        builder.HasIndex(x => new { x.StudySessionId, x.SequenceNumber }).IsUnique();
        builder.HasIndex(x => new { x.StudySessionId, x.AnsweredAtUtc });
        builder.HasOne(x => x.Concept).WithMany().HasForeignKey(x => x.ConceptId).OnDelete(DeleteBehavior.Restrict);
    }
}
