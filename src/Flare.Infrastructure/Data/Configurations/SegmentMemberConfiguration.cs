using Flare.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Flare.Infrastructure.Data.Configurations;

public class SegmentMemberConfiguration : IEntityTypeConfiguration<SegmentMember>
{
    public void Configure(EntityTypeBuilder<SegmentMember> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.TargetingKey).HasMaxLength(512).IsRequired();

        builder.HasIndex(e => new { e.SegmentId, e.TargetingKey }).IsUnique();

        builder.HasOne(e => e.Segment)
            .WithMany(s => s.Members)
            .HasForeignKey(e => e.SegmentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
