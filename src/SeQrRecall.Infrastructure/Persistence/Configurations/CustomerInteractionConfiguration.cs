using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeQrRecall.Domain.Entities;
using SeQrRecall.Domain.Enums;

namespace SeQrRecall.Infrastructure.Persistence.Configurations;

internal sealed class CustomerInteractionConfiguration : IEntityTypeConfiguration<CustomerInteraction>
{
    public void Configure(EntityTypeBuilder<CustomerInteraction> builder)
    {
        builder.ToTable("CustomerInteractions");
        builder.HasKey(interaction => interaction.Id);

        builder.Property(interaction => interaction.PhotoUrl).HasMaxLength(500);
        builder.Property(interaction => interaction.AudioFileUrl).HasMaxLength(500);
        builder.Property(interaction => interaction.ShortSummary).HasMaxLength(500);
        builder.Property(interaction => interaction.ProcessingError).HasMaxLength(2000);
        builder.Property(interaction => interaction.DetectedLanguages).HasMaxLength(200);
        builder.Property(interaction => interaction.ProcessingStatus)
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasDefaultValue(ProcessingStatus.Draft);
        builder.Property(interaction => interaction.IsDeleted).HasDefaultValue(false);
        builder.Property(interaction => interaction.CreatedOn).IsRequired();
        builder.Property(interaction => interaction.InteractionDate).IsRequired();

        builder.HasIndex(interaction => new { interaction.UserId, interaction.InteractionDate }).IsDescending(false, true);
        builder.HasIndex(interaction => new { interaction.CustomerId, interaction.InteractionDate }).IsDescending(false, true);

        builder.HasOne(interaction => interaction.User)
            .WithMany(user => user.CustomerInteractions)
            .HasForeignKey(interaction => interaction.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(interaction => interaction.Customer)
            .WithMany(customer => customer.Interactions)
            .HasForeignKey(interaction => interaction.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(interaction => !interaction.IsDeleted && !interaction.Customer.IsDeleted);
    }
}
