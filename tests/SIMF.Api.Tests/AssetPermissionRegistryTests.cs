using SIMF.Common;
using SIMF.Common.Enums;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>D-357 — guard: every <see cref="AssetCategory"/> must map to a real
/// permission gate, so a new category can never ship on the generic asset
/// endpoints without an explicit view + write permission. Fails the build if a
/// category is added without wiring its gate in <see cref="AssetPermissionRegistry"/>.</summary>
public sealed class AssetPermissionRegistryTests
{
    [Fact]
    public void Every_asset_category_has_a_view_and_write_permission_gate()
    {
        foreach (var category in Enum.GetValues<AssetCategory>())
        {
            var gate = AssetPermissionRegistry.For(category); // throws if unmapped
            Assert.False(string.IsNullOrWhiteSpace(gate.ViewPermission),
                $"{category} has no view permission.");
            Assert.False(string.IsNullOrWhiteSpace(gate.WritePermission),
                $"{category} has no write permission.");
        }
    }

    [Fact]
    public void Mapped_categories_cover_the_whole_enum()
    {
        Assert.Equal(Enum.GetValues<AssetCategory>().Length,
            AssetPermissionRegistry.MappedCategories.Count);
    }
}
