using Flare.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Flare.Infrastructure.Data.Configurations;

public class TargetingConditionConfiguration : IEntityTypeConfiguration<TargetingCondition>
{
    public void Configure(EntityTypeBuilder<TargetingCondition> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.AttributeKey).HasMaxLength(255).IsRequired();
        builder.Property(e => e.Value).IsRequired();

        builder.Property(e => e.Operator)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.HasOne(e => e.TargetingRule)
            .WithMany(r => r.Conditions)
            .HasForeignKey(e => e.TargetingRuleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
