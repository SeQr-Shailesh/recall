using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeQrRecall.Domain.Entities;

namespace SeQrRecall.Infrastructure.Persistence.Configurations;

internal sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");
        builder.HasKey(customer => customer.Id);

        builder.Property(customer => customer.Name).HasMaxLength(200).IsRequired();
        builder.Property(customer => customer.CompanyName).HasMaxLength(200);
        builder.Property(customer => customer.Mobile).HasMaxLength(20);
        builder.Property(customer => customer.Email).HasMaxLength(256);
        builder.Property(customer => customer.PhotoUrl).HasMaxLength(500);
        builder.Property(customer => customer.IsActive).HasDefaultValue(true);
        builder.Property(customer => customer.IsDeleted).HasDefaultValue(false);
        builder.Property(customer => customer.CreatedOn).IsRequired();

        builder.HasIndex(customer => new { customer.UserId, customer.Name });
        builder.HasIndex(customer => new { customer.UserId, customer.CompanyName });
        builder.HasIndex(customer => new { customer.UserId, customer.Mobile });

        builder.HasOne(customer => customer.User)
            .WithMany(user => user.Customers)
            .HasForeignKey(customer => customer.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(customer => !customer.IsDeleted);
    }
}
