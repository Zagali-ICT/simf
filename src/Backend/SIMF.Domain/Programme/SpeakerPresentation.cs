using SIMF.Domain.Common;

namespace SIMF.Domain.Programme;

/// <summary>
/// One presentation file a <see cref="Speaker"/> presents in a
/// <see cref="Session"/>. The bytes live in the unified file store, as they do
/// for media images and avatars; this row keeps the metadata and points at the
/// <c>StoredFile</c> that holds them.
/// </summary>
public sealed class SpeakerPresentation : BaseAuditEntity
{
    /// <summary>Cascade-deleted with the speaker.</summary>
    public Guid SpeakerId { get; set; }
    public Speaker? Speaker { get; set; }

    /// <summary>Delete is restricted here rather than cascading, since sessions
    /// soft-delete and a session in use should not be removable.</summary>
    public Guid SessionId { get; set; }
    public Session? Session { get; set; }

    /// <summary>The original upload name, which doubles as the display
    /// title.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>The <c>StoredFile</c> holding the bytes. A real foreign key, and
    /// required: a presentation row without a file has nothing to present.
    ///
    /// <para>This was <c>StoredFileName</c>, a string, and its comment still
    /// described "a file name under the configured root" long after the value
    /// had become a <c>StoredFile</c> id written as text.</para></summary>
    public Guid StoredFileId { get; set; }

    public string ContentType { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    /// <summary>The admin who uploaded the file. A bare Guid: the user lives in
    /// the Identity database.</summary>
    public Guid UploadedByUserId { get; set; }
}
