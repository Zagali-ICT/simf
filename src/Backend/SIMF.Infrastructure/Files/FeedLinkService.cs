// Tests: SIMF.Api.Tests/FeedLinkTests.cs
using Microsoft.EntityFrameworkCore;
using SIMF.Application.Files.Abstractions;
using SIMF.Common.Enums;
using SIMF.Domain.Files;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Files;

/// <summary>The externally hosted feeds, held in the file store and handed back
/// verbatim. See <see cref="IFeedLinkService"/> for why verbatim is not optional.
///
/// <para>Writing goes through <see cref="IFileService"/> so a feed URL passes the
/// same validation, owner-upsert and audit as every other file: one URL rule, in
/// one place, instead of a bespoke check on each of six entities. Reading is a
/// plain lookup of <see cref="StoredFile.ExternalUrl"/>, which is the only column
/// that can answer — an <c>Upload</c> row has no URL at all, its bytes being
/// addressed by a storage key.</para></summary>
internal sealed class FeedLinkService(
    SimfAppDbContext dbContext,
    IFileService fileService) : IFeedLinkService
{
    public async Task<Guid?> SetAsync(
        FileService service,
        Guid ownerId,
        string? url,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            await RetireAsync(service, ownerId, actorUserId, cancellationToken);
            return null;
        }

        // CreateExternalLinkAsync upserts by (service, owner): it replaces the
        // owner's active row in place, so changing a feed does not accumulate
        // dead rows, and the owning row's pointer is maintained by the store.
        var result = await fileService.CreateExternalLinkAsync(
            new CreateExternalLinkCommand(service, ownerId, url.Trim(), actorUserId),
            cancellationToken);
        return result.Id;
    }

    public async Task<string?> ResolveAsync(
        Guid? fileId, CancellationToken cancellationToken = default)
    {
        if (fileId is not { } id)
        {
            return null;
        }

        return await dbContext.StoredFiles.AsNoTracking()
            .Where(file => file.Id == id
                && file.IsActive
                && file.SourceType == FileSourceType.ExternalLink)
            .Select(file => file.ExternalUrl)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, string>> ResolveManyAsync(
        IEnumerable<Guid?> fileIds, CancellationToken cancellationToken = default)
    {
        var ids = fileIds
            .Where(id => id is not null)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        if (ids.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        return await dbContext.StoredFiles.AsNoTracking()
            .Where(file => ids.Contains(file.Id)
                && file.IsActive
                && file.SourceType == FileSourceType.ExternalLink
                && file.ExternalUrl != null)
            .ToDictionaryAsync(file => file.Id, file => file.ExternalUrl!, cancellationToken);
    }

    // Clearing a feed retires its row rather than orphaning it: the owning
    // pointer is nulled by the store as part of the delete, so the two halves of
    // the link cannot end up disagreeing.
    private async Task RetireAsync(
        FileService service, Guid ownerId, Guid actorUserId, CancellationToken cancellationToken)
    {
        var ids = await dbContext.StoredFiles.AsNoTracking()
            .Where(file => file.Service == service && file.OwnerEntityId == ownerId && file.IsActive)
            .Select(file => file.Id)
            .ToListAsync(cancellationToken);

        foreach (var id in ids)
        {
            await fileService.DeleteAsync(id, actorUserId, cancellationToken);
        }
    }
}
