using Microsoft.EntityFrameworkCore;
using SeQrRecall.Domain.Entities;
using SeQrRecall.Infrastructure.Persistence;
using Xunit;

namespace SeQrRecall.UnitTests.Persistence;

public sealed class SoftDeleteQueryFilterTests
{
    [Fact]
    public async Task Deleted_notes_are_hidden_by_the_global_query_filter()
    {
        await using ApplicationDbContext context = CreateContext();
        User user = new() { Id = Guid.NewGuid(), FullName = "Test User" };
        Note visible = new() { Id = Guid.NewGuid(), UserId = user.Id, Title = "Visible" };
        Note deleted = new() { Id = Guid.NewGuid(), UserId = user.Id, Title = "Deleted", IsDeleted = true };

        context.Users.Add(user);
        context.Notes.AddRange(visible, deleted);
        await context.SaveChangesAsync();

        List<Note> notes = await context.Notes.ToListAsync();
        List<Note> all = await context.Notes.IgnoreQueryFilters().ToListAsync();

        Assert.Single(notes);
        Assert.Equal("Visible", notes[0].Title);
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task Deleted_customers_are_hidden_by_the_global_query_filter()
    {
        await using ApplicationDbContext context = CreateContext();
        User user = new() { Id = Guid.NewGuid(), FullName = "Test User" };
        Customer customer = new()
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Name = "Hidden Co",
            IsDeleted = true
        };

        context.Users.Add(user);
        context.Customers.Add(customer);
        await context.SaveChangesAsync();

        Assert.Empty(await context.Customers.ToListAsync());
        Assert.Single(await context.Customers.IgnoreQueryFilters().ToListAsync());
    }

    private static ApplicationDbContext CreateContext()
    {
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new ApplicationDbContext(options);
    }
}
