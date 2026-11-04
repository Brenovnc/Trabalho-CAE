using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyPlatform.Api.Models;

namespace StudyPlatform.Api.Data.Configurations;
public sealed class CommunityModuleConfiguration : IEntityTypeConfiguration<Module>
{
    public void Configure(EntityTypeBuilder<Module> builder)
    {
        builder.Property(x => x.CommunityOriginAuthor).HasMaxLength(160);
        builder.HasIndex(x => new { x.IsCommunityListing, x.SharedAtUtc });
    }
}
