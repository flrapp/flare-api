using Flare.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Flare.Infrastructure.Data.Configurations;

public class ProjectUserProjectPermissionConfiguration : IEntityTypeConfiguration<ProjectUserProjectPermission>
{
    public void Configure(EntityTypeBuilder<ProjectUserProjectPermission> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => new { e.ProjectUserId, e.Permission }).IsUnique();
        builder.Property(e => e.Permission).HasConversion<string>();

        builder.HasOne(e => e.ProjectUser)
            .WithMany(pu => pu.ProjectPermissions)
            .HasForeignKey(e => e.ProjectUserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
