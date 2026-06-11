using SIMF.Common.Enums;
using SIMF.Domain.Common;

namespace SIMF.Domain.Assets;

/// <summary>
/// D-357 — the single, unified media asset record every image-bearing App entity
/// shares (speaker photo, company / sponsor / media-partner logo, archive cover,
/// news image). It is the one upload/download mechanism going forward; the older
/// per-entity <c>*RelativePath</c> columns are kept only as a read fallback and
/// are not extended.
///
/// <para>One row per (<see cref="Category"/>, <see cref="OwnerId"/>) while active —
/// enforced by a filtered unique index. <see cref="OwnerId"/> is a polymorphic
/// <b>bare Guid</b> (the owning entity differs by category), so it carries no DB
/// FK — the same logical-reference policy the Data/Identity separation uses
/// (D-157).</para>
///
/// <para>Bytes live OUTSIDE the row (D-90): an <see cref="AssetSourceType.Upload"/>
/// asset stores its file under <see cref="StoragePath"/> (with
/// <see cref="ContentType"/> / <see cref="SizeBytes"/>); an
/// <see cref="AssetSourceType.ExternalLink"/> asset references
/// <see cref="ExternalUrl"/> in place and stores no bytes. Soft-deleted via the
/// inherited <see cref="BaseAuditEntity.IsActive"/> /
/// <see cref="BaseAuditEntity.Deactivate"/>.</para>
/// </summary>
public sealed class Asset : BaseAuditEntity
{
    /// <summary>Which entity family this asset belongs to.</summary>
    public AssetCategory Category { get; set; }

    /// <summary>The owning entity's id — polymorphic per <see cref="Category"/>,
    /// a logical reference with no DB FK (D-157).</summary>
    public Guid OwnerId { get; set; }

    /// <summary>Image / Video / Document. Video is
    /// <see cref="AssetSourceType.ExternalLink"/> only.</summary>
    public AssetKind Kind { get; set; }

    /// <summary>Uploaded file vs external URL.</summary>
    public AssetSourceType SourceType { get; set; }

    /// <summary>Relative file name under the asset-storage root — set when
    /// <see cref="SourceType"/> is <see cref="AssetSourceType.Upload"/>.</summary>
    public string? StoragePath { get; set; }

    /// <summary>The external URL — set when <see cref="SourceType"/> is
    /// <see cref="AssetSourceType.ExternalLink"/>.</summary>
    public string? ExternalUrl { get; set; }

    /// <summary>MIME type of the stored bytes (Upload only).</summary>
    public string? ContentType { get; set; }

    /// <summary>Size of the stored bytes, in bytes (Upload only).</summary>
    public long? SizeBytes { get; set; }

    /// <summary>The original uploaded file name, for display (Upload only).</summary>
    public string? OriginalFileName { get; set; }
}
