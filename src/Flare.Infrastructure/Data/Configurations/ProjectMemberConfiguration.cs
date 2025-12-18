using Domian.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Flare.Infrastructure.Data.Configurations;

public class ProjectMemberConfiguration : IEntityTypeConfiguration<ProjectMember>
{
    public void Configure(EntityTypeBuilder<ProjectMember> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => new { e.ProjectId, e.UserId }).IsUnique();
        builder.Property(e => e.ProjectRole).HasConversion<string>();
        
        builder.HasOne(e => e.Project)
            .WithMany(p => p.Members)
            .HasForeignKey(e => e.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
            
        builder.HasOne(e => e.User)
            .WithMany(u => u.ProjectMemberships)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}