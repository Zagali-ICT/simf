// Tests: SIMF.Api.Tests/PartnerDirectoryServiceTests.cs
using FastEndpoints;
using SIMF.Api.Endpoints.Admin;
using SIMF.Application.Networking.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Networking;

namespace SIMF.Api.Endpoints.Networking;

/// <summary>Build #13 — "Meet People Like You" partner directory. Returns the
/// deduped union of curated Sponsors, Speakers and Booth companies plus the
/// opted-in "Other"-type accounts (Normal / VIP visitors excluded). Empty when the
/// CP switch is off. Requires an approved authenticated account (matches the
/// recommender + networking endpoints — no permission code).</summary>
public sealed class PartnerDirectoryEndpoint(IPartnerDirectoryService service)
    : EndpointWithoutRequest<ApiResult<PartnerDirectoryResponse>>
{
    public override void Configure()
    {
        Get("/app/networking/partner-directory");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Account");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await service.GetAsync(ct);
        await Send.OkAsync(ApiResult<PartnerDirectoryResponse>.Ok(result), ct);
    }
}
