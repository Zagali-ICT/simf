using SIMF.Common.Enums;

namespace SIMF.Application.Files.Abstractions;

/// <summary>Reads and writes the externally hosted feeds — live streams, the
/// sign-language feed, a summary video, a gallery video, the hero background —
/// as rows in the file store rather than as free text on the owning entity.
///
/// <para>The URL still reaches the clients <b>verbatim</b>, and that is the whole
/// design constraint. Both the app and the website classify a feed by inspecting
/// the string: the player extracts a YouTube id and, failing that, hands the URL
/// to a direct-stream decoder; the hero refuses to mount at all unless the last
/// path segment is <c>.mp4</c> or <c>.m3u8</c>. Serving these through the
/// download-by-id redirect — the indirection every other file in this system
/// uses — would satisfy "the client can load it" and still break both surfaces:
/// visibly on the live screen, silently on the hero. So the store holds the link,
/// and the read path hands back exactly what was stored.</para></summary>
public interface IFeedLinkService
{
    /// <summary>Point an owner's feed at <paramref name="url"/>, or clear it when
    /// the URL is null or blank. Returns the file id to store on the owning row.
    /// The URL is validated by the file store against the same rule the players
    /// apply, so an unplayable link is refused here rather than discovered as an
    /// empty screen later.</summary>
    Task<Guid?> SetAsync(
        FileService service,
        Guid ownerId,
        string? url,
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    /// <summary>The URL behind one file id, or null when there is no id, no live
    /// row, or the row holds bytes rather than a link.</summary>
    Task<string?> ResolveAsync(Guid? fileId, CancellationToken cancellationToken = default);

    /// <summary>The URLs behind many file ids, in one query. For list projections,
    /// which would otherwise issue a lookup per row.</summary>
    Task<IReadOnlyDictionary<Guid, string>> ResolveManyAsync(
        IEnumerable<Guid?> fileIds,
        CancellationToken cancellationToken = default);
}
