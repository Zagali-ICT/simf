using SIMF.Domain.Common;

namespace SIMF.Domain.Programme;

/// <summary>One presentation file a <see cref="Speaker"/> presents in a
/// <see cref="Session"/>; the bytes live in the <c>StoredFile</c> store.</summary>
public sealed class SpeakerPresentation : BaseAuditEntity
{
    public Guid SpeakerId { get; set; }
    public Speaker? Speaker { get; set; }

    public Guid SessionId { get; set; }
    public Session? Session { get; set; }

    /// <summary>The original upload name, which doubles as the display title.</summary>
    public string FileName { get; set; } = string.Empty;

    public Guid StoredFileId { get; set; }

    public string ContentType { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    /// <summary>The admin who uploaded the file. A bare Guid: the user lives in
    /// the Identity database.</summary>
    public Guid UploadedByUserId { get; set; }
}
