// Tests: SIMF.Api.Tests/VisitorContactSharingTests.cs
using FastEndpoints;
using SIMF.Api.Endpoints.Admin;
using SIMF.Api.RequestContext;
using SIMF.Application.Contacts.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Contacts;

namespace SIMF.Api.Endpoints.Contacts;

// SIMF-FDS-014 §5.7 (D-284, Track 2) — visitor-to-visitor contact sharing (app
// audience). App-only, no CP surface and no permission code — these self-service
// visitor features key off RequireApprovedAccount + the app token (matching
// Connection), which does NOT violate the new-page-needs-
// permission rule (that governs CP pages / admin API actions).

/// <summary>GET the caller's share token, minting one on first request.</summary>
public sealed class GetShareTokenEndpoint(IVisitorShareService service)
    : EndpointWithoutRequest<ApiResult<VisitorShareTokenResponse>>
{
    public override void Configure()
    {
        Get("/app/account/share-token");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Account");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = User.ActorId();
        await Send.OkAsync(ApiResult<VisitorShareTokenResponse>.Ok(
            await service.GetOrMintTokenAsync(userId, ct)), ct);
    }
}

/// <summary>POST — rotate the caller's share token (old code stops resolving).</summary>
public sealed class RotateShareTokenEndpoint(IVisitorShareService service)
    : EndpointWithoutRequest<ApiResult<VisitorShareTokenResponse>>
{
    public override void Configure()
    {
        Post("/app/account/share-token/rotate");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Account");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = User.ActorId();
        await Send.OkAsync(ApiResult<VisitorShareTokenResponse>.Ok(
            await service.RotateTokenAsync(userId, ct)), ct);
    }
}

/// <summary>POST — resolve a scanned token to the owner's live card.</summary>
public sealed class ResolveContactEndpoint(IVisitorShareService service)
    : Endpoint<ResolveContactRequest, ApiResult<VisitorCard>>
{
    public override void Configure()
    {
        Post("/app/contacts/resolve");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Account");
    }

    public override async Task HandleAsync(ResolveContactRequest req, CancellationToken ct) =>
        await Send.OkAsync(ApiResult<VisitorCard>.Ok(
            await service.ResolveAsync(req.Token, ct)), ct);
}

/// <summary>POST — save the holder of a scanned token to My Contacts.</summary>
public sealed class SaveContactEndpoint(IVisitorShareService service)
    : Endpoint<SaveContactRequest, ApiResult<SavedContactRow>>
{
    public override void Configure()
    {
        Post("/app/contacts/save");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Account");
    }

    public override async Task HandleAsync(SaveContactRequest req, CancellationToken ct)
    {
        var ownerUserId = User.ActorId();
        await Send.OkAsync(ApiResult<SavedContactRow>.Ok(
            await service.SaveAsync(ownerUserId, req.Token, req.Note, ct)), ct);
    }
}

/// <summary>GET — the caller's My Contacts list (cards resolved on read).</summary>
public sealed class ListSavedContactsEndpoint(IVisitorShareService service)
    : EndpointWithoutRequest<ApiResult<IReadOnlyList<SavedContactRow>>>
{
    public override void Configure()
    {
        Get("/app/contacts");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Account");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var ownerUserId = User.ActorId();
        await Send.OkAsync(ApiResult<IReadOnlyList<SavedContactRow>>.Ok(
            await service.ListSavedAsync(ownerUserId, ct)), ct);
    }
}

public sealed class SavedContactRoute { public Guid Id { get; set; } }

/// <summary>DELETE — remove one of the caller's saved contacts (soft-delete).</summary>
public sealed class RemoveSavedContactEndpoint(IVisitorShareService service)
    : Endpoint<SavedContactRoute, ApiResult<bool>>
{
    public override void Configure()
    {
        Delete("/app/contacts/{id:guid}");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Account");
    }

    public override async Task HandleAsync(SavedContactRoute req, CancellationToken ct)
    {
        var ownerUserId = User.ActorId();
        await service.RemoveSavedAsync(ownerUserId, req.Id, ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}

/// <summary>GET — export a saved contact as a vCard 3.0 (.vcf). The client may
/// also build the vCard from the card DTO itself (§5.7).</summary>
public sealed class SavedContactVCardEndpoint(IVisitorShareService service)
    : Endpoint<SavedContactRoute>
{
    public override void Configure()
    {
        Get("/app/contacts/{id:guid}/vcard");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Account");
    }

    public override async Task HandleAsync(SavedContactRoute req, CancellationToken ct)
    {
        var ownerUserId = User.ActorId();
        var card = await service.GetSavedCardAsync(ownerUserId, req.Id, ct);

        // FR-EXH-002 — the rendering moved to the shared VisitorCardVCard so the
        // exhibitor lead export is the same vCard, not a second copy of it. The
        // bytes this endpoint returns are unchanged.
        HttpContext.Response.ContentType = VisitorCardVCard.ContentType;
        HttpContext.Response.Headers.ContentDisposition = "attachment; filename=\"simf-contact.vcf\"";
        await HttpContext.Response.WriteAsync(VisitorCardVCard.Build(card), ct);
    }
}
