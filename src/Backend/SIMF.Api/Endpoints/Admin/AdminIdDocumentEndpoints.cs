// Tests: SIMF.Api.Tests/WalkInRegistrationTests.cs (round-trip smoke),
//        SIMF.Api.Tests/UserProfileFaceGateTests.cs (admin walk-in face gate)
using System.Security.Claims;
using FastEndpoints;
using SIMF.Application.IdentityAccess;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// D-129 — admin-side ID-document upload + read for the walk-in flow.
/// Two paths per kind (visitor / other). Upload accepts multipart with a
/// single file field "file"; read streams the decrypted bytes back. Same
/// MIME + magic-byte gate as the self-service variant; storage layer
/// AES-GCM-encrypts at rest.
/// </summary>
public abstract class AdminIdDocumentUploadEndpointBase(
    IUserProfileService service,
    IFaceDetectionService faceDetection,
    IAdminUserProvisioningService provisioning)
    : Endpoint<EmptyRequest, ApiResult<bool>>
{
    /// <summary>5 MB cap — same as the self-service upload.</summary>
    protected const long MaxBytes = 5L * 1024 * 1024;

    public abstract Guid SubjectId { get; }
    public abstract UserType ExpectedKind { get; }

    /// <summary>D-836 — audience (<c>true</c>) vs partner/Other (<c>false</c>)
    /// for the Visitor family; <c>null</c> for the Admin family. Mirrors the
    /// avatar endpoints.</summary>
    public abstract bool? ExpectedIsVisitor { get; }

    public override async Task HandleAsync(EmptyRequest _, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        // D-836 — confine this Edit permission to its own family. The service's
        // guard only compares UserType, and D-186 made BOTH the visitors and the
        // others route pass UserType.Visitor, so without this an admin holding
        // only Visitors.Edit could overwrite a PARTNER's national-ID image
        // through the visitors route (and vice versa). 404, not 403: a subject
        // outside your family should not be confirmed to exist.
        if (!await provisioning.IsSubjectInFamilyAsync(
                SubjectId, ExpectedKind, ExpectedIsVisitor, ct))
        {
            await Send.NotFoundAsync(ct);
            return;
        }

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

        // C7 (D-371) — server-side human-face gate, parity with the
        // self-service upload. The walk-in operator's device runs no on-device
        // pre-check, so the server is the only face authority on this path.
        // Offline FaceAiSharp ONNX; fails closed on an undecodable image.
        if (!await faceDetection.ContainsHumanFaceAsync(bytes, ct))
        {
            throw new ApiException(
                ErrorCodes.VisitorIdImageNoFace, 400,
                "No human face was detected in the photo — retake a clear photo of the face.",
                "لم يتم التعرف على وجه بشري في الصورة — أعد التقاط صورة واضحة للوجه.");
        }

        await service.UploadIdImageForSubjectAsync(
            actorId, SubjectId, ExpectedKind, bytes, contentType, ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}

/// <summary><c>POST /api/v1/admin/visitors/{id}/id-document</c>.</summary>
public sealed class UploadVisitorIdDocumentEndpoint(
    IUserProfileService service, IFaceDetectionService faceDetection,
    IAdminUserProvisioningService provisioning)
    : AdminIdDocumentUploadEndpointBase(service, faceDetection, provisioning)
{
    public override Guid SubjectId => Route<Guid>("id");
    public override UserType ExpectedKind => UserType.Visitor;
    public override bool? ExpectedIsVisitor => true;

    public override void Configure()
    {
        Post("/admin/visitors/{id:guid}/id-document");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Visitors.Edit), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        AllowFileUploads();
        Summary(summary => summary.Summary =
            "Admin upload of a visitor's ID-document image.");
    }
}

/// <summary><c>POST /api/v1/admin/others/{id}/id-document</c>.</summary>
public sealed class UploadOtherIdDocumentEndpoint(
    IUserProfileService service, IFaceDetectionService faceDetection,
    IAdminUserProvisioningService provisioning)
    : AdminIdDocumentUploadEndpointBase(service, faceDetection, provisioning)
{
    public override Guid SubjectId => Route<Guid>("id");
    // D-186: Other accounts are Visitor-typed under the hood; the
    // partner-vs-audience distinction lives on the linked ProfileType.
    // D-836 — that is exactly why UserType alone is NOT enough here. This
    // comment used to say the UserType guard was "what matters", which was
    // true only for the upload-to-an-Admin-row concern it was written for;
    // ExpectedIsVisitor is what keeps the two Visitor-family tiers apart.
    public override UserType ExpectedKind => UserType.Visitor;
    public override bool? ExpectedIsVisitor => false;

    public override void Configure()
    {
        Post("/admin/others/{id:guid}/id-document");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Others.Edit), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        AllowFileUploads();
        Summary(summary => summary.Summary =
            "Admin upload of an Other account's ID-document image.");
    }
}

/// <summary><c>GET /api/v1/admin/visitors/{id}/id-document</c> — streams the
/// decrypted image bytes back so the CP can render it inline.</summary>
public abstract class AdminIdDocumentFetchEndpointBase(
    IUserProfileService service, IAdminUserProvisioningService provisioning)
    : EndpointWithoutRequest
{
    public abstract Guid SubjectId { get; }
    public abstract UserType ExpectedKind { get; }

    /// <summary>D-836 — audience (<c>true</c>) vs partner/Other (<c>false</c>)
    /// for the Visitor family; <c>null</c> for the Admin family.</summary>
    public abstract bool? ExpectedIsVisitor { get; }

    public override async Task HandleAsync(CancellationToken ct)
    {
        // A9 (PII) — capture the acting admin so the read is audited. The route is
        // permission-gated, so a token without a `sub` cannot legitimately reach
        // here; treat its absence as unauthorized rather than write a null actor.
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        // D-836 — confine this View permission to its own family, mirroring the
        // avatar endpoints. The national ID / Iqama / passport image is the most
        // sensitive PII in the system, and UserType alone does not separate the
        // audience tier from the partner tier (D-186 made both Visitor-typed), so
        // without this a Visitors.View holder could read a PARTNER's ID image.
        // Checked BEFORE the read so no bytes are decrypted and no PII-disclosure
        // audit row is written for a subject outside the caller's family.
        if (!await provisioning.IsSubjectInFamilyAsync(
                SubjectId, ExpectedKind, ExpectedIsVisitor, ct))
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var image = await service.ReadIdImageForSubjectAsync(actorId, SubjectId, ExpectedKind, ct);
        if (image is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        // A2-14 (NCA App-Sec Standard) — the ID document (national ID / Iqama /
        // passport) is high-value PII; never cache it in a proxy or the browser.
        HttpContext.Response.Headers.CacheControl = "no-store";
        await Send.BytesAsync(image.Content, contentType: image.ContentType, cancellation: ct);
    }
}

/// <summary><c>GET /api/v1/admin/visitors/{id}/id-document</c>.</summary>
public sealed class FetchVisitorIdDocumentEndpoint(
    IUserProfileService service, IAdminUserProvisioningService provisioning)
    : AdminIdDocumentFetchEndpointBase(service, provisioning)
{
    public override Guid SubjectId => Route<Guid>("id");
    public override UserType ExpectedKind => UserType.Visitor;
    public override bool? ExpectedIsVisitor => true;

    public override void Configure()
    {
        Get("/admin/visitors/{id:guid}/id-document");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Visitors.View), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Summary(summary => summary.Summary =
            "Stream the visitor's ID-document image.");
    }
}

/// <summary><c>GET /api/v1/admin/others/{id}/id-document</c>.</summary>
public sealed class FetchOtherIdDocumentEndpoint(
    IUserProfileService service, IAdminUserProvisioningService provisioning)
    : AdminIdDocumentFetchEndpointBase(service, provisioning)
{
    public override Guid SubjectId => Route<Guid>("id");
    // D-186: Other accounts are Visitor-typed under the hood; the
    // partner-vs-audience distinction lives on the linked ProfileType,
    // which is exactly why ExpectedIsVisitor is required here (D-836).
    public override UserType ExpectedKind => UserType.Visitor;
    public override bool? ExpectedIsVisitor => false;

    public override void Configure()
    {
        Get("/admin/others/{id:guid}/id-document");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Others.View), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Summary(summary => summary.Summary =
            "Stream the Other account's ID-document image.");
    }
}
