// Tests: SIMF.Api.Tests/AccountDeletionCodeTests.cs
using FastEndpoints;
using Microsoft.AspNetCore.RateLimiting;
using SIMF.Api.RequestContext;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Account;

namespace SIMF.Api.Endpoints.Account;

/// <summary>
/// <c>POST /api/v1/app/account/delete/send-code</c> — emails the signed-in
/// caller a one-time code confirming they mean to erase their own account.
/// </summary>
/// <remarks>
/// <para>Deletion is irreversible, so it earns the same second factor that
/// enrolling a biometric credential does: an unlocked phone alone must not be
/// able to destroy an account.</para>
/// <para><b>Deliberately carries no <c>Policies(...)</c> call, and the service
/// behind it checks no <c>AccountState</c>.</b> The biometric step-up this
/// mirrors demands an approved account and refuses a disabled one; copying
/// either here would lock out precisely the pending, rejected and disabled
/// holders that <see cref="AccountDeleteEndpoint"/> exists for. Authentication
/// alone is the gate, and the caller can only ever ask for their own code: the
/// subject is the <c>sub</c> claim, never a parameter.</para>
/// </remarks>
public sealed class SendAccountDeletionCodeEndpoint(IAccountDeletionService deletion)
    : EndpointWithoutRequest<ApiResult<SendAccountDeletionCodeResponse>>
{
    public override void Configure()
    {
        Post("/app/account/delete/send-code");
        Tags("Account");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary =>
        {
            summary.Summary = "Email a confirmation code for account deletion.";
            summary.Description =
                "Sends a six-digit code to the account's own address and returns "
                + "it masked, with the lifetime in seconds. The newest code "
                + "invalidates any previous one.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await deletion.SendDeletionCodeAsync(User.ActorId(), ct);
        await Send.OkAsync(ApiResult<SendAccountDeletionCodeResponse>.Ok(result), ct);
    }
}
