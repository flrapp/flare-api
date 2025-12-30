using Flare.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Flare.Infrastructure.Data.Configurations;

public class ProjectUserScopePermissionConfiguration : IEntityTypeConfiguration<ProjectUserScopePermission>
{
    public void Configure(EntityTypeBuilder<ProjectUserScopePermission> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => new { e.ProjectUserId, e.ScopeId, e.Permission }).IsUnique();
        builder.Property(e => e.Permission).HasConversion<string>();

        builder.HasOne(e => e.ProjectUser)
            .WithMany(pu => pu.ScopePermissions)
            .HasForeignKey(e => e.ProjectUserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Scope)
            .WithMany()
            .HasForeignKey(e => e.ScopeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
