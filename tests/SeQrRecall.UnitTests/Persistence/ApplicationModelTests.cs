using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using SeQrRecall.Domain.Entities;
using SeQrRecall.Infrastructure.Persistence;
using Xunit;

namespace SeQrRecall.UnitTests.Persistence;

public sealed class ApplicationModelTests
{
    [Fact]
    public void Users_have_filtered_unique_indexes_on_mobile_and_email()
    {
        using ApplicationDbContext context = CreateModelContext();
        IEntityType entity = context.Model.FindEntityType(typeof(User))!;
        IIndex[] unique = entity.GetIndexes().Where(index => index.IsUnique).ToArray();

        Assert.Contains(unique, index => index.Properties.Any(property => property.Name == "Mobile")
            && index.GetFilter() is not null);
        Assert.Contains(unique, index => index.Properties.Any(property => property.Name == "Email")
            && index.GetFilter() is not null);
    }

    [Fact]
    public void Notes_and_customers_have_soft_delete_query_filters()
    {
        using ApplicationDbContext context = CreateModelContext();

        Assert.NotEmpty(context.Model.FindEntityType(typeof(Note))!.GetDeclaredQueryFilters());
        Assert.NotEmpty(context.Model.FindEntityType(typeof(Customer))!.GetDeclaredQueryFilters());
        Assert.NotEmpty(context.Model.FindEntityType(typeof(CustomerInteraction))!.GetDeclaredQueryFilters());
    }

    [Fact]
    public void Notes_are_indexed_by_user_and_created_on()
    {
        using ApplicationDbContext context = CreateModelContext();
        IEntityType entity = context.Model.FindEntityType(typeof(Note))!;

        Assert.Contains(
            entity.GetIndexes(),
            index => index.Properties.Select(property => property.Name).SequenceEqual(["UserId", "CreatedOn"]));
        Assert.Contains(
            entity.GetIndexes(),
            index => index.Properties.Select(property => property.Name).SequenceEqual(["UserId", "ProcessingStatus"]));
    }

    private static ApplicationDbContext CreateModelContext()
    {
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=localhost;Database=SeQrRecall;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;

        return new ApplicationDbContext(options);
    }
}
