using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.ApiClient;

/// <summary>
/// A typed client over the SIMF Login API (SIMF-API-001 section 12). Every
/// call returns the API's <see cref="ApiResult{T}"/> envelope; a transport
/// failure is mapped to a failed envelope as well, so a caller branches on
/// success or failure one way only and never has to catch an exception.
/// </summary>
public sealed class SimfAuthClient(HttpClient http)
{
    private const string BasePath = "api/v1/app/auth/";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>The password step. On success a second factor is still required.</summary>
    public Task<ApiResult<SignInResponse>> SignInAsync(
        SignInRequest request, CancellationToken cancellationToken = default) =>
        PostAsync<SignInRequest, SignInResponse>("sign-in", request, null, cancellationToken);

    /// <summary>The Control Panel second factor — an authenticator TOTP code.</summary>
    public Task<ApiResult<AuthTokens>> VerifyTotpAsync(
        VerifyTotpRequest request, CancellationToken cancellationToken = default) =>
        PostAsync<VerifyTotpRequest, AuthTokens>("verify-totp", request, null, cancellationToken);

    /// <summary>The visitor second factor — a code emailed to the account.</summary>
    public Task<ApiResult<AuthTokens>> VerifyOtpAsync(
        VerifyOtpRequest request, CancellationToken cancellationToken = default) =>
        PostAsync<VerifyOtpRequest, AuthTokens>("verify-otp", request, null, cancellationToken);

    /// <summary>The recovery-code fallback for a lost authenticator (D-040).</summary>
    public Task<ApiResult<AuthTokens>> VerifyRecoveryCodeAsync(
        VerifyRecoveryCodeRequest request, CancellationToken cancellationToken = default) =>
        PostAsync<VerifyRecoveryCodeRequest, AuthTokens>(
            "verify-recovery-code", request, null, cancellationToken);

    /// <summary>Exchanges a refresh token for a fresh token pair.</summary>
    public Task<ApiResult<AuthTokens>> RefreshAsync(
        RefreshRequest request, CancellationToken cancellationToken = default) =>
        PostAsync<RefreshRequest, AuthTokens>("refresh", request, null, cancellationToken);

    /// <summary>Requests a password-reset code be emailed to the account.</summary>
    public Task<ApiResult<ForgotPasswordResponse>> ForgotPasswordAsync(
        ForgotPasswordRequest request, CancellationToken cancellationToken = default) =>
        PostAsync<ForgotPasswordRequest, ForgotPasswordResponse>(
            "forgot-password", request, null, cancellationToken);

    /// <summary>Sets a new password using a reset code.</summary>
    public Task<ApiResult<ResetPasswordResponse>> ResetPasswordAsync(
        ResetPasswordRequest request, CancellationToken cancellationToken = default) =>
        PostAsync<ResetPasswordRequest, ResetPasswordResponse>(
            "reset-password", request, null, cancellationToken);

    /// <summary>Changes the password of the authenticated caller.</summary>
    public Task<ApiResult<ChangePasswordResponse>> ChangePasswordAsync(
        ChangePasswordRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        PostAsync<ChangePasswordRequest, ChangePasswordResponse>(
            "change-password", request, accessToken, cancellationToken);

    /// <summary>
    /// Sets a new password against the single-use ticket the sign-in
    /// step issued for a Control Panel account with a forced-change credential.
    /// Anonymous — the ticket authorises it (no access token).
    /// </summary>
    public Task<ApiResult<CompletePasswordChangeResponse>> CompletePasswordChangeAsync(
        CompletePasswordChangeRequest request, CancellationToken cancellationToken = default) =>
        PostAsync<CompletePasswordChangeRequest, CompletePasswordChangeResponse>(
            "complete-password-change", request, null, cancellationToken);

    /// <summary>
    /// #2 (Q1, 2026-07-30) — begins MANDATORY authenticator enrolment for a
    /// Control Panel account whose password step returned an enrolment ticket
    /// instead of a session. Anonymous — the ticket authorises it (no access
    /// token exists yet).
    /// </summary>
    public Task<ApiResult<TotpSetupResponse>> StartTwoFactorEnrolmentAsync(
        StartTwoFactorEnrolmentRequest request, CancellationToken cancellationToken = default) =>
        PostAsync<StartTwoFactorEnrolmentRequest, TotpSetupResponse>(
            "totp/enrolment/start", request, null, cancellationToken);

    /// <summary>
    /// #2 — confirms the first authenticator code and completes the held-back
    /// sign-in. Returns the session plus the one-time recovery codes.
    /// </summary>
    public Task<ApiResult<CompleteTwoFactorEnrolmentResponse>> CompleteTwoFactorEnrolmentAsync(
        CompleteTwoFactorEnrolmentRequest request, CancellationToken cancellationToken = default) =>
        PostAsync<CompleteTwoFactorEnrolmentRequest, CompleteTwoFactorEnrolmentResponse>(
            "totp/enrolment/complete", request, null, cancellationToken);

    /// <summary>Ends every session for the authenticated caller.</summary>
    public Task<ApiResult<SignOutResponse>> SignOutAsync(
        string accessToken, CancellationToken cancellationToken = default) =>
        PostAsync<object, SignOutResponse>("sign-out", new object(), accessToken, cancellationToken);

    private async Task<ApiResult<TResponse>> PostAsync<TRequest, TResponse>(
        string path, TRequest body, string? bearerToken, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, BasePath + path)
        {
            Content = JsonContent.Create(body, options: JsonOptions),
        };
        if (bearerToken is not null)
        {
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        }

        try
        {
            using var response = await http.SendAsync(message, cancellationToken);

            // The API returns the ApiResult envelope on both success and failure
            // HTTP status codes, so the body is read the same way either way.
            var result = await response.Content.ReadFromJsonAsync<ApiResult<TResponse>>(
                JsonOptions, cancellationToken);

            return result ?? TransportFailure<TResponse>(
                "The server returned an empty response.",
                "أعاد الخادم استجابة فارغة.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // A genuine caller cancellation — for example the Blazor circuit
            // ending — is not a service failure; let it propagate.
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException
            or TaskCanceledException or JsonException or NotSupportedException)
        {
            // An unreachable service, a timeout, or a non-JSON body (such as a
            // reverse-proxy error page) is surfaced as a failed envelope — the
            // caller never has to catch an exception.
            return TransportFailure<TResponse>(
                "The SIMF service could not be reached. Please try again.",
                "تعذّر الوصول إلى خدمة SIMF. حاول مرة أخرى.");
        }
    }

    private static ApiResult<TResponse> TransportFailure<TResponse>(
        string message, string messageArabic) =>
        ApiResult<TResponse>.Fail(new ApiError
        {
            Code = ErrorCodes.InternalError,
            Message = message,
            MessageArabic = messageArabic,
        });
}
