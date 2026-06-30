using SIMF.Contracts.Regions;

namespace SIMF.Application.Regions.Abstractions;

/// <summary>Public, read-only region list that backs the app's region picker.
/// Mirrors IPublicOrganisationService.</summary>
public interface IPublicRegionService
{
    /// <summary>The active regions, ordered for the picker (by SortOrder then
    /// Arabic name).</summary>
    /// <param name="ct">Cancellation token.</param>
    Task<IReadOnlyList<RegionPickerItem>> GetActiveRegionsAsync(
        CancellationToken ct = default);
}
