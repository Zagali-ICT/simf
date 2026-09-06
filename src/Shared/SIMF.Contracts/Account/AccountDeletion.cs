namespace SIMF.Contracts.Account;

/// <summary>Erases the signed-in caller's own account
/// (<c>DELETE /app/account</c>).</summary>
/// <remarks>
/// The endpoint took no body before the emailed confirmation code existed, and
/// <see cref="Code"/> is nullable so it stays compatible with an installed build
/// that sends none. The server decides whether a missing code is acceptable, via
/// <c>AccountDeletion:RequireCodeForDeletion</c> — the client is not trusted to
/// know.
/// </remarks>
public sealed class DeleteAccountRequest
{
    /// <summary>The code emailed by <c>POST /app/account/delete/send-code</c>.</summary>
    public string? Code { get; set; }
}

/// <summary>Where the deletion confirmation code was sent, and for how long it
/// is good (<c>POST /app/account/delete/send-code</c>).</summary>
/// <param name="MaskedEmail">The address, masked for display — never the full
/// address, because the screen showing it is reachable from a shared device.</param>
/// <param name="ExpiresInSeconds">Lifetime, so the client can run a countdown
/// rather than hard-coding one that drifts from the server's.</param>
public sealed record SendAccountDeletionCodeResponse(
    string MaskedEmail, int ExpiresInSeconds);
