namespace SIMF.Contracts.Authentication;

/// <summary>The body of <c>POST /api/v1/auth/verify-email</c> (SIMF-API-001 section 12.4).</summary>
public sealed class VerifyEmailRequest
{
    public string Email { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;
}

/// <summary>The data returned by a successful email verification.</summary>
public sealed record VerifyEmailResponse(string Email, bool EmailVerified);
