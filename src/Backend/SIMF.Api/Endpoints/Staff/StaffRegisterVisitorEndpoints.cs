// Tests: SIMF.Api.Tests/StaffWalkInRegistrationTests.cs
using FastEndpoints;
using SIMF.Api.Endpoints.Admin; // AuthorizationPolicies
using SIMF.Api.RequestContext;
using SIMF.Application.Abstractions;
using SIMF.Application.IdentityAccess;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Common.Options;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Staff;

/// <summary>
/// <c>POST /app/staff/visitors/register-onsite</c>. The staff-app twin
/// of the Control-Panel walk-in desk (<c>RegisterVisitorOnSiteEndpoint</c>):
/// exhibition staff register a walk-in visitor straight from the mobile/tablet
/// app (Figma 1467:12357). It reuses the shared on-site provisioning service —
/// creating a <see cref="AccountState.PendingApproval"/> visitor with no QR
/// until an admin approves it (D-425) — and is gated by the SAME
/// <c>Visitors.RegisterOnsite</c> permission so a person authorised to register
/// walk-ins has the capability on both surfaces. App tokens carry the JWT
/// permission claims (same model the gate-operator console uses, D-207/D-208),
/// so the gate is the real authority; the app role-gate is only a UX guard.
/// </summary>
public sealed class StaffRegisterVisitorEndpoint(IAdminUserProvisioningService service)
    : Endpoint<AdminWalkInRegistrationRequest, ApiResult<AdminWalkInRegistrationResponse>>
{
    public override void Configure()
    {
        Post("/app/staff/visitors/register-onsite");
        Policies(
            PermissionCatalog.PolicyFor(PermissionCatalog.Visitors.RegisterOnsite),
            nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Staff");
        Options(rb => rb.RequireRateLimiting(RateLimitOptions.OperationalPolicy));
        Summary(s => s.Summary =
            "Staff-app on-site walk-in visitor registration. Creates a PENDING account; the QR is minted on approval.");
    }

    public override async Task HandleAsync(
        AdminWalkInRegistrationRequest req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        // Audience-side: the desk creates a Visitor-typed account; the guard
        // rejects a partner ProfileType (parity with the CP visitors desk).
        var response = await service.RegisterOnSiteAsync(
            actorId, UserType.Visitor, req, ct, expectedIsVisitor: true);
        await Send.OkAsync(
            ApiResult<AdminWalkInRegistrationResponse>.Ok(response), ct);
    }
}

/// <summary>
/// <c>POST /app/staff/visitors/{id}/id-document</c>. Staff-app twin of
/// <c>UploadVisitorIdDocumentEndpoint</c>: attaches the freshly registered
/// visitor's ID-document image (national ID / Iqama / passport). Multipart, one
/// "file" field; same 5 MB + MIME + magic-byte + human-face gate as the CP /
/// self-service paths; the storage layer AES-GCM-encrypts at rest. Gated by the
/// walk-in permission (the same person who registered the visitor attaches it).
/// </summary>
public sealed class StaffUploadVisitorIdDocumentEndpoint(
    IUserProfileService service, IFaceDetectionService faceDetection)
    : Endpoint<EmptyRequest, ApiResult<bool>>
{
    private const long MaxBytes = 5L * 1024 * 1024;

    public override void Configure()
    {
        Post("/app/staff/visitors/{id:guid}/id-document");
        Policies(
            PermissionCatalog.PolicyFor(PermissionCatalog.Visitors.RegisterOnsite),
            nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Staff");
        Options(rb => rb.RequireRateLimiting(RateLimitOptions.OperationalPolicy));
        AllowFileUploads();
        Summary(s => s.Summary =
            "Staff-app upload of a walk-in visitor's ID-document image.");
    }

    public override async Task HandleAsync(EmptyRequest _, CancellationToken ct)
    {
        var actorId = User.ActorId();

        var file = Files.GetFile("file");
        if (file is null || file.Length == 0)
        {
            throw new ApiException(
                ErrorCodes.VisitorIdImageMissing, 400,
                "An ID image is required.",
                "صورة الهوية مطلوبة.");
        }
        if (file.Length > MaxBytes)
        {
            throw new ApiException(
                ErrorCodes.VisitorIdImageTooLarge, 413,
                "The ID image must be 5 MB or less.",
                "يجب ألا يتجاوز حجم صورة الهوية 5 ميغابايت.");
        }

        using var stream = new MemoryStream();
        await file.CopyToAsync(stream, ct);
        var bytes = stream.ToArray();
        var contentType = (file.ContentType ?? string.Empty).Trim().ToLowerInvariant();

        if (!ImageUploadValidation.IsAllowedImage(bytes, contentType))
        {
            throw new ApiException(
                ErrorCodes.VisitorIdImageMimeUnsupported, 400,
                "The ID image must be PNG, JPEG or WebP.",
                "يجب أن تكون صورة الهوية بصيغة PNG أو JPEG أو WebP.");
        }

        // C7 (D-371) parity — the offline server-side human-face gate; the
        // staff device runs no on-device pre-check on this path.
        if (!await faceDetection.ContainsHumanFaceAsync(bytes, ct))
        {
            throw new ApiException(
                ErrorCodes.VisitorIdImageNoFace, 400,
                "No human face was detected in the photo — retake a clear photo of the face.",
                "لم يتم التعرف على وجه بشري في الصورة — أعد التقاط صورة واضحة للوجه.");
        }

        var subjectId = Route<Guid>("id");
        await service.UploadIdImageForSubjectAsync(
            actorId, subjectId, UserType.Visitor, bytes, contentType, ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}

/// <summary>
/// <c>POST /app/staff/visitors/{id}/avatar</c>. Staff-app twin of
/// <c>UploadVisitorAvatarEndpoint</c>: attaches the walk-in visitor's profile
/// photo. Reuses <see cref="IAccountService.SetAvatarAsync"/> (id-parameterised,
/// 2 MB + MIME + magic-byte gate, no face requirement — it is a profile photo).
/// </summary>
public sealed class StaffUploadVisitorAvatarEndpoint(IAccountService accountService)
    : Endpoint<EmptyRequest, ApiResult<AvatarResponse>>
{
    public override void Configure()
    {
        Post("/app/staff/visitors/{id:guid}/avatar");
        Policies(
            PermissionCatalog.PolicyFor(PermissionCatalog.Visitors.RegisterOnsite),
            nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Staff");
        Options(rb => rb.RequireRateLimiting(RateLimitOptions.OperationalPolicy));
        AllowFileUploads();
        Summary(s => s.Summary =
            "Staff-app upload of a walk-in visitor's profile photo (avatar).");
    }

    public override async Task HandleAsync(EmptyRequest req, CancellationToken ct)
    {
        User.RequireActor();

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
        var subjectId = Route<Guid>("id");
        var response = await accountService.SetAvatarAsync(
            subjectId, stream.ToArray(),
            file.ContentType ?? string.Empty, ct);
        await Send.OkAsync(ApiResult<AvatarResponse>.Ok(response), ct);
    }
}
