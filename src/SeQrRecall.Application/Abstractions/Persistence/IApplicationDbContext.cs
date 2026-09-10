using SeQrRecall.Domain.Entities;

namespace SeQrRecall.Application.Abstractions.Persistence;

/// <summary>
/// Persistence boundary. Implemented with EF Core in Infrastructure during Phase 1.
/// Application code must not reference EF Core types.
/// </summary>
public interface IApplicationDbContext
{
    IQueryable<User> Users { get; }

    IQueryable<RefreshToken> RefreshTokens { get; }

    IQueryable<Note> Notes { get; }

    IQueryable<NoteActionItem> NoteActionItems { get; }

    IQueryable<Lead> Leads { get; }

    IQueryable<LeadActionItem> LeadActionItems { get; }

    IQueryable<Customer> Customers { get; }

    IQueryable<CustomerInteraction> CustomerInteractions { get; }

    IQueryable<CustomerInteractionActionItem> CustomerInteractionActionItems { get; }

    IQueryable<AuditLog> AuditLogs { get; }

    void Add<TEntity>(TEntity entity)
        where TEntity : class;

    void Remove<TEntity>(TEntity entity)
        where TEntity : class;

    Task<User?> GetUserByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<User?> GetUserByMobileAsync(string mobile, CancellationToken cancellationToken = default);

    Task<User?> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<RefreshToken?> GetRefreshTokenByHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    Task<List<RefreshToken>> GetRefreshTokensByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<Note?> GetNoteByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Note?> GetNoteByIdForUserAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);

    Task<Note?> GetNoteWithActionItemsByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Note?> GetNoteWithActionItemsForUserAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<Note> Items, int TotalCount)> GetNotesPageForUserAsync(
        Guid userId,
        int pageNumber,
        int pageSize,
        string? search,
        CancellationToken cancellationToken = default);

    Task<Lead?> GetLeadByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Lead?> GetLeadByIdForUserAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);

    Task<Lead?> GetLeadWithActionItemsByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Lead?> GetLeadWithActionItemsForUserAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<Lead> Items, int TotalCount)> GetLeadsPageForUserAsync(
        Guid userId,
        int pageNumber,
        int pageSize,
        string? search,
        CancellationToken cancellationToken = default);

    Task<Customer?> GetCustomerByIdForUserAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<Customer> Items, int TotalCount)> GetCustomersPageForUserAsync(
        Guid userId,
        int pageNumber,
        int pageSize,
        string? search,
        CancellationToken cancellationToken = default);

    Task<CustomerInteraction?> GetCustomerInteractionByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<CustomerInteraction?> GetCustomerInteractionByIdForUserAsync(
        Guid userId,
        Guid id,
        CancellationToken cancellationToken = default);

    Task<CustomerInteraction?> GetCustomerInteractionWithActionItemsByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<CustomerInteraction?> GetCustomerInteractionWithDetailsForUserAsync(
        Guid userId,
        Guid id,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<CustomerInteraction> Items, int TotalCount)> GetInteractionsPageForCustomerAsync(
        Guid userId,
        Guid customerId,
        int pageNumber,
        int pageSize,
        string? search,
        CancellationToken cancellationToken = default);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
