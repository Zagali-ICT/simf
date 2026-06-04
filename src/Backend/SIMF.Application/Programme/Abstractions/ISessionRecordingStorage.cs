namespace SIMF.Application.Programme.Abstractions;

/// <summary>P3.2b — D-232 (D-213): out-of-row storage for session recordings,
/// streamed both ways so a large MP4 never lands wholly in memory (the D-213
/// "no whole-file ReadAllBytes" rule). The read side mirrors
/// <c>IAvatarStorage.OpenReadAsync</c> (which also returns a <see cref="Stream"/>);
/// this seam differs only in (1) <see cref="SaveAsync"/> takes a <see cref="Stream"/>
/// IN — the image/presentation seams take a <c>byte[]</c> — and (2) the read
/// record carries <c>Length</c> instead of a content-type, because HTTP range
/// streaming needs the total length up front (the content-type is read off the
/// session row). Streamed-both-ways is the preferred seam for large/binary media
/// going forward; the byte[] seams remain only for small images/presentations.
/// The filesystem impl can later be swapped for MinIO / object storage behind
/// this same seam (deferred upgrade path).</summary>
public interface ISessionRecordingStorage
{
    /// <summary>Streams <paramref name="content"/> to disk under a name derived
    /// from <paramref name="sessionId"/> (one file per session — re-upload
    /// replaces) and returns the stored file name the caller persists.</summary>
    Task<string> SaveAsync(
        Guid sessionId, Stream content, string originalFileName,
        CancellationToken cancellationToken = default);

    /// <summary>Opens the stored recording for reading, or null if the file is
    /// missing. The caller (the streaming endpoint) owns the returned stream
    /// and disposes it after sending. Not async — opening a file handle is
    /// synchronous; the read itself is async via range streaming.</summary>
    SessionRecordingStream? OpenRead(string storedFileName);

    Task DeleteAsync(
        string storedFileName, CancellationToken cancellationToken = default);
}

/// <summary>A seekable read stream over a stored recording plus its byte length
/// (HTTP range streaming needs the total length up front).</summary>
public sealed record SessionRecordingStream(Stream Content, long Length);
