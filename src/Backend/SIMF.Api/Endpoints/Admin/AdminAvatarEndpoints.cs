// Tests: SIMF.Api.Tests/WalkInRegistrationTests.cs (Admin_uploads_visitor_avatar_sets_path)
using System.Security.Claims;
using FastEndpoints;
using SIMF.Api.Endpoints.Account;
using SIMF.Application.Files.Abstractions;
using SIMF.Application.IdentityAccess;
using SIMF.Common;
using SIMF.Contracts.Authentication;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// D-427 (CS-3) — admin upload of a subject's profile photo (avatar) for the
/// walk-in flow. The avatar is the visitor's photo / "logo", distinct from the
/// ID-document image, and is shown alongside the ID image in the approve modal
/// (CS-4). Reuses <see cref="IAccountService.SetAvatarAsync"/>, which is
/// id-parameterised and already enforces the 2 MB cap + MIME + magic-byte gate
/// (no human-face requirement — it is a profile photo, optionally a logo, D-427
/// owner decision). Permission-gated like the admin ID-document upload
/// (Visitors.Edit / Others.Edit); the same SubjectId route shape.
/// </summary>
public abstract class AdminAvatarUploadEndpointBase(IAccountService accountService)
    : Endpoint<EmptyRequest, ApiResult<AvatarResponse>>
{
    public abstract Guid SubjectId { get; }

    public override async Task HandleAsync(EmptyRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out _))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var file = Files.GetFile("file");
        if (file is null || file.Length == 0)
        {
            throw new ApiException(
                ErrorCodes.AvatarFileMissing, 400,
                "An avatar file is required.",
                "ملف الصورة مطلوب.");
        }

        using var stream = new MemoryStream();
        await file.CopyToAsync(stream, ct);
        // SetAvatarAsync validates size (2 MB) + MIME + magic bytes, stores the
        // file and sets SimfUser.AvatarRelativePath for the subject.
        var response = await accountService.SetAvatarAsync(
            SubjectId, stream.ToArray(), file.ContentType ?? string.Empty, ct);
        await Send.OkAsync(ApiResult<AvatarResponse>.Ok(response), ct);
    }
}

/// <summary><c>POST /api/v1/admin/visitors/{id}/avatar</c>.</summary>
public sealed class UploadVisitorAvatarEndpoint(IAccountService accountService)
    : AdminAvatarUploadEndpointBase(accountService)
{
    public override Guid SubjectId => Route<Guid>("id");

    public override void Configure()
    {
        Post("/admin/visitors/{id:guid}/avatar");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Visitors.Edit), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        AllowFileUploads();
        Summary(summary => summary.Summary =
            "Admin upload of a visitor's profile photo (avatar).");
    }
}

/// <summary><c>POST /api/v1/admin/others/{id}/avatar</c>.</summary>
public sealed class UploadOtherAvatarEndpoint(IAccountService accountService)
    : AdminAvatarUploadEndpointBase(accountService)
{
    public override Guid SubjectId => Route<Guid>("id");

    public override void Configure()
    {
        Post("/admin/others/{id:guid}/avatar");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Others.Edit), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        AllowFileUploads();
        Summary(summary => summary.Summary =
            "Admin upload of an Other account's profile photo (avatar).");
    }
}

/// <summary>
/// CS-4 — admin stream-read of a subject's profile photo (avatar) so the CP
/// approve modal can render it alongside the ID image. Mirrors the self-service
/// <c>account/avatar/{userId}</c> fetch but drops the self-only guard for an
/// admin View permission (the avatar is the account's, on SimfUser/Identity).
/// </summary>
public abstract class AdminAvatarFetchEndpointBase(
    SimfAppDbContext appDb, IFileStorageProvider storage)
    : EndpointWithoutRequest
{
    public abstract Guid SubjectId { get; }

    public override async Task HandleAsync(CancellationToken ct)
    {
        // D-568 (S3) — resolve the subject's avatar from the StoredFile store.
        // Authorization is the route's admin View permission (Configure below);
        // this is a raw decrypt read, not IFileService.DownloadAsync (see AvatarBytes).
        var avatar = await AvatarBytes.ReadAsync(appDb, storage, SubjectId, ct);
        if (avatar is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        HttpContext.Response.Headers.CacheControl = "private, max-age=60";
        await Send.StreamAsync(
            new MemoryStream(avatar.Value.Content), contentType: avatar.Value.ContentType, cancellation: ct);
    }
}

/// <summary><c>GET /api/v1/admin/visitors/{id}/avatar</c>.</summary>
public sealed class FetchVisitorAvatarEndpoint(
    SimfAppDbContext appDb, IFileStorageProvider storage)
    : AdminAvatarFetchEndpointBase(appDb, storage)
{
    public override Guid SubjectId => Route<Guid>("id");

    public override void Configure()
    {
        Get("/admin/visitors/{id:guid}/avatar");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Visitors.View), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Summary(summary => summary.Summary =
            "Stream a visitor's profile photo (avatar).");
    }
}

/// <summary><c>GET /api/v1/admin/others/{id}/avatar</c>.</summary>
public sealed class FetchOtherAvatarEndpoint(
    SimfAppDbContext appDb, IFileStorageProvider storage)
    : AdminAvatarFetchEndpointBase(appDb, storage)
{
    public override Guid SubjectId => Route<Guid>("id");

    public override void Configure()
    {
        Get("/admin/others/{id:guid}/avatar");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Others.View), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Summary(summary => summary.Summary =
            "Stream an Other account's profile photo (avatar).");
    }
}
