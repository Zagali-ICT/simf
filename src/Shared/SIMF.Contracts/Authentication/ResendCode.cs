namespace SIMF.Contracts.Authentication;

/// <summary>The body of <c>POST /api/v1/app/auth/resend-code</c> (SIMF-API-001 section 12.4).</summary>
public sealed class ResendCodeRequest
{
    public string Email { get; set; } = string.Empty;
}

/// <summary>The data returned by a successful code resend.</summary>
public sealed record ResendCodeResponse(string Email, int CodeExpiresInSeconds);
