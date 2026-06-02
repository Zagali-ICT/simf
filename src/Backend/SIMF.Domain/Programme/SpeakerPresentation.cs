namespace SIMF.Domain.Programme;

/// <summary>P2.3 — D-228 (SIMF-FDS-004 §5.3 + §7, FR-407): one presentation
/// file a <see cref="Speaker"/> presents in a <see cref="Session"/>. The bytes
/// live OUTSIDE the row (decision D-90, same policy as media images / avatars)
/// via <c>ISpeakerPresentationStorage</c>; this row keeps only the metadata +
/// the storage key. Soft-deleted via <see cref="IsActive"/>.</summary>
public sealed class SpeakerPresentation
{
    public Guid Id { get; set; }

    /// <summary>The presenting speaker. Real FK (same DbContext), cascade.</summary>
    public Guid SpeakerId { get; set; }
    public Speaker? Speaker { get; set; }

    /// <summary>The session the file is presented in. Real FK (same DbContext),
    /// restrict (a session in use cannot be hard-deleted; sessions soft-delete).</summary>
    public Guid SessionId { get; set; }
    public Session? Session { get; set; }

    /// <summary>The original upload file name, also used as the display title
    /// (≤256).</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>The storage key returned by <c>ISpeakerPresentationStorage</c>
    /// (a file name under the configured root, ≤256).</summary>
    public string StoredFileName { get; set; } = string.Empty;

    /// <summary>The upload MIME type (≤128).</summary>
    public string ContentType { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    /// <summary>The admin (logical FK to SimfUser on the Identity DB) who
    /// uploaded the file.</summary>
    public Guid UploadedByUserId { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }

    public void Deactivate() => IsActive = false;
}
