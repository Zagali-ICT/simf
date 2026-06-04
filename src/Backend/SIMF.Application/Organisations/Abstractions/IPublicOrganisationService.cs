using SIMF.Contracts.Organisations;

namespace SIMF.Application.Organisations.Abstractions;

/// <summary>Public, read-only organisation search that backs the bilingual
/// organisation picker (delegation / registration forms). Mirrors
/// IPublicBoothService.</summary>
public interface IPublicOrganisationService
{
    /// <summary>Active organisations matching the search text (Arabic or
    /// English name), capped at <paramref name="top"/> results and ordered
    /// for the picker. A null or empty <paramref name="search"/> returns the
    /// first page of active organisations.</summary>
    /// <param name="search">Optional name fragment to match.</param>
    /// <param name="top">Maximum number of items to return.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IReadOnlyList<OrganisationPickerItem>> SearchAsync(
        string? search, int top, CancellationToken ct = default);
}
