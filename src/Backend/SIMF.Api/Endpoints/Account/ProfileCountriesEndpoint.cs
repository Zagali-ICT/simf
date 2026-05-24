// Tests: SIMF.Api.Tests/UserProfileTests.cs (Saudi present)
using FastEndpoints;
using SIMF.Common;
using SIMF.Contracts.UserProfile;

namespace SIMF.Api.Endpoints.Account;

/// <summary>
/// <c>GET /api/v1/account/user-profile/countries</c> — returns the
/// supported nationality picker list (decisions D-046 b, P8 — D-049;
/// renamed from <c>/account/visitor-profile/countries</c>). Auth required; the data
/// is not sensitive, but the endpoint sits under the same /account/
/// group as the rest of the profile surface so the CP / Website proxy
/// authentication stays uniform.
/// </summary>
public sealed class ProfileCountriesEndpoint
    : EndpointWithoutRequest<ApiResult<CountryListResponse>>
{
    public override void Configure()
    {
        Get("/account/user-profile/countries");
        Tags("Account");
        Summary(summary => summary.Summary =
            "Return the supported nationality list.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var countries = Countries.All
            .Select(country => new CountryDto(country.Code, country.NameEn, country.NameAr))
            .ToArray();
        await Send.OkAsync(
            ApiResult<CountryListResponse>.Ok(new CountryListResponse(countries)), ct);
    }
}
