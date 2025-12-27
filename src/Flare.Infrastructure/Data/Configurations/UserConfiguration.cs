using Flare.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Flare.Infrastructure.Data.Configurations;

public class UserConfiguration: IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.Username).IsUnique();
        builder.Property(e => e.Username).HasMaxLength(50).IsRequired();
        builder.Property(e => e.FullName).HasMaxLength(255).IsRequired();
        builder.Property(e => e.GlobalRole).HasConversion<string>();
    }
}