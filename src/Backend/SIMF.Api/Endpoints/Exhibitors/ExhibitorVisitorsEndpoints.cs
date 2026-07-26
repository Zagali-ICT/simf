// Tests: SIMF.Api.Tests/ExhibitorVisitorScanTests.cs
using System.Security.Claims;
using FastEndpoints;
using SIMF.Api.Endpoints.Admin;
using SIMF.Application.Exhibitors.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Contacts;
using SIMF.Contracts.Exhibitors;

namespace SIMF.Api.Endpoints.Exhibitors;

// D-426 — exhibitor lead capture (app audience). App-only, no CP surface and no
// permission code — like the visitor contact-share feature it keys off
// RequireApprovedAccount + the app token; the exhibitor check is enforced in the
// service. DEF-EXH-001: that check is now "the caller's profile type carries
// MobileAppRole.Exhibitor" (D-519), not the old "any non-visitor type", which let
// Staff / Moderator / Media / Sponsor tokens harvest visitor PII.

/// <summary>POST — scan a visitor's entry-badge QR → capture to My Visitors +
/// return the visitor's full card. 403 unless the caller is an exhibitor, 404 if
/// no eligible visitor badge matches.</summary>
public sealed class ScanVisitorBadgeEndpoint(IExhibitorVisitorService service)
    : Endpoint<ScanVisitorBadgeRequest, ApiResult<VisitorCard>>
{
    public override void Configure()
    {
        Post("/app/exhibitor/visitors/scan");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Exhibitor");
        Summary(s => s.Summary = "Scan a visitor badge → capture to My Visitors.");
    }

    public override async Task HandleAsync(ScanVisitorBadgeRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var userId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        var card = await service.ScanByBadgeAsync(userId, req.QrId, req.Note, ct);
        await Send.OkAsync(ApiResult<VisitorCard>.Ok(card), ct);
    }
}

/// <summary>GET — the exhibitor's captured visitors (My Visitors), newest first,
/// each with the visitor's full card. 403 unless the caller is an exhibitor.</summary>
public sealed class ListMyVisitorsEndpoint(IExhibitorVisitorService service)
    : EndpointWithoutRequest<ApiResult<IReadOnlyList<ExhibitorVisitorRow>>>
{
    public override void Configure()
    {
        Get("/app/exhibitor/visitors");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Exhibitor");
        Summary(s => s.Summary = "List the exhibitor's captured visitors.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var userId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        var rows = await service.ListMyVisitorsAsync(userId, ct);
        await Send.OkAsync(ApiResult<IReadOnlyList<ExhibitorVisitorRow>>.Ok(rows), ct);
    }
}
