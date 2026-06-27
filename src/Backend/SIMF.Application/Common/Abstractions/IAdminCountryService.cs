using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.Application.Common.Abstractions;

/// <summary>D-151 — admin CRUD over <c>Country</c>. Id is the ISO 3166-1
/// numeric code, manually assigned at create time (NOT IDENTITY).</summary>
public interface IAdminCountryService
{
    Task<GridPage<AdminCountrySummary>> ListAllAsync(
        GridQuery query, CancellationToken cancellationToken = default);

    Task<AdminCountryDetail?> GetAsync(
        int id, CancellationToken cancellationToken = default);

    Task<AdminCountryDetail> CreateAsync(
        Guid actorUserId, AdminCreateCountryRequest request,
        CancellationToken cancellationToken = default);

    Task<AdminCountryDetail> UpdateAsync(
        Guid actorUserId, int id, AdminUpdateCountryRequest request,
        CancellationToken cancellationToken = default);

    Task DeactivateAsync(
        Guid actorUserId, int id,
        CancellationToken cancellationToken = default);

    /// <summary>D-499 (الوفود) — the active delegates of a country, offered in the
    /// CP head-of-delegation picker on the country Edit form.</summary>
    Task<IReadOnlyList<AdminCountryDelegateOption>> ListDelegatesAsync(
        int countryId, CancellationToken cancellationToken = default);
}
