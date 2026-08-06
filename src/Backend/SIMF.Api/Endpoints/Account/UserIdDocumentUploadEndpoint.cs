// Tests: SIMF.Api.Tests/UserProfileTests.cs (round-trip, magic-byte gate)
using FastEndpoints;
using SIMF.Api.RequestContext;
using SIMF.Application.Auditing;
using SIMF.Application.IdentityAccess;
using SIMF.Common;
using SIMF.Domain.Auditing;

using SIMF.Common.Enums;

namespace SIMF.Api.Endpoints.Account;

/// <summary>The body of a user ID-document upload.</summary>
public sealed class UserIdDocumentUploadRequest
{
    public IFormFile? File { get; set; }
}

/// <summary>
/// <c>POST /api/v1/app/account/user-profile/id-image</c> — uploads the
/// user's ID-document image attachment (renamed from
/// <c>/account/visitor-profile/id-image</c>). PNG / JPEG /
/// WebP, up to 5 MB, content-type + magic-byte verified before the
/// bytes touch the storage layer. The storage layer then encrypts the
/// file with AES-GCM under the per-installation key (see
/// <c>EncryptedUserIdDocumentStorage</c>).
/// </summary>
public sealed class UserIdDocumentUploadEndpoint(
    IUserProfileService service,
    IAuditLog auditLog)
    : Endpoint<UserIdDocumentUploadRequest, ApiResult<bool>>
{
    private const long MaxBytes = 5L * 1024 * 1024;

    public override void Configure()
    {
        Post("/app/account/user-profile/id-image");
        Tags("Account");
        AllowFileUploads();
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary =
            "Upload the user's ID-document image attachment.");
    }

    public override async Task HandleAsync(
        UserIdDocumentUploadRequest req, CancellationToken ct)
    {
        var actorId = User.ActorId();

        if (req.File is null || req.File.Length == 0)
        {
            await AuditRejectAsync(actorId, ErrorCodes.VisitorIdImageMissing, ct);
            throw new ApiException(
                ErrorCodes.VisitorIdImageMissing, 400,
                "An ID image is required.",
                "صورة الهوية مطلوبة.");
        }

        if (req.File.Length > MaxBytes)
        {
            await AuditRejectAsync(actorId, ErrorCodes.VisitorIdImageTooLarge, ct);
            throw new ApiException(
                ErrorCodes.VisitorIdImageTooLarge, 400,
                "The ID image must be 5 MB or less.",
                "يجب ألا يتجاوز حجم صورة الهوية 5 ميغابايت.");
        }

        using var stream = new MemoryStream();
        await req.File.CopyToAsync(stream, ct);
        var bytes = stream.ToArray();

        var normalisedContentType = req.File.ContentType?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!ImageUploadValidation.IsAllowedImage(bytes, normalisedContentType))
        {
            await AuditRejectAsync(actorId, ErrorCodes.VisitorIdImageMimeUnsupported, ct);
            throw new ApiException(
                ErrorCodes.VisitorIdImageMimeUnsupported, 400,
                "The ID image must be PNG, JPEG or WebP.",
                "يجب أن تكون صورة الهوية بصيغة PNG أو JPEG أو WebP.");
        }

        // Two-photo split — the self-service ID upload is now
        // a DOCUMENT picked from the gallery (national-ID / Iqama / passport
        // scan), so the human-face gate that belonged to the old "ID = live
        // selfie" model is removed here. The live face requirement now lives on
        // the separate FACE photo (the avatar), captured through the client
        // liveness flow. Content-type + magic-byte + size are still enforced
        // above. The admin walk-in id-document path keeps its own face gate.
        await service.UploadIdImageAsync(actorId, bytes, normalisedContentType, ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }

    private Task AuditRejectAsync(Guid actorId, string errorCode, CancellationToken ct) =>
        auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.UserProfileIdImageRejected,
            Outcome = AuditOutcome.Failure,
            SubjectUserId = actorId,
            ActorUserId = actorId,
            ErrorCode = errorCode,
        }, ct);
}
