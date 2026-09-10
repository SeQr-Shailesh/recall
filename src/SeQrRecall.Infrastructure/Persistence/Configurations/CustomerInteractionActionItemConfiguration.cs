using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeQrRecall.Domain.Entities;
using SeQrRecall.Domain.Enums;

namespace SeQrRecall.Infrastructure.Persistence.Configurations;

internal sealed class CustomerInteractionActionItemConfiguration : IEntityTypeConfiguration<CustomerInteractionActionItem>
{
    public void Configure(EntityTypeBuilder<CustomerInteractionActionItem> builder)
    {
        builder.ToTable("CustomerInteractionActionItems");
        builder.HasKey(item => item.Id);

        builder.Property(item => item.Description).HasMaxLength(1000).IsRequired();
        builder.Property(item => item.IsCompleted).HasDefaultValue(false);
        builder.Property(item => item.Kind)
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasDefaultValue(ActionItemKind.Action);
        builder.Property(item => item.CreatedOn).IsRequired();

        builder.HasOne(item => item.Interaction)
            .WithMany(interaction => interaction.ActionItems)
            .HasForeignKey(item => item.InteractionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
