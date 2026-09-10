using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeQrRecall.Domain.Entities;

namespace SeQrRecall.Infrastructure.Persistence.Configurations;

internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");
        builder.HasKey(log => log.Id);

        builder.Property(log => log.Action).HasMaxLength(100).IsRequired();
        builder.Property(log => log.EntityType).HasMaxLength(100).IsRequired();
        builder.Property(log => log.Details).HasMaxLength(2000);
        builder.Property(log => log.CreatedOn).IsRequired();

        builder.HasIndex(log => log.CreatedOn);
        builder.HasIndex(log => new { log.EntityType, log.EntityId });
    }
}
