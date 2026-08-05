// Tests: SIMF.Api.Tests/ExhibitorVisitorScanTests.cs
using FastEndpoints;
using SIMF.Api.Endpoints.Admin;
using SIMF.Api.RequestContext;
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
        var userId = User.ActorId();
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
        var userId = User.ActorId();
        var rows = await service.ListMyVisitorsAsync(userId, ct);
        await Send.OkAsync(ApiResult<IReadOnlyList<ExhibitorVisitorRow>>.Ok(rows), ct);
    }
}

/// <summary>FR-EXH-002 — the capture id on the route of the two per-lead
/// actions.</summary>
public sealed class ExhibitorVisitorRoute
{
    public Guid Id { get; set; }
}

/// <summary>FR-EXH-002 — DELETE one captured lead from the booth's list
/// (soft-delete, idempotent). My Contacts has had both a remove and an export
/// since D-286; the lead list had neither, so a mis-scan or a lead the visitor
/// asked to be dropped could not be removed at all. 403 unless the caller is a
/// current booth officer.</summary>
public sealed class RemoveCapturedVisitorEndpoint(IExhibitorVisitorService service)
    : Endpoint<ExhibitorVisitorRoute, ApiResult<bool>>
{
    public override void Configure()
    {
        Delete("/app/exhibitor/visitors/{id:guid}");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Exhibitor");
        Summary(s => s.Summary = "Remove a captured visitor from My Visitors.");
    }

    public override async Task HandleAsync(ExhibitorVisitorRoute req, CancellationToken ct)
    {
        var userId = User.ActorId();
        await service.RemoveCaptureAsync(userId, req.Id, ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}

/// <summary>FR-EXH-002 — GET one captured lead as a vCard 3.0 (.vcf), the same
/// export My Contacts offers and rendered by the same
/// <see cref="Contacts.VisitorCardVCard"/>. 404 when the capture is not on the
/// caller's booth or its subject is no longer eligible (DEF-EXH-004 — the export
/// must not become a second door onto a card the list refuses).</summary>
public sealed class CapturedVisitorVCardEndpoint(IExhibitorVisitorService service)
    : Endpoint<ExhibitorVisitorRoute>
{
    public override void Configure()
    {
        Get("/app/exhibitor/visitors/{id:guid}/vcard");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Exhibitor");
        Summary(s => s.Summary = "Export a captured visitor as a vCard.");
    }

    public override async Task HandleAsync(ExhibitorVisitorRoute req, CancellationToken ct)
    {
        var userId = User.ActorId();
        var card = await service.GetCaptureCardAsync(userId, req.Id, ct);

        HttpContext.Response.ContentType = Contacts.VisitorCardVCard.ContentType;
        HttpContext.Response.Headers.ContentDisposition =
            "attachment; filename=\"simf-lead.vcf\"";
        await HttpContext.Response.WriteAsync(Contacts.VisitorCardVCard.Build(card), ct);
    }
}
