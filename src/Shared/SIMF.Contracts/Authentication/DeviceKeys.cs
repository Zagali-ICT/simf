namespace SIMF.Contracts.Authentication;

/// <summary>D-172 (gap doc G10, PDF §2.5) — register a new biometric
/// device key. Caller must already be signed in; the new key binds to
/// the caller's user id. The public key is a base64-encoded
/// SubjectPublicKeyInfo DER blob (importable via
/// <c>ECDsa.ImportSubjectPublicKeyInfo</c>).</summary>
public sealed class RegisterDeviceKeyRequest
{
    public string PublicKey { get; set; } = string.Empty;
    public string Algorithm { get; set; } = "ES256";
    public string Label { get; set; } = string.Empty;

    /// <summary>#7a — the emailed-OTP step-up code confirming the user intends
    /// to enrol biometric sign-in. Required by the server when
    /// <c>DeviceKey:RequireStepUpForEnrol</c> is on (the default); obtained from
    /// <c>POST /app/auth/device-keys/step-up</c>. Nullable + appended so the
    /// register contract stays wire-compatible.</summary>
    public string? StepUpCode { get; set; }
}

/// <summary>#7a — the result of requesting a biometric-enrol step-up code: the
/// masked address the code was emailed to (for the "we sent a code to t***@x"
/// line) and how long it stays valid. Never carries the plaintext code.</summary>
public sealed record SendBiometricStepUpResponse(
    string MaskedEmail,
    int ExpiresInSeconds);

/// <summary>D-172 — one of the caller's device keys (or one row in the
/// admin list). The public-key field is **not** included to avoid
/// leaking the key over the wire on every list request — the bytes
/// the server uses on verify already live server-side.</summary>
public sealed record DeviceKeyEntry(
    Guid Id,
    Guid UserId,
    string Algorithm,
    string Label,
    DateTime CreatedAt,
    DateTime? LastUsedAt,
    DateTime? RevokedAt);

/// <summary>D-172 — the server-issued challenge for a sign-in attempt.
/// Client signs the challenge bytes (after base64 decode) with its
/// private key and submits the signature in
/// <see cref="SignInWithDeviceKeyRequest"/>.</summary>
public sealed record DeviceKeyChallenge(
    string Challenge,
    int ExpiresInSeconds);

/// <summary>D-172 — sign-in with device key. The client decodes the
/// challenge, signs it with the device's private key, and posts the
/// signature here. The signature is the raw IEEE-P1363
/// (r || s, 64 bytes for P-256) format base64-encoded — matches the
/// JWS ES256 standard and is what every mobile crypto library returns.</summary>
public sealed class SignInWithDeviceKeyRequest
{
    public Guid DeviceKeyId { get; set; }
    public string Challenge { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
}
