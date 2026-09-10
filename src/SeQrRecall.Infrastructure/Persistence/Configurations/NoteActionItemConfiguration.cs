using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeQrRecall.Domain.Entities;
using SeQrRecall.Domain.Enums;

namespace SeQrRecall.Infrastructure.Persistence.Configurations;

internal sealed class NoteActionItemConfiguration : IEntityTypeConfiguration<NoteActionItem>
{
    public void Configure(EntityTypeBuilder<NoteActionItem> builder)
    {
        builder.ToTable("NoteActionItems");
        builder.HasKey(item => item.Id);

        builder.Property(item => item.Description).HasMaxLength(1000).IsRequired();
        builder.Property(item => item.IsCompleted).HasDefaultValue(false);
        builder.Property(item => item.Kind)
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasDefaultValue(ActionItemKind.Action);
        builder.Property(item => item.CreatedOn).IsRequired();

        builder.HasOne(item => item.Note)
            .WithMany(note => note.ActionItems)
            .HasForeignKey(item => item.NoteId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
