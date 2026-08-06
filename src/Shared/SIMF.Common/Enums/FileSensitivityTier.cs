namespace SIMF.Common.Enums;

/// <summary>The data-classification tier of a <c>StoredFile</c>, derived
/// from its <see cref="FileService"/> at write time and persisted so the
/// classification is auditable rather than inferred (SAMA A1-3 / NCA ECC 2-7-2).
///
/// <para>Persisted as an int; <b>append-only</b>.</para></summary>
public enum FileSensitivityTier
{
    /// <summary>Publicly servable (logos, gallery, news, banners).</summary>
    Public = 0,

    /// <summary>Internal — any signed-in approved account (presentations, recordings).</summary>
    Internal = 1,

    /// <summary>Confidential — admin or owner only (VIP photo, avatar).</summary>
    Confidential = 2,

    /// <summary>Secret — owner or admin only, no-store, fully audited (ID document).</summary>
    Secret = 3,
}
