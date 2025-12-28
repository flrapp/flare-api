using Flare.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Flare.Infrastructure.Data.Configurations;

public class ProjectUserConfiguration : IEntityTypeConfiguration<ProjectUser>
{
    public void Configure(EntityTypeBuilder<ProjectUser> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => new { e.ProjectId, e.UserId }).IsUnique();

        builder.HasOne(e => e.Project)
            .WithMany(p => p.Members)
            .HasForeignKey(e => e.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.User)
            .WithMany(u => u.ProjectMemberships)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.ProjectPermissions)
            .WithOne(p => p.ProjectUser)
            .HasForeignKey(p => p.ProjectUserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.ScopePermissions)
            .WithOne(p => p.ProjectUser)
            .HasForeignKey(p => p.ProjectUserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
