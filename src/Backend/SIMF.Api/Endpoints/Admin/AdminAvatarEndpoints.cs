// Tests: SIMF.Api.Tests/WalkInRegistrationTests.cs (Admin_uploads_visitor_avatar_sets_path,
//        Avatar_family_guard_confines_each_route_to_its_own_family — D-357 per-family scope guard)
using System.Security.Claims;
using FastEndpoints;
using SIMF.Api.Endpoints.Account;
using SIMF.Application.Files.Abstractions;
using SIMF.Application.IdentityAccess;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
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
public abstract class AdminAvatarUploadEndpointBase(
    IAccountService accountService, IAdminUserProvisioningService provisioning)
    : Endpoint<EmptyRequest, ApiResult<AvatarResponse>>
{
    public abstract Guid SubjectId { get; }

    /// <summary>The account family this route serves — its View/Edit permission
    /// must only reach subjects of this family. See
    /// <see cref="IAdminUserProvisioningService.IsSubjectInFamilyAsync"/>.</summary>
    public abstract UserType ExpectedType { get; }

    /// <summary>Audience (<c>true</c>) vs partner/Other (<c>false</c>) for the
    /// Visitor family; <c>null</c> for the Admin family.</summary>
    public abstract bool? ExpectedIsVisitor { get; }

    public override async Task HandleAsync(EmptyRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out _))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        // D-357 (review follow-up) — confine this Edit permission to its own family
        // so it can't overwrite another family's photo across the shared id space.
        // 404 (not 403) so a wrong-family id is indistinguishable from a missing one.
        if (!await provisioning.IsSubjectInFamilyAsync(SubjectId, ExpectedType, ExpectedIsVisitor, ct))
        {
            await Send.NotFoundAsync(ct);
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
public sealed class UploadVisitorAvatarEndpoint(
    IAccountService accountService, IAdminUserProvisioningService provisioning)
    : AdminAvatarUploadEndpointBase(accountService, provisioning)
{
    public override Guid SubjectId => Route<Guid>("id");
    public override UserType ExpectedType => UserType.Visitor;
    public override bool? ExpectedIsVisitor => true;

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
public sealed class UploadOtherAvatarEndpoint(
    IAccountService accountService, IAdminUserProvisioningService provisioning)
    : AdminAvatarUploadEndpointBase(accountService, provisioning)
{
    public override Guid SubjectId => Route<Guid>("id");
    public override UserType ExpectedType => UserType.Visitor;
    public override bool? ExpectedIsVisitor => false;

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
    SimfAppDbContext appDb, IFileStorageProvider storage,
    IAdminUserProvisioningService provisioning)
    : EndpointWithoutRequest
{
    public abstract Guid SubjectId { get; }

    /// <summary>The account family this route serves — its View permission must
    /// only reach subjects of this family. See
    /// <see cref="IAdminUserProvisioningService.IsSubjectInFamilyAsync"/>.</summary>
    public abstract UserType ExpectedType { get; }

    /// <summary>Audience (<c>true</c>) vs partner/Other (<c>false</c>) for the
    /// Visitor family; <c>null</c> for the Admin family.</summary>
    public abstract bool? ExpectedIsVisitor { get; }

    public override async Task HandleAsync(CancellationToken ct)
    {
        // D-357 (review follow-up) — confine this View permission to its own family
        // so it can't read another family's photo across the shared SimfUser id
        // space. 404 (not 403) keeps a wrong-family id indistinguishable from a
        // missing one (also the natural response for the no-avatar case below).
        if (!await provisioning.IsSubjectInFamilyAsync(SubjectId, ExpectedType, ExpectedIsVisitor, ct))
        {
            await Send.NotFoundAsync(ct);
            return;
        }

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
    SimfAppDbContext appDb, IFileStorageProvider storage,
    IAdminUserProvisioningService provisioning)
    : AdminAvatarFetchEndpointBase(appDb, storage, provisioning)
{
    public override Guid SubjectId => Route<Guid>("id");
    public override UserType ExpectedType => UserType.Visitor;
    public override bool? ExpectedIsVisitor => true;

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
    SimfAppDbContext appDb, IFileStorageProvider storage,
    IAdminUserProvisioningService provisioning)
    : AdminAvatarFetchEndpointBase(appDb, storage, provisioning)
{
    public override Guid SubjectId => Route<Guid>("id");
    public override UserType ExpectedType => UserType.Visitor;
    public override bool? ExpectedIsVisitor => false;

    public override void Configure()
    {
        Get("/admin/others/{id:guid}/avatar");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Others.View), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Summary(summary => summary.Summary =
            "Stream an Other account's profile photo (avatar).");
    }
}

/// <summary>D-357 — <c>GET /api/v1/admin/admins/{id}/avatar</c>. Backs the
/// Admins-list thumbnail; gated by Admins.View (the Admins page permission),
/// mirroring the visitors/others avatar reads. Reuses the same id-keyed
/// StoredFile read (avatars live in the one central file store for every user
/// type, admins included).</summary>
public sealed class FetchAdminAvatarEndpoint(
    SimfAppDbContext appDb, IFileStorageProvider storage,
    IAdminUserProvisioningService provisioning)
    : AdminAvatarFetchEndpointBase(appDb, storage, provisioning)
{
    public override Guid SubjectId => Route<Guid>("id");
    public override UserType ExpectedType => UserType.Admin;
    public override bool? ExpectedIsVisitor => null;

    public override void Configure()
    {
        Get("/admin/admins/{id:guid}/avatar");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Admins.View), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Summary(summary => summary.Summary =
            "Stream an admin account's profile photo (avatar).");
    }
}
