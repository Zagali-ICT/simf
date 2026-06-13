// Tests: SIMF.Api.Tests/UserProfileTests.cs (Saudi present)
using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SIMF.Common;
using SIMF.Contracts.UserProfile;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Api.Endpoints.Account;

/// <summary>
/// <c>GET /api/v1/app/account/user-profile/countries</c> — returns the
/// supported nationality picker list (decisions D-046 b, P8 — D-049;
/// renamed from <c>/account/visitor-profile/countries</c>). Auth required; the data
/// is not sensitive, but the endpoint sits under the same /account/
/// group as the rest of the profile surface so the CP / Website proxy
/// authentication stays uniform.
///
/// <para>D-151 — reads the live <see cref="SIMF.Domain.Common.Country"/>
/// table (admin-curated) instead of the retired
/// <c>SIMF.Common.Countries</c> static list, ordered by the admin-set
/// <c>DisplayOrder</c> then the English name as a stable tiebreaker.</para>
/// </summary>
public sealed class ProfileCountriesEndpoint(SimfAppDbContext appDb)
    : EndpointWithoutRequest<ApiResult<CountryListResponse>>
{
    public override void Configure()
    {
        Get("/app/account/user-profile/countries");
        Tags("Account");
        Summary(summary => summary.Summary =
            "Return the supported nationality list.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var countries = await appDb.Countries
            .AsNoTracking()
            .Where(country => country.IsActive)
            .OrderBy(country => country.DisplayOrder)
            .ThenBy(country => country.Name)
            .Select(country => new CountryDto(country.Code, country.Name, country.NameArabic))
            .ToArrayAsync(ct);
        await Send.OkAsync(
            ApiResult<CountryListResponse>.Ok(new CountryListResponse(countries)), ct);
    }
}
