using System.Linq;
using SIMF.Common;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>
/// D-469 — the Saudi administrative-region constant used by the birth-location
/// dropdown (mirrored by the app's <c>saudi_regions.dart</c>).
/// </summary>
public sealed class SaudiRegionsTests
{
    [Fact]
    public void There_are_13_regions_with_unique_codes_and_names()
    {
        Assert.Equal(13, SaudiRegions.All.Count);
        Assert.Equal(13, SaudiRegions.All.Select(r => r.Code).Distinct().Count());
        Assert.All(SaudiRegions.All, region =>
        {
            Assert.False(string.IsNullOrWhiteSpace(region.Code));
            Assert.False(string.IsNullOrWhiteSpace(region.Arabic));
            Assert.False(string.IsNullOrWhiteSpace(region.English));
        });
    }

    [Fact]
    public void ByCode_resolves_a_known_region_and_null_for_unknown()
    {
        var riyadh = SaudiRegions.ByCode("riyadh");
        Assert.NotNull(riyadh);
        Assert.Equal("Riyadh", riyadh!.English);
        Assert.Equal("منطقة الرياض", riyadh.Arabic);

        Assert.Null(SaudiRegions.ByCode("atlantis"));
        Assert.Null(SaudiRegions.ByCode(null));
    }

    [Theory]
    [InlineData("Riyadh", "riyadh")]            // stored under an English session
    [InlineData("منطقة الرياض", "riyadh")]      // stored under an Arabic session
    [InlineData("  Eastern Province  ", "eastern")] // trimmed
    public void ByName_maps_either_language_back_to_the_code(string stored, string expectedCode)
    {
        var region = SaudiRegions.ByName(stored);
        Assert.NotNull(region);
        Assert.Equal(expectedCode, region!.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("Some passport city")]
    public void ByName_returns_null_for_blank_or_non_region(string? stored)
    {
        Assert.Null(SaudiRegions.ByName(stored));
    }
}
