// Tests: SIMF.Api.Tests/DeviceKeySignInTests.cs
using FastEndpoints;
using SIMF.Api.Endpoints.Admin;
using SIMF.Api.RequestContext;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Auth;

/// <summary>D-172 (gap doc G10, PDF §2.5) — register a new device key
/// after a successful sign-in. Caller is the bound user; the new key
/// is created with no challenge.</summary>
public sealed class RegisterDeviceKeyEndpoint(IDeviceKeyService service)
    : Endpoint<RegisterDeviceKeyRequest, ApiResult<DeviceKeyEntry>>
{
    public override void Configure()
    {
        Post("/app/auth/device-keys");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Auth");
    }

    public override async Task HandleAsync(RegisterDeviceKeyRequest req, CancellationToken ct)
    {
        var callerId = User.ActorId();
        var entry = await service.RegisterAsync(callerId, req, ct);
        await Send.OkAsync(ApiResult<DeviceKeyEntry>.Ok(entry), ct);
    }
}

/// <summary>#7a — issue + email a step-up code to the signed-in caller's own
/// address. The user supplies it on the following
/// <see cref="RegisterDeviceKeyEndpoint"/> call, so a borrowed-but-unlocked
/// phone can't silently enrol a biometric credential without also holding the
/// account's email. The re-issue cap lives in the service (5 per hour); "auth"
/// adds the per-IP limiter the other device-key endpoints use.</summary>
public sealed class SendBiometricStepUpEndpoint(IDeviceKeyService service)
    : EndpointWithoutRequest<ApiResult<SendBiometricStepUpResponse>>
{
    public override void Configure()
    {
        Post("/app/auth/device-keys/step-up");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Auth");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var callerId = User.ActorId();
        var result = await service.IssueEnrolStepUpAsync(callerId, ct);
        await Send.OkAsync(ApiResult<SendBiometricStepUpResponse>.Ok(result), ct);
    }
}

public sealed class ListMyDeviceKeysEndpoint(IDeviceKeyService service)
    : EndpointWithoutRequest<ApiResult<IReadOnlyList<DeviceKeyEntry>>>
{
    public override void Configure()
    {
        Get("/app/auth/device-keys");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Auth");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var callerId = User.ActorId();
        var rows = await service.ListMineAsync(callerId, ct);
        await Send.OkAsync(ApiResult<IReadOnlyList<DeviceKeyEntry>>.Ok(rows), ct);
    }
}

public sealed class IssueDeviceKeyChallengeRoute
{
    public Guid Id { get; set; }
}

/// <summary>Anonymous: anyone with the device-key id can ask
/// for a challenge. The challenge is useless without the matching
/// private key, so leaking the deviceKeyId does not enable sign-in.</summary>
public sealed class IssueDeviceKeyChallengeEndpoint(IDeviceKeyService service)
    : Endpoint<IssueDeviceKeyChallengeRoute, ApiResult<DeviceKeyChallenge>>
{
    public override void Configure()
    {
        Post("/app/auth/device-keys/{id:guid}/challenge");
        AllowAnonymous();
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Auth");
    }

    public override async Task HandleAsync(IssueDeviceKeyChallengeRoute req, CancellationToken ct)
    {
        var challenge = await service.IssueChallengeAsync(req.Id, ct);
        await Send.OkAsync(ApiResult<DeviceKeyChallenge>.Ok(challenge), ct);
    }
}

/// <summary>Anonymous: verify signed challenge + mint JWT pair.
/// On failure returns 401 with the typed code; success returns full
/// <see cref="AuthTokens"/>.</summary>
public sealed class SignInWithDeviceKeyEndpoint(IDeviceKeyService service)
    : Endpoint<SignInWithDeviceKeyRequest, ApiResult<AuthTokens>>
{
    public override void Configure()
    {
        Post("/app/auth/sign-in-with-device-key");
        AllowAnonymous();
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Auth");
    }

    public override async Task HandleAsync(SignInWithDeviceKeyRequest req, CancellationToken ct)
    {
        var tokens = await service.SignInWithDeviceKeyAsync(req, ct);
        if (tokens is null)
        {
            throw new ApiException(
                ErrorCodes.DeviceKeySignatureInvalid, 401,
                "Device-key sign-in failed.",
                "فشل تسجيل الدخول بمفتاح الجهاز.");
        }
        await Send.OkAsync(ApiResult<AuthTokens>.Ok(tokens), ct);
    }
}

public sealed class RevokeMyDeviceKeyRoute
{
    public Guid Id { get; set; }
}

public sealed class RevokeMyDeviceKeyEndpoint(IDeviceKeyService service)
    : Endpoint<RevokeMyDeviceKeyRoute, ApiResult<bool>>
{
    public override void Configure()
    {
        Delete("/app/auth/device-keys/{id:guid}");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Auth");
    }

    public override async Task HandleAsync(RevokeMyDeviceKeyRoute req, CancellationToken ct)
    {
        var callerId = User.ActorId();
        await service.RevokeAsync(callerId, req.Id, actorIsAdministrator: false, ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}

public sealed class AdminRevokeDeviceKeyRoute
{
    public Guid Id { get; set; }
}

public sealed class AdminRevokeDeviceKeyEndpoint(IDeviceKeyService service)
    : Endpoint<AdminRevokeDeviceKeyRoute, ApiResult<bool>>
{
    public override void Configure()
    {
        Delete("/admin/device-keys/{id:guid}");
        Policies(nameof(AuthorizationPolicies.AdministratorOnly),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(AdminRevokeDeviceKeyRoute req, CancellationToken ct)
    {
        var actorId = User.ActorId();
        await service.RevokeAsync(actorId, req.Id, actorIsAdministrator: true, ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}
