// Tests: SIMF.Api.Tests/WalkInRegistrationTests.cs (Admin_uploads_vip_photo_sets_path)
using System.Security.Claims;
using FastEndpoints;
using SIMF.Application.IdentityAccess;
using SIMF.Common;
using SIMF.Common.Enums;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// V-1 (D-429) — admin upload + read of a visitor's VVIP/VIP welcome photo
/// (صورة واضحة) for the موج (Mawj) integration. The photo is distinct from the
/// account avatar and the ID image: it lives in its own store and is written to
/// <c>UserProfile.VipPhotoRelativePath</c>. Captured on the dedicated VIP
/// registration page; surfaced + downloaded on the VIP roster export page. Same
/// 2 MB + MIME + magic-byte gate as the avatar upload (no human-face gate — a VIP
/// portrait may be a formal headshot or an official emblem). Permission-gated like
/// the avatar / ID-document admin uploads (Visitors.Edit / Visitors.View).
/// </summary>
public sealed class UploadVisitorVipPhotoEndpoint(IUserProfileService service)
    : Endpoint<EmptyRequest, ApiResult<bool>>
{
    /// <summary>2 MB cap — same as the avatar upload.</summary>
    private const long MaxBytes = 2L * 1024 * 1024;

    public override void Configure()
    {
        Post("/admin/visitors/{id:guid}/vip-photo");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Visitors.Edit), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        AllowFileUploads();
        Summary(summary => summary.Summary =
            "Admin upload of a visitor's VVIP/VIP welcome photo (موج).");
    }

    public override async Task HandleAsync(EmptyRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var file = Files.GetFile("file");
        if (file is null || file.Length == 0)
        {
            throw new ApiException(
                ErrorCodes.AvatarFileMissing, 400,
                "A photo file is required.",
                "ملف الصورة مطلوب.");
        }
        if (file.Length > MaxBytes)
        {
            throw new ApiException(
                ErrorCodes.AvatarFileTooLarge, 413,
                "The photo must be 2 MB or less.",
                "يجب ألا يتجاوز حجم الصورة 2 ميغابايت.");
        }

        using var stream = new MemoryStream();
        await file.CopyToAsync(stream, ct);
        var bytes = stream.ToArray();
        var contentType = (file.ContentType ?? string.Empty).Trim().ToLowerInvariant();

        if (!ImageUploadValidation.IsAllowedImage(bytes, contentType))
        {
            throw new ApiException(
                ErrorCodes.AvatarMimeUnsupported, 400,
                "The photo must be PNG, JPEG or WebP.",
                "يجب أن تكون الصورة بصيغة PNG أو JPEG أو WebP.");
        }

        await service.UploadVipPhotoForSubjectAsync(
            actorId, Route<Guid>("id"), UserType.Visitor, bytes, contentType, ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}

/// <summary><c>GET /api/v1/admin/visitors/{id}/vip-photo</c> — streams the VIP
/// welcome photo back so the CP roster / export page can render and download it.</summary>
public sealed class FetchVisitorVipPhotoEndpoint(IUserProfileService service)
    : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/admin/visitors/{id:guid}/vip-photo");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Visitors.View), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Summary(summary => summary.Summary =
            "Stream a visitor's VVIP/VIP welcome photo (موج).");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var photo = await service.ReadVipPhotoForSubjectAsync(
            Route<Guid>("id"), UserType.Visitor, ct);
        if (photo is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        HttpContext.Response.Headers.CacheControl = "private, max-age=60";
        await Send.BytesAsync(photo.Content, contentType: photo.ContentType, cancellation: ct);
    }
}
