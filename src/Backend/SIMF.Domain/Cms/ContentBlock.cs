namespace SIMF.Domain.Cms;

/// <summary>
/// D-173 (gap doc G8, PDF §1 + §2.1) — one editable piece of public
/// content (welcome message, page heading, body copy, label).
/// Identified by a stable <see cref="Key"/> slug the client codes
/// against (e.g. <c>home.welcome.title</c>); the EN + AR text is
/// edited by an admin at runtime from the CP, no code change needed.
///
/// <para>The Flutter app and the Website both read these via the
/// public <c>GET /api/v1/content/{key}</c> endpoint and cache locally
/// with an <c>If-Modified-Since</c> header keyed on
/// <see cref="LastUpdatedAt"/>.</para>
/// </summary>
public sealed class ContentBlock
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Stable slug — lower-kebab-case, dotted hierarchy. The
    /// client codes against this value; renaming a key is a wire-
    /// breaking change. Up to 128 chars, unique across the table.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>English content (markdown allowed). Up to 8000 chars
    /// so a long body / article body fits without a separate
    /// "long-form" table.</summary>
    public string ContentEn { get; set; } = string.Empty;

    /// <summary>Arabic content (markdown allowed). Same shape as
    /// <see cref="ContentEn"/>.</summary>
    public string ContentAr { get; set; } = string.Empty;

    /// <summary>Soft-delete flag — hides the row from the public read
    /// endpoint without losing the editor's text.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>The admin who last saved the row. Logical FK to
    /// <c>SimfUser.Id</c> on the Identity DB.</summary>
    public Guid LastUpdatedByUserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset LastUpdatedAt { get; set; }
}
