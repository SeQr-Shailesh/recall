using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeQrRecall.Domain.Entities;

namespace SeQrRecall.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(user => user.Id);

        builder.Property(user => user.FullName).HasMaxLength(200).IsRequired();
        builder.Property(user => user.Mobile).HasMaxLength(20);
        builder.Property(user => user.Email).HasMaxLength(256);
        builder.Property(user => user.ProfilePhotoUrl).HasMaxLength(500);
        builder.Property(user => user.IsActive).HasDefaultValue(true);
        builder.Property(user => user.CreatedOn).IsRequired();

        builder.HasIndex(user => user.Mobile)
            .IsUnique()
            .HasFilter("[Mobile] IS NOT NULL");

        builder.HasIndex(user => user.Email)
            .IsUnique()
            .HasFilter("[Email] IS NOT NULL");
    }
}
