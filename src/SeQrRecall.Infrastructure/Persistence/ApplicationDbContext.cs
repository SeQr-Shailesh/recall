using Microsoft.EntityFrameworkCore;
using SeQrRecall.Application.Abstractions.Persistence;
using SeQrRecall.Application.Abstractions.Security;
using SeQrRecall.Domain.Common;
using SeQrRecall.Domain.Entities;

namespace SeQrRecall.Infrastructure.Persistence;

public sealed class ApplicationDbContext : DbContext, IApplicationDbContext
{
    private readonly ICurrentUserService? _currentUser;

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        ICurrentUserService? currentUser = null)
        : base(options)
    {
        _currentUser = currentUser;
    }

    public DbSet<User> Users => Set<User>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<Note> Notes => Set<Note>();

    public DbSet<NoteActionItem> NoteActionItems => Set<NoteActionItem>();

    public DbSet<Lead> Leads => Set<Lead>();

    public DbSet<LeadActionItem> LeadActionItems => Set<LeadActionItem>();

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<CustomerInteraction> CustomerInteractions => Set<CustomerInteraction>();

    public DbSet<CustomerInteractionActionItem> CustomerInteractionActionItems => Set<CustomerInteractionActionItem>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    IQueryable<User> IApplicationDbContext.Users => Users;

    IQueryable<RefreshToken> IApplicationDbContext.RefreshTokens => RefreshTokens;

    IQueryable<Note> IApplicationDbContext.Notes => Notes;

    IQueryable<NoteActionItem> IApplicationDbContext.NoteActionItems => NoteActionItems;

    IQueryable<Lead> IApplicationDbContext.Leads => Leads;

    IQueryable<LeadActionItem> IApplicationDbContext.LeadActionItems => LeadActionItems;

    IQueryable<Customer> IApplicationDbContext.Customers => Customers;

    IQueryable<CustomerInteraction> IApplicationDbContext.CustomerInteractions => CustomerInteractions;

    IQueryable<CustomerInteractionActionItem> IApplicationDbContext.CustomerInteractionActionItems =>
        CustomerInteractionActionItems;

    IQueryable<AuditLog> IApplicationDbContext.AuditLogs => AuditLogs;

    void IApplicationDbContext.Add<TEntity>(TEntity entity)
    {
        Set<TEntity>().Add(entity);
    }

    void IApplicationDbContext.Remove<TEntity>(TEntity entity)
    {
        Set<TEntity>().Remove(entity);
    }

    public Task<User?> GetUserByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Users.FirstOrDefaultAsync(user => user.Id == id, cancellationToken);
    }

    public Task<User?> GetUserByMobileAsync(string mobile, CancellationToken cancellationToken = default)
    {
        return Users.FirstOrDefaultAsync(user => user.Mobile == mobile, cancellationToken);
    }

    public Task<User?> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return Users.FirstOrDefaultAsync(user => user.Email == email, cancellationToken);
    }

    public Task<RefreshToken?> GetRefreshTokenByHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        return RefreshTokens
            .Include(token => token.User)
            .FirstOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);
    }

    public Task<List<RefreshToken>> GetRefreshTokensByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return RefreshTokens.Where(token => token.UserId == userId).ToListAsync(cancellationToken);
    }

    public Task<Note?> GetNoteByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Notes.FirstOrDefaultAsync(note => note.Id == id, cancellationToken);
    }

    public Task<Note?> GetNoteByIdForUserAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        return Notes.FirstOrDefaultAsync(note => note.Id == id && note.UserId == userId, cancellationToken);
    }

    public Task<Note?> GetNoteWithActionItemsByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Notes
            .Include(note => note.ActionItems)
            .FirstOrDefaultAsync(note => note.Id == id, cancellationToken);
    }

    public Task<Note?> GetNoteWithActionItemsForUserAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        return Notes
            .Include(note => note.ActionItems)
            .FirstOrDefaultAsync(note => note.Id == id && note.UserId == userId, cancellationToken);
    }

    public async Task<(IReadOnlyList<Note> Items, int TotalCount)> GetNotesPageForUserAsync(
        Guid userId,
        int pageNumber,
        int pageSize,
        string? search,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Note> query = Notes.Where(note => note.UserId == userId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim();
            if (term.Length > 200)
            {
                term = term[..200];
            }

            query = query.Where(note =>
                (note.Title != null && note.Title.Contains(term))
                || (note.ShortSummary != null && note.ShortSummary.Contains(term))
                || (note.FullSummary != null && note.FullSummary.Contains(term))
                || (note.Transcript != null && note.Transcript.Contains(term)));
        }

        int totalCount = await query.CountAsync(cancellationToken);
        List<Note> items = await query
            .OrderByDescending(note => note.CreatedOn)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public Task<Lead?> GetLeadByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Leads.FirstOrDefaultAsync(lead => lead.Id == id, cancellationToken);
    }

    public Task<Lead?> GetLeadByIdForUserAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        return Leads.FirstOrDefaultAsync(lead => lead.Id == id && lead.UserId == userId, cancellationToken);
    }

    public Task<Lead?> GetLeadWithActionItemsByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Leads
            .Include(lead => lead.ActionItems)
            .FirstOrDefaultAsync(lead => lead.Id == id, cancellationToken);
    }

    public Task<Lead?> GetLeadWithActionItemsForUserAsync(
        Guid userId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return Leads
            .Include(lead => lead.ActionItems)
            .FirstOrDefaultAsync(lead => lead.Id == id && lead.UserId == userId, cancellationToken);
    }

    public async Task<(IReadOnlyList<Lead> Items, int TotalCount)> GetLeadsPageForUserAsync(
        Guid userId,
        int pageNumber,
        int pageSize,
        string? search,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Lead> query = Leads.Where(lead => lead.UserId == userId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim();
            if (term.Length > 200)
            {
                term = term[..200];
            }

            query = query.Where(lead =>
                (lead.Title != null && lead.Title.Contains(term))
                || (lead.ShortSummary != null && lead.ShortSummary.Contains(term))
                || (lead.FullSummary != null && lead.FullSummary.Contains(term))
                || (lead.Transcript != null && lead.Transcript.Contains(term)));
        }

        int totalCount = await query.CountAsync(cancellationToken);
        List<Lead> items = await query
            .OrderByDescending(lead => lead.CreatedOn)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public Task<Customer?> GetCustomerByIdForUserAsync(
        Guid userId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return Customers.FirstOrDefaultAsync(
            customer => customer.Id == id && customer.UserId == userId,
            cancellationToken);
    }

    public async Task<(IReadOnlyList<Customer> Items, int TotalCount)> GetCustomersPageForUserAsync(
        Guid userId,
        int pageNumber,
        int pageSize,
        string? search,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Customer> query = Customers.Where(customer => customer.UserId == userId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim();
            if (term.Length > 200)
            {
                term = term[..200];
            }

            query = query.Where(customer =>
                customer.Name.Contains(term)
                || (customer.CompanyName != null && customer.CompanyName.Contains(term))
                || (customer.Mobile != null && customer.Mobile.Contains(term))
                || (customer.Email != null && customer.Email.Contains(term)));
        }

        int totalCount = await query.CountAsync(cancellationToken);
        List<Customer> items = await query
            .OrderByDescending(customer => customer.CreatedOn)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public Task<CustomerInteraction?> GetCustomerInteractionByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return CustomerInteractions.FirstOrDefaultAsync(interaction => interaction.Id == id, cancellationToken);
    }

    public Task<CustomerInteraction?> GetCustomerInteractionByIdForUserAsync(
        Guid userId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return CustomerInteractions.FirstOrDefaultAsync(
            interaction => interaction.Id == id && interaction.UserId == userId,
            cancellationToken);
    }

    public Task<CustomerInteraction?> GetCustomerInteractionWithActionItemsByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return CustomerInteractions
            .Include(interaction => interaction.ActionItems)
            .FirstOrDefaultAsync(interaction => interaction.Id == id, cancellationToken);
    }

    public Task<CustomerInteraction?> GetCustomerInteractionWithDetailsForUserAsync(
        Guid userId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return CustomerInteractions
            .Include(interaction => interaction.Customer)
            .Include(interaction => interaction.ActionItems)
            .FirstOrDefaultAsync(
                interaction => interaction.Id == id && interaction.UserId == userId,
                cancellationToken);
    }

    public async Task<(IReadOnlyList<CustomerInteraction> Items, int TotalCount)> GetInteractionsPageForCustomerAsync(
        Guid userId,
        Guid customerId,
        int pageNumber,
        int pageSize,
        string? search,
        CancellationToken cancellationToken = default)
    {
        IQueryable<CustomerInteraction> query = CustomerInteractions.Where(interaction =>
            interaction.UserId == userId && interaction.CustomerId == customerId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim();
            if (term.Length > 200)
            {
                term = term[..200];
            }

            query = query.Where(interaction =>
                (interaction.ShortSummary != null && interaction.ShortSummary.Contains(term))
                || (interaction.FullSummary != null && interaction.FullSummary.Contains(term))
                || (interaction.Transcript != null && interaction.Transcript.Contains(term)));
        }

        int totalCount = await query.CountAsync(cancellationToken);
        List<CustomerInteraction> items = await query
            .OrderByDescending(interaction => interaction.InteractionDate)
            .ThenByDescending(interaction => interaction.CreatedOn)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public override int SaveChanges()
    {
        ApplyAudit();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAudit();
        return base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    private void ApplyAudit()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Guid? userId = _currentUser is { IsAuthenticated: true } ? _currentUser.UserId : null;

        foreach (Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry in ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Added)
            {
                Microsoft.EntityFrameworkCore.ChangeTracking.PropertyEntry? idProperty = entry.Properties
                    .FirstOrDefault(property => property.Metadata.Name == "Id");
                if (idProperty?.CurrentValue is Guid id && id == Guid.Empty)
                {
                    idProperty.CurrentValue = Guid.NewGuid();
                }
            }

            if (entry.Entity is not IAuditableEntity auditable)
            {
                continue;
            }

            if (entry.State == EntityState.Added)
            {
                if (auditable.CreatedOn == default)
                {
                    auditable.CreatedOn = now;
                }

                auditable.CreatedBy ??= userId;
            }
            else if (entry.State == EntityState.Modified)
            {
                auditable.UpdatedOn = now;
                auditable.UpdatedBy = userId;
            }
        }
    }
}
