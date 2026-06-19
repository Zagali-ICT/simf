using SIMF.Common.Enums;

namespace SIMF.Common;

/// <summary>D-357 — maps each <see cref="AssetCategory"/> to the existing
/// per-entity permission codes that gate writing (upload / set-link / remove) and
/// reading (admin fetch) its asset. The generic asset endpoints resolve the
/// required permission through this registry and enforce it imperatively, so a
/// new category cannot ship without an explicit gate — a guard test asserts every
/// <see cref="AssetCategory"/> value is mapped.</summary>
public static class AssetPermissionRegistry
{
    /// <summary>The view (admin fetch) + write (upload / link / remove) permission
    /// codes that gate a category's asset.</summary>
    public sealed record Gate(string ViewPermission, string WritePermission);

    private static readonly IReadOnlyDictionary<AssetCategory, Gate> Map =
        new Dictionary<AssetCategory, Gate>
        {
            [AssetCategory.SpeakerPhoto] =
                new(PermissionCatalog.Speakers.View, PermissionCatalog.Speakers.Edit),
            [AssetCategory.CompanyLogo] =
                new(PermissionCatalog.Contacts.View, PermissionCatalog.Contacts.Edit),
            [AssetCategory.MediaPartnerLogo] =
                new(PermissionCatalog.MediaPartners.View, PermissionCatalog.MediaPartners.Edit),
            [AssetCategory.SponsorLogo] =
                new(PermissionCatalog.Sponsors.View, PermissionCatalog.Sponsors.Edit),
            [AssetCategory.ArchiveCover] =
                new(PermissionCatalog.Archive.View, PermissionCatalog.Archive.Edit),
            [AssetCategory.NewsImage] =
                new(PermissionCatalog.News.View, PermissionCatalog.News.Edit),
            [AssetCategory.ProgrammeDayImage] =
                new(PermissionCatalog.ProgrammeDays.View, PermissionCatalog.ProgrammeDays.Edit),
        };

    /// <summary>The view + write permission codes for a category. Throws if the
    /// category has no mapped gate (caught by the guard test before it ships).</summary>
    public static Gate For(AssetCategory category) =>
        Map.TryGetValue(category, out var gate)
            ? gate
            : throw new ArgumentOutOfRangeException(
                nameof(category), category,
                "No permission gate is mapped for this asset category.");

    /// <summary>All mapped categories — used by the guard test that fails the
    /// build if a category ships without a gate.</summary>
    public static IReadOnlyCollection<AssetCategory> MappedCategories =>
        (IReadOnlyCollection<AssetCategory>)Map.Keys;
}
