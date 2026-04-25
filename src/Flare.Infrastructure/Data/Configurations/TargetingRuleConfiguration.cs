using Flare.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Flare.Infrastructure.Data.Configurations;

public class TargetingRuleConfiguration : IEntityTypeConfiguration<TargetingRule>
{
    public void Configure(EntityTypeBuilder<TargetingRule> builder)
    {
        builder.HasKey(e => e.Id);

        builder.HasIndex(e => new { e.FeatureFlagValueId, e.Priority });

        builder.Property(e => e.ServeJsonValue).HasColumnType("text");

        builder.HasOne(e => e.FeatureFlagValue)
            .WithMany(v => v.TargetingRules)
            .HasForeignKey(e => e.FeatureFlagValueId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
