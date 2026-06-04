// Tests: SIMF.Api.Tests/SessionCommentsTests.cs
using System.Security.Claims;
using FastEndpoints;
using SIMF.Api.Endpoints.Admin;
using SIMF.Application.SessionComments.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Sessions;

namespace SIMF.Api.Endpoints.Sessions;

/// <summary>D-199 (Mockup page 28) — admin moderation list over a
/// session's audience comments. Optional status filter (e.g. only
/// Pending for the moderation queue). Mirrors the admin grid shape
/// (POST /list with the paging fields in the body). <c>GridQuery</c>
/// is sealed, so the paging fields are composed here (matching the
/// canonical <c>Skip</c>/<c>Top</c>/<c>Search</c>/<c>Sort</c> names)
/// and the handler builds a <c>GridQuery</c> from them.</summary>
public sealed class ListSessionCommentsModerationRoute
{
    public Guid SessionId { get; set; }

    /// <summary>Optional moderation-status filter. Null = all statuses
    /// (Pending + Approved + Hidden), excluding soft-deleted.</summary>
    public SessionCommentStatus? Status { get; set; }

    /// <summary>Row offset (first row to return). Default 0.</summary>
    public int Skip { get; set; }

    /// <summary>Page size. Default 25, clamped to 200 at the service.</summary>
    public int Top { get; set; } = 25;

    /// <summary>Free-text search across the comment body.</summary>
    public string? Search { get; set; }

    /// <summary>Sort key — only <c>created</c> is honoured today.</summary>
    public string? Sort { get; set; }

    /// <summary>When true (default), newest first.</summary>
    public bool SortDescending { get; set; } = true;
}

public sealed class ListSessionCommentsModerationEndpoint(IAdminSessionCommentService service)
    : Endpoint<ListSessionCommentsModerationRoute, ApiResult<GridPage<SessionCommentModerationRow>>>
{
    public override void Configure()
    {
        Post("/admin/sessions/{sessionId:guid}/comments/list");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Comments.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(
        ListSessionCommentsModerationRoute req, CancellationToken ct)
    {
        var query = new GridQuery
        {
            Skip = req.Skip,
            Top = req.Top,
            Search = req.Search,
            Sort = req.Sort,
            SortDescending = req.SortDescending,
        };
        await Send.OkAsync(ApiResult<GridPage<SessionCommentModerationRow>>.Ok(
            await service.ListAsync(req.SessionId, req.Status, query, ct)), ct);
    }
}

/// <summary>D-199 — set a comment's moderation status
/// (approve / hide / re-pend). Idempotent.</summary>
public sealed class SetSessionCommentStatusRoute
{
    public Guid SessionId { get; set; }
    public Guid CommentId { get; set; }
    public SessionCommentStatus Status { get; set; }
}

public sealed class SetSessionCommentStatusEndpoint(IAdminSessionCommentService service)
    : Endpoint<SetSessionCommentStatusRoute, ApiResult<SessionCommentModerationRow>>
{
    public override void Configure()
    {
        Put("/admin/sessions/{sessionId:guid}/comments/{commentId:guid}/status");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Comments.Moderate),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(
        SetSessionCommentStatusRoute req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        var row = await service.SetStatusAsync(
            actorId, req.SessionId, req.CommentId, req.Status, ct);
        await Send.OkAsync(ApiResult<SessionCommentModerationRow>.Ok(row), ct);
    }
}

/// <summary>D-199 — soft-delete a comment (CLAUDE.md §7 — sets
/// IsActive = false; the row is retained for audit).</summary>
public sealed class DeactivateSessionCommentRoute
{
    public Guid SessionId { get; set; }
    public Guid CommentId { get; set; }
}

public sealed class DeactivateSessionCommentEndpoint(IAdminSessionCommentService service)
    : Endpoint<DeactivateSessionCommentRoute, ApiResult<bool>>
{
    public override void Configure()
    {
        Delete("/admin/sessions/{sessionId:guid}/comments/{commentId:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Comments.Moderate),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(
        DeactivateSessionCommentRoute req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await service.DeactivateAsync(actorId, req.SessionId, req.CommentId, ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}
