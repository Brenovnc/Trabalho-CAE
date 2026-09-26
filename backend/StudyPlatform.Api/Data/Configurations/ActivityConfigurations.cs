using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyPlatform.Api.Domain.Enums;
using StudyPlatform.Api.Models.Activities;

namespace StudyPlatform.Api.Data.Configurations;

public sealed class RecognitionActivityConfiguration : IEntityTypeConfiguration<RecognitionActivity>
{
    public void Configure(EntityTypeBuilder<RecognitionActivity> builder)
    {
        builder.ToTable("RecognitionActivities");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Statement).IsRequired();
        builder.Property(x => x.Explanation).IsRequired();
    }
}

public sealed class FillBlankActivityConfiguration : IEntityTypeConfiguration<FillBlankActivity>
{
    public void Configure(EntityTypeBuilder<FillBlankActivity> builder)
    {
        builder.ToTable("FillBlankActivities");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Text).IsRequired();
        builder.HasMany(x => x.Answers)
            .WithOne(x => x.Activity)
            .HasForeignKey(x => x.FillBlankActivityId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Distractors)
            .WithOne(x => x.Activity)
            .HasForeignKey(x => x.FillBlankActivityId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class FillBlankAnswerConfiguration : IEntityTypeConfiguration<FillBlankAnswer>
{
    public void Configure(EntityTypeBuilder<FillBlankAnswer> builder)
    {
        builder.ToTable("FillBlankAnswers", table =>
        {
            table.HasCheckConstraint("CK_FillBlankAnswers_Slot_Positive", "\"SlotNumber\" > 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CorrectText).HasMaxLength(300).IsRequired();
        builder.HasIndex(x => new { x.FillBlankActivityId, x.SlotNumber }).IsUnique();
    }
}

public sealed class FillBlankDistractorConfiguration : IEntityTypeConfiguration<FillBlankDistractor>
{
    public void Configure(EntityTypeBuilder<FillBlankDistractor> builder)
    {
        builder.ToTable("FillBlankDistractors");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Text).HasMaxLength(300).IsRequired();
        builder.HasIndex(x => new { x.FillBlankActivityId, x.Text }).IsUnique();
    }
}

public sealed class OrderingActivityConfiguration : IEntityTypeConfiguration<OrderingActivity>
{
    public void Configure(EntityTypeBuilder<OrderingActivity> builder)
    {
        builder.ToTable("OrderingActivities");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Instruction).IsRequired();
        builder.HasMany(x => x.Items)
            .WithOne(x => x.Activity)
            .HasForeignKey(x => x.OrderingActivityId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class OrderingActivityItemConfiguration : IEntityTypeConfiguration<OrderingActivityItem>
{
    public void Configure(EntityTypeBuilder<OrderingActivityItem> builder)
    {
        builder.ToTable("OrderingActivityItems", table =>
        {
            table.HasCheckConstraint("CK_OrderingActivityItems_Position_NonNegative", "\"Position\" >= 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Text).IsRequired();
        builder.HasIndex(x => new { x.OrderingActivityId, x.Position }).IsUnique();
    }
}

public sealed class ActivityAttemptConfiguration : IEntityTypeConfiguration<ActivityAttempt>
{
    public void Configure(EntityTypeBuilder<ActivityAttempt> builder)
    {
        builder.ToTable("ActivityAttempts", table =>
        {
            table.HasCheckConstraint(
                "CK_ActivityAttempts_ActivityReferenceMatchesType",
                """
                (
                    ("ActivityType" = 'TrueFalse' AND "RecognitionActivityId" IS NOT NULL AND "FillBlankActivityId" IS NULL AND "OrderingActivityId" IS NULL)
                    OR ("ActivityType" = 'FillBlank' AND "RecognitionActivityId" IS NULL AND "FillBlankActivityId" IS NOT NULL AND "OrderingActivityId" IS NULL)
                    OR ("ActivityType" = 'Ordering' AND "RecognitionActivityId" IS NULL AND "FillBlankActivityId" IS NULL AND "OrderingActivityId" IS NOT NULL)
                    OR ("ActivityType" IN ('Exposure', 'GuessConcept') AND "RecognitionActivityId" IS NULL AND "FillBlankActivityId" IS NULL AND "OrderingActivityId" IS NULL)
                )
                """);
            table.HasCheckConstraint("CK_ActivityAttempts_ResponseTime_NonNegative", "\"ResponseTimeMs\" IS NULL OR \"ResponseTimeMs\" >= 0");
            table.HasCheckConstraint("CK_ActivityAttempts_Attempts_Positive", "\"AttemptsUsed\" > 0");
            table.HasCheckConstraint("CK_ActivityAttempts_Hints_NonNegative", "\"HintsUsed\" >= 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ActivityType).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(x => x.FsrsRating).HasConversion<string>().HasMaxLength(12);
        builder.Property(x => x.AnswerJson).HasColumnType("jsonb");
        builder.Property(x => x.ActivitySnapshotJson).HasColumnType("jsonb").IsRequired();
        builder.HasIndex(x => new { x.StudentId, x.ConceptId, x.CreatedAtUtc });
        builder.HasIndex(x => new { x.StudySessionId, x.CreatedAtUtc });

        builder.HasOne(x => x.RecognitionActivity)
            .WithMany(x => x.Attempts)
            .HasForeignKey(x => x.RecognitionActivityId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.FillBlankActivity)
            .WithMany(x => x.Attempts)
            .HasForeignKey(x => x.FillBlankActivityId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.OrderingActivity)
            .WithMany(x => x.Attempts)
            .HasForeignKey(x => x.OrderingActivityId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}