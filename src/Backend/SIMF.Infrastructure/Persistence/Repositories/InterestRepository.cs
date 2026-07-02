using Microsoft.EntityFrameworkCore;
using SIMF.Application.IdentityAccess;
using SIMF.Common;
using SIMF.Contracts.Admin;
using SIMF.Contracts.UserProfile;
using SIMF.Domain.Profiles;

namespace SIMF.Infrastructure.Persistence.Repositories;

/// <summary>R4 — D-209: EF-backed <see cref="IInterestRepository"/> over
/// <see cref="SimfAppDbContext"/>. Query shapes are lifted verbatim from the
/// pre-move <c>InterestService</c> so the SQL is unchanged.</summary>
internal sealed class InterestRepository(SimfAppDbContext dbContext)
    : IInterestRepository
{
    // Sort tokens the admin grid sends (the SimfDataGrid column Keys, lower-cased).
    private static class SortKeys
    {
        public const string Name = "name";
        public const string NameArabic = "namearabic";
        public const string DisplayOrder = "displayorder";
        public const string CreatedAt = "createdat";
    }

    public async Task<IReadOnlyList<InterestDto>> ListActiveAsync(
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Interests
            .AsNoTracking()
            .Where(interest => interest.IsActive)
            .OrderBy(interest => interest.DisplayOrder)
            .ThenBy(interest => interest.Name)
            .Select(interest => new InterestDto(
                interest.Id, interest.Name, interest.NameArabic, interest.DisplayOrder))
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<AdminInterestSummary> Items, int Total)> ListPageAsync(
        GridQuery query, int skip, int top,
        CancellationToken cancellationToken = default)
    {
        var rows = dbContext.Interests.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            rows = rows.Where(interest =>
                EF.Functions.Like(interest.Name, $"%{term}%")
                || EF.Functions.Like(interest.NameArabic, $"%{term}%"));
        }

        if (query.Filters.TryGetValue("name", out var nameFilter)
            && !string.IsNullOrWhiteSpace(nameFilter))
        {
            rows = rows.Where(interest =>
                EF.Functions.Like(interest.Name, $"%{nameFilter}%"));
        }
        if (query.Filters.TryGetValue("nameArabic", out var arFilter)
            && !string.IsNullOrWhiteSpace(arFilter))
        {
            rows = rows.Where(interest =>
                EF.Functions.Like(interest.NameArabic, $"%{arFilter}%"));
        }
        if (query.Filters.TryGetValue("isActive", out var activeFilter)
            && bool.TryParse(activeFilter, out var isActive))
        {
            rows = rows.Where(interest => interest.IsActive == isActive);
        }

        rows = (query.Sort?.ToLowerInvariant(), query.SortDescending) switch
        {
            (SortKeys.Name, true) => rows.OrderByDescending(interest => interest.Name),
            (SortKeys.Name, false) => rows.OrderBy(interest => interest.Name),
            (SortKeys.NameArabic, true) => rows.OrderByDescending(interest => interest.NameArabic),
            (SortKeys.NameArabic, false) => rows.OrderBy(interest => interest.NameArabic),
            (SortKeys.DisplayOrder, true) => rows.OrderByDescending(interest => interest.DisplayOrder),
            (SortKeys.DisplayOrder, false) => rows.OrderBy(interest => interest.DisplayOrder),
            (SortKeys.CreatedAt, false) => rows.OrderBy(interest => interest.CreatedAt),
            // Natural order matches the visitor picker — DisplayOrder, then Name.
            _ => rows.OrderBy(interest => interest.DisplayOrder).ThenBy(interest => interest.Name),
        };

        var total = await rows.CountAsync(cancellationToken);
        var page = await rows
            .Skip(skip)
            .Take(top)
            .Select(interest => new AdminInterestSummary(
                interest.Id,
                interest.Name,
                interest.NameArabic,
                interest.DisplayOrder,
                interest.IsActive,
                interest.CreatedAt))
            .ToListAsync(cancellationToken);

        return (page, total);
    }

    public async Task<AdminInterestSummary?> GetSummaryAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        var interest = await dbContext.Interests
            .AsNoTracking()
            .SingleOrDefaultAsync(row => row.Id == id, cancellationToken);
        return interest is null
            ? null
            : new AdminInterestSummary(
                interest.Id, interest.Name, interest.NameArabic,
                interest.DisplayOrder, interest.IsActive, interest.CreatedAt);
    }

    public Task<bool> NameExistsAsync(
        string name, Guid? excludingId,
        CancellationToken cancellationToken = default)
    {
        var rows = dbContext.Interests.AsNoTracking();
        return excludingId is { } id
            ? rows.AnyAsync(row => row.Id != id && row.Name == name, cancellationToken)
            : rows.AnyAsync(row => row.Name == name, cancellationToken);
    }

    public Task<UserInterest?> FindAsync(
        Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Interests.SingleOrDefaultAsync(row => row.Id == id, cancellationToken);

    public async Task AddAsync(UserInterest interest, CancellationToken cancellationToken = default)
    {
        dbContext.Interests.Add(interest);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
