using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeQrRecall.Domain.Entities;
using SeQrRecall.Domain.Enums;

namespace SeQrRecall.Infrastructure.Persistence.Configurations;

internal sealed class LeadActionItemConfiguration : IEntityTypeConfiguration<LeadActionItem>
{
    public void Configure(EntityTypeBuilder<LeadActionItem> builder)
    {
        builder.ToTable("LeadActionItems");
        builder.HasKey(item => item.Id);

        builder.Property(item => item.Description).HasMaxLength(1000).IsRequired();
        builder.Property(item => item.IsCompleted).HasDefaultValue(false);
        builder.Property(item => item.Kind)
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasDefaultValue(ActionItemKind.Action);
        builder.Property(item => item.CreatedOn).IsRequired();

        builder.HasOne(item => item.Lead)
            .WithMany(lead => lead.ActionItems)
            .HasForeignKey(item => item.LeadId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
