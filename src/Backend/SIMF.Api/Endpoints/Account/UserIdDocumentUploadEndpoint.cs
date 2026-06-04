// Tests: SIMF.Api.Tests/UserProfileTests.cs (round-trip, magic-byte gate)
using System.Security.Claims;
using FastEndpoints;
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
/// <c>POST /api/v1/account/user-profile/id-image</c> — uploads the
/// user's ID-document image attachment (decisions D-046 b, P8 —
/// D-048; renamed from <c>/account/visitor-profile/id-image</c>). PNG / JPEG /
/// WebP, up to 5 MB, content-type + magic-byte verified before the
/// bytes touch the storage layer. The storage layer then encrypts the
/// file with AES-GCM under the per-installation key (see
/// <see cref="SIMF.Infrastructure.Identity.EncryptedUserIdDocumentStorage"/>).
/// </summary>
public sealed class UserIdDocumentUploadEndpoint(
    IUserProfileService service,
    IAuditLog auditLog)
    : Endpoint<UserIdDocumentUploadRequest, ApiResult<bool>>
{
    private const long MaxBytes = 5L * 1024 * 1024;
    private static readonly string[] AllowedMimeTypes =
        { "image/jpeg", "image/png", "image/webp" };

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
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

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
        if (!AllowedMimeTypes.Contains(normalisedContentType)
            || !MagicBytesMatch(bytes, normalisedContentType))
        {
            await AuditRejectAsync(actorId, ErrorCodes.VisitorIdImageMimeUnsupported, ct);
            throw new ApiException(
                ErrorCodes.VisitorIdImageMimeUnsupported, 400,
                "The ID image must be PNG, JPEG or WebP.",
                "يجب أن تكون صورة الهوية بصيغة PNG أو JPEG أو WebP.");
        }

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

    /// <summary>Same magic-byte signatures as the avatar gate (AccountService).
    /// PNG <c>89 50 4E 47 0D 0A 1A 0A</c>; JPEG <c>FF D8 FF</c>;
    /// WebP <c>52 49 46 46 ?? ?? ?? ?? 57 45 42 50</c>.</summary>
    private static bool MagicBytesMatch(byte[] content, string contentType) =>
        contentType switch
        {
            "image/png" => content.Length >= 8
                && content[0] == 0x89 && content[1] == 0x50
                && content[2] == 0x4E && content[3] == 0x47
                && content[4] == 0x0D && content[5] == 0x0A
                && content[6] == 0x1A && content[7] == 0x0A,
            "image/jpeg" => content.Length >= 3
                && content[0] == 0xFF && content[1] == 0xD8 && content[2] == 0xFF,
            "image/webp" => content.Length >= 12
                && content[0] == 0x52 && content[1] == 0x49
                && content[2] == 0x46 && content[3] == 0x46
                && content[8] == 0x57 && content[9] == 0x45
                && content[10] == 0x42 && content[11] == 0x50,
            _ => false,
        };
}
