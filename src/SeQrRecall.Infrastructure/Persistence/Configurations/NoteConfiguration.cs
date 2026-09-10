using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeQrRecall.Domain.Entities;
using SeQrRecall.Domain.Enums;

namespace SeQrRecall.Infrastructure.Persistence.Configurations;

internal sealed class NoteConfiguration : IEntityTypeConfiguration<Note>
{
    public void Configure(EntityTypeBuilder<Note> builder)
    {
        builder.ToTable("Notes");
        builder.HasKey(note => note.Id);

        builder.Property(note => note.Title).HasMaxLength(200);
        builder.Property(note => note.ShortSummary).HasMaxLength(500);
        builder.Property(note => note.AudioFileUrl).HasMaxLength(500);
        builder.Property(note => note.ProcessingError).HasMaxLength(2000);
        builder.Property(note => note.DetectedLanguages).HasMaxLength(200);
        builder.Property(note => note.ProcessingStatus)
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasDefaultValue(ProcessingStatus.Draft);
        builder.Property(note => note.IsDeleted).HasDefaultValue(false);
        builder.Property(note => note.CreatedOn).IsRequired();

        builder.HasIndex(note => new { note.UserId, note.CreatedOn }).IsDescending(false, true);
        builder.HasIndex(note => new { note.UserId, note.ProcessingStatus });

        builder.HasOne(note => note.User)
            .WithMany(user => user.Notes)
            .HasForeignKey(note => note.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(note => !note.IsDeleted);
    }
}
