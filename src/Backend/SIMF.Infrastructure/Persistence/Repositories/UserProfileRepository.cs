using Microsoft.EntityFrameworkCore;
using SIMF.Application.IdentityAccess;
using SIMF.Common.Enums;
using SIMF.Contracts.UserProfile;
using SIMF.Domain.Profiles;

namespace SIMF.Infrastructure.Persistence.Repositories;

/// <summary>R4 — D-209: EF-backed <see cref="IUserProfileRepository"/>. Spans
/// both contexts (App DB for the profile + lookups; Identity DB for the
/// account reads + the transactional save). Query shapes are lifted verbatim
/// from the pre-move <c>UserProfileService</c>.</summary>
internal sealed class UserProfileRepository(
    SimfIdentityDbContext dbContext,
    SimfAppDbContext appDbContext) : IUserProfileRepository
{
    public Task<UserProfile?> GetWithInterestsAsync(
        Guid userId, bool tracked, CancellationToken cancellationToken = default)
    {
        IQueryable<UserProfile> query = appDbContext.UserProfiles.Include(p => p.Interests);
        if (!tracked)
        {
            query = query.AsNoTracking();
        }
        return query.SingleOrDefaultAsync(p => p.UserId == userId, cancellationToken);
    }

    public Task<UserProfile?> FindAsync(
        Guid userId, CancellationToken cancellationToken = default) =>
        appDbContext.UserProfiles.SingleOrDefaultAsync(p => p.UserId == userId, cancellationToken);

    public void Add(UserProfile profile) => appDbContext.UserProfiles.Add(profile);

    public async Task<long> NextRegistrationReferenceAsync(
        CancellationToken cancellationToken = default)
    {
        // D-373 — a SQL sequence is the concurrency-safe issuer for the
        // human-quotable registration reference. Raw ADO because SQL Server
        // forbids NEXT VALUE FOR inside the derived table EF wraps
        // SqlQueryRaw results into.
        var connection = appDbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT NEXT VALUE FOR [dbo].[RegistrationReferenceSequence];";
        var transaction = appDbContext.Database.CurrentTransaction;
        if (transaction is not null)
        {
            command.Transaction =
                Microsoft.EntityFrameworkCore.Storage.DbContextTransactionExtensions
                    .GetDbTransaction(transaction);
        }
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return (long)value!;
    }

    public async Task<RejectionText?> GetRejectionTextAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        var row = await appDbContext.UserProfiles
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => new { p.RejectionReason, p.RejectionReasonArabic })
            .SingleOrDefaultAsync(cancellationToken);
        if (row is null) { return null; }
        if (row.RejectionReason is null && row.RejectionReasonArabic is null) { return null; }
        return new RejectionText(row.RejectionReason, row.RejectionReasonArabic);
    }

    public async Task<string?> GetIdImagePathAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        var path = await appDbContext.UserProfiles
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => p.IdImageRelativePath)
            .SingleOrDefaultAsync(cancellationToken);
        return string.IsNullOrEmpty(path) ? null : path;
    }

    public async Task<string?> GetVipPhotoPathAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        var path = await appDbContext.UserProfiles
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => p.VipPhotoRelativePath)
            .SingleOrDefaultAsync(cancellationToken);
        return string.IsNullOrEmpty(path) ? null : path;
    }

    public async Task<(string StorageKey, string? ContentType, bool IsEncrypted)?> GetOwnerScopedFileAsync(
        FileService service, Guid ownerUserId, CancellationToken cancellationToken = default)
    {
        var row = await appDbContext.StoredFiles
            .AsNoTracking()
            .Where(f => f.Service == service
                && f.OwnerEntityId == ownerUserId && f.IsActive && f.StorageKey != null)
            .OrderByDescending(f => f.CreatedAt)
            .ThenByDescending(f => f.Id)
            .Select(f => new { f.StorageKey, f.ContentType, f.IsEncrypted })
            .FirstOrDefaultAsync(cancellationToken);
        return row is null ? null : (row.StorageKey!, row.ContentType, row.IsEncrypted);
    }

    public Task<ProfileCompletenessFacts?> GetCompletenessFactsAsync(
        Guid userId, CancellationToken cancellationToken = default) =>
        appDbContext.UserProfiles
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => new ProfileCompletenessFacts(
                p.Name, p.NameArabic, p.Gender,
                p.IdImageRelativePath, p.Interests.Any()))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<ProfileTypeRole?> GetAssignedProfileTypeRoleAsync(
        Guid userId, CancellationToken cancellationToken = default) =>
        appDbContext.UserProfiles
            .AsNoTracking()
            .Where(p => p.UserId == userId && p.ProfileType != null)
            .Select(p => new ProfileTypeRole(p.ProfileType!.IsForVisitor, p.ProfileType.MobileAppRole))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<ProfileTypeFacts?> FindProfileTypeAsync(
        Guid profileTypeId, CancellationToken cancellationToken = default) =>
        appDbContext.ProfileTypes
            .AsNoTracking()
            .Where(p => p.Id == profileTypeId)
            .Select(p => new ProfileTypeFacts(
                p.IsActive, SIMF.Common.Enums.UserType.Visitor,
                p.IsForVisitor, p.Name))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<Guid>> FilterActiveInterestIdsAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default) =>
        await appDbContext.Interests
            .AsNoTracking()
            .Where(interest => ids.Contains(interest.Id) && interest.IsActive)
            .Select(interest => interest.Id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<UserInterest>> GetInterestsByIdsAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default) =>
        await appDbContext.Interests
            .Where(interest => ids.Contains(interest.Id))
            .ToListAsync(cancellationToken);

    public async Task<int?> ResolveCountryIdAsync(
        string code, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code)) { return null; }
        var upper = code.Trim().ToUpperInvariant();
        return await appDbContext.Countries
            .AsNoTracking()
            .Where(country => country.Code == upper && country.IsActive)
            .Select(country => (int?)country.Id)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<string> ResolveCountryCodeAsync(
        int id, CancellationToken cancellationToken = default)
    {
        if (id == 0) { return string.Empty; }
        return await appDbContext.Countries
            .AsNoTracking()
            .Where(country => country.Id == id)
            .Select(country => country.Code)
            .SingleOrDefaultAsync(cancellationToken) ?? string.Empty;
    }

    public Task<bool> OrganisationExistsActiveAsync(
        Guid id, CancellationToken cancellationToken = default) =>
        appDbContext.Organisations
            .AsNoTracking()
            .AnyAsync(organisation => organisation.Id == id && organisation.IsActive, cancellationToken);

    public Task<bool> RegionExistsActiveAsync(
        Guid id, CancellationToken cancellationToken = default) =>
        appDbContext.Regions
            .AsNoTracking()
            .AnyAsync(region => region.Id == id && region.IsActive, cancellationToken);

    public async Task<IReadOnlyList<PendingAdminRecipient>> ListApprovedAdminsAsync(
        CancellationToken cancellationToken = default) =>
        await dbContext.Users
            .AsNoTracking()
            .Where(u => u.UserType == UserType.Admin && u.AccountState == AccountState.Approved)
            .Select(u => new PendingAdminRecipient(u.Id, u.Email, u.DisplayName))
            .ToListAsync(cancellationToken);

    public Task SaveIdentityChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);

    public Task SaveAppChangesAsync(CancellationToken cancellationToken = default) =>
        appDbContext.SaveChangesAsync(cancellationToken);
}
