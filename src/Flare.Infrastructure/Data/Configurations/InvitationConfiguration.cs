using Flare.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Flare.Infrastructure.Data.Configurations;

public class InvitationConfiguration: IEntityTypeConfiguration<Invitation>
{
    public void Configure(EntityTypeBuilder<Invitation> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.Token).IsUnique();
        builder.Property(e => e.Status).HasConversion<string>();
        
        builder.HasOne(e => e.Project)
            .WithMany()
            .HasForeignKey(e => e.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
            
        builder.HasOne(e => e.Inviter)
            .WithMany()
            .HasForeignKey(e => e.InvitedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}