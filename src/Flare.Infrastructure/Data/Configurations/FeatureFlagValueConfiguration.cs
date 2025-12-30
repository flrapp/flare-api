using Flare.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Flare.Infrastructure.Data.Configurations;

public class FeatureFlagValueConfiguration : IEntityTypeConfiguration<FeatureFlagValue>
{
    public void Configure(EntityTypeBuilder<FeatureFlagValue> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => new { e.FeatureFlagId, e.ScopeId }).IsUnique();

        builder.HasOne(e => e.FeatureFlag)
            .WithMany(f => f.Values)
            .HasForeignKey(e => e.FeatureFlagId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Scope)
            .WithMany(s => s.FeatureFlagValues)
            .HasForeignKey(e => e.ScopeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
