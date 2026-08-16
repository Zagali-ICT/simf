using SIMF.Domain.Common;
using SIMF.Domain.Files;

namespace SIMF.Domain.Programme;

/// <summary>One presentation file a <see cref="Speaker"/> presents in a
/// <see cref="Session"/>; the bytes live in the <c>StoredFile</c> store.</summary>
public sealed class SpeakerPresentation : BaseAuditEntity
{
    public Guid SpeakerId { get; set; }
    public Speaker? Speaker { get; set; }

    public Guid SessionId { get; set; }
    public Session? Session { get; set; }

    public Guid StoredFileId { get; set; }

    /// <summary>The bytes, and everything about them: the store row owns the
    /// name, the media type, the size and the uploader.
    ///
    /// <para>This entity carried its own copy of all four until they were
    /// removed. The media type is why it mattered rather than being untidy:
    /// <c>IFileService</c> canonicalises it on upload while the copy kept the raw
    /// string the client sent, so the two disagreed for any client that sent a
    /// non-canonical type - and the copy was the one the admin grid displayed.
    /// The uploader and the upload instant are <c>CreatedBy</c> and
    /// <c>CreatedAt</c> on the store row, which is a <c>BaseAuditEntity</c>.</para></summary>
    public StoredFile? StoredFile { get; set; }
}
