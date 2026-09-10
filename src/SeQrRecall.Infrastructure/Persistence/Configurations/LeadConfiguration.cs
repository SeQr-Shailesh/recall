using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeQrRecall.Domain.Entities;
using SeQrRecall.Domain.Enums;

namespace SeQrRecall.Infrastructure.Persistence.Configurations;

internal sealed class LeadConfiguration : IEntityTypeConfiguration<Lead>
{
    public void Configure(EntityTypeBuilder<Lead> builder)
    {
        builder.ToTable("Leads");
        builder.HasKey(lead => lead.Id);

        builder.Property(lead => lead.Title).HasMaxLength(200);
        builder.Property(lead => lead.ShortSummary).HasMaxLength(500);
        builder.Property(lead => lead.AudioFileUrl).HasMaxLength(500);
        builder.Property(lead => lead.PhotoUrl).HasMaxLength(500);
        builder.Property(lead => lead.ProcessingError).HasMaxLength(2000);
        builder.Property(lead => lead.DetectedLanguages).HasMaxLength(200);
        builder.Property(lead => lead.ProcessingStatus)
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasDefaultValue(ProcessingStatus.Draft);
        builder.Property(lead => lead.IsDeleted).HasDefaultValue(false);
        builder.Property(lead => lead.CreatedOn).IsRequired();

        builder.HasIndex(lead => new { lead.UserId, lead.CreatedOn }).IsDescending(false, true);
        builder.HasIndex(lead => new { lead.UserId, lead.ProcessingStatus });

        builder.HasOne(lead => lead.User)
            .WithMany(user => user.Leads)
            .HasForeignKey(lead => lead.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(lead => !lead.IsDeleted);
    }
}
