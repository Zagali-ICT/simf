// Tests: SIMF.Api.Tests/ProfileEndpointsTests.cs
using FastEndpoints;
using Microsoft.AspNetCore.RateLimiting;
using SIMF.Api.RequestContext;
using SIMF.Application.IdentityAccess;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Account;

/// <summary>The body of an avatar upload — one image file.</summary>
public sealed class AvatarUploadRequest
{
    public IFormFile? File { get; set; }
}

/// <summary>
/// <c>POST /api/v1/app/account/avatar</c> — uploads a new avatar image for the
/// signed-in user. Accepts PNG, JPEG or WebP up to 2 MB (myComment #11).
/// </summary>
public sealed class AvatarUploadEndpoint(IAccountService accountService)
    : Endpoint<AvatarUploadRequest, ApiResult<AvatarResponse>>
{
    public override void Configure()
    {
        Post("/app/account/avatar");
        Tags("Account");
        AllowFileUploads();
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary = "Upload a new avatar image.");
    }

    public override async Task HandleAsync(AvatarUploadRequest req, CancellationToken ct)
    {
        var userId = User.ActorId();

        if (req.File is null || req.File.Length == 0)
        {
            throw new ApiException(
                ErrorCodes.AvatarFileMissing, 400,
                "An avatar file is required.",
                "ملف الصورة مطلوب.");
        }

        using var stream = new MemoryStream();
        await req.File.CopyToAsync(stream, ct);
        var response = await accountService.SetAvatarAsync(
            userId, stream.ToArray(), req.File.ContentType, ct);
        await Send.OkAsync(ApiResult<AvatarResponse>.Ok(response), ct);
    }
}
