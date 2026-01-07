using Flare.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Flare.Infrastructure.Data.Configurations;

public class ScopeConfiguration : IEntityTypeConfiguration<Scope>
{
    public void Configure(EntityTypeBuilder<Scope> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Index).HasMaxLength(255).IsRequired();
        builder.HasIndex(e => new { e.ProjectId, e.Alias }).IsUnique();
        builder.Property(e => e.Alias).HasMaxLength(50).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(255).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(1000);

        builder.HasOne(e => e.Project)
            .WithMany(p => p.Scopes)
            .HasForeignKey(e => e.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
