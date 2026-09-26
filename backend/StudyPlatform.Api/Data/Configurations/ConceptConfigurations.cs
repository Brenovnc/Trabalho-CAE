using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyPlatform.Api.Models;

namespace StudyPlatform.Api.Data.Configurations;

public sealed class ConceptConfiguration : IEntityTypeConfiguration<Concept>
{
    public void Configure(EntityTypeBuilder<Concept> builder)
    {
        builder.ToTable("Concepts");
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.Id, x.ModuleId })
            .HasName("AK_Concepts_Id_ModuleId");
        builder.Property(x => x.ExternalId).HasMaxLength(80);
        builder.Property(x => x.Name).HasMaxLength(160).IsRequired();
        builder.Property(x => x.Definition).IsRequired();
        builder.HasIndex(x => x.ModuleId);
        builder.HasIndex(x => new { x.ModuleId, x.ExternalId })
            .IsUnique()
            .HasFilter("\"ExternalId\" IS NOT NULL");

        builder.HasOne(x => x.Module)
            .WithMany(x => x.Concepts)
            .HasForeignKey(x => x.ModuleId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Prerequisites)
            .WithOne(x => x.Concept)
            .HasForeignKey(x => new { x.ConceptId, x.ModuleId })
            .HasPrincipalKey(x => new { x.Id, x.ModuleId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.RequiredBy)
            .WithOne(x => x.PrerequisiteConcept)
            .HasForeignKey(x => new { x.PrerequisiteConceptId, x.ModuleId })
            .HasPrincipalKey(x => new { x.Id, x.ModuleId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Keywords)
            .WithOne(x => x.Concept)
            .HasForeignKey(x => x.ConceptId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Clues)
            .WithOne(x => x.Concept)
            .HasForeignKey(x => x.ConceptId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.RecognitionActivities)
            .WithOne(x => x.Concept)
            .HasForeignKey(x => x.ConceptId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.FillBlankActivities)
            .WithOne(x => x.Concept)
            .HasForeignKey(x => x.ConceptId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.OrderingActivities)
            .WithOne(x => x.Concept)
            .HasForeignKey(x => x.ConceptId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.StudentStates)
            .WithOne(x => x.Concept)
            .HasForeignKey(x => x.ConceptId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.ActivityAttempts)
            .WithOne(x => x.Concept)
            .HasForeignKey(x => x.ConceptId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ConceptPrerequisiteConfiguration : IEntityTypeConfiguration<ConceptPrerequisite>
{
    public void Configure(EntityTypeBuilder<ConceptPrerequisite> builder)
    {
        builder.ToTable("ConceptPrerequisites", table =>
        {
            table.HasCheckConstraint(
                "CK_ConceptPrerequisites_NoSelfReference",
                "\"ConceptId\" <> \"PrerequisiteConceptId\"");
        });
        builder.HasKey(x => new { x.ConceptId, x.PrerequisiteConceptId });
        builder.HasIndex(x => new { x.PrerequisiteConceptId, x.ModuleId });
    }
}

public sealed class ConceptKeywordConfiguration : IEntityTypeConfiguration<ConceptKeyword>
{
    public void Configure(EntityTypeBuilder<ConceptKeyword> builder)
    {
        builder.ToTable("ConceptKeywords");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Value).HasMaxLength(200).IsRequired();
        builder.HasIndex(x => new { x.ConceptId, x.Value }).IsUnique();
    }
}

public sealed class ConceptClueConfiguration : IEntityTypeConfiguration<ConceptClue>
{
    public void Configure(EntityTypeBuilder<ConceptClue> builder)
    {
        builder.ToTable("ConceptClues", table =>
        {
            table.HasCheckConstraint("CK_ConceptClues_Position_NonNegative", "\"Position\" >= 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Text).HasMaxLength(1000).IsRequired();
        builder.HasIndex(x => new { x.ConceptId, x.Position }).IsUnique();
    }
}