using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SIMF.Common;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Notifications;
using SIMF.Contracts.Organisations;
using SIMF.Contracts.UserProfile;

namespace SIMF.ApiClient;

/// <summary>
/// A typed client over the SIMF Account API — the profile, the authenticator
/// enrolment, and the avatar (myComment item #11). Every method takes the
/// caller's bearer access token explicitly and returns the upstream HTTP
/// status alongside the <see cref="ApiResult{T}"/> body, so a proxy can
/// forward the status verbatim (5-agent review SEV-1.3). A transport failure
/// is mapped to a 503 envelope, so a caller never has to catch.
/// </summary>
public sealed class SimfAccountClient(HttpClient http)
{
    private const string BasePath = "api/v1/app/";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Returns the signed-in user's account profile (id, email,
    /// display name, avatar, 2FA flag, roles). Not to be confused with the
    /// visitor-onboarding "user-profile" surface (Arabic/English name,
    /// nationality, ID image, etc.) at <c>account/user-profile</c>.</summary>
    public Task<ApiCallResult<ProfileResponse>> GetProfileAsync(
        string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<ProfileResponse>(HttpMethod.Get, "account/profile", null, accessToken, cancellationToken);

    /// <summary>Begins authenticator-app enrolment — returns the secret, otpauth URI and QR.</summary>
    public Task<ApiCallResult<TotpSetupResponse>> TotpSetupAsync(
        string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<TotpSetupResponse>(HttpMethod.Post, "auth/totp/setup", null, accessToken, cancellationToken);

    /// <summary>
    /// Returns the QR + otpauth URI for the caller's CURRENT
    /// authenticator secret without rotating it. Returns a 404
    /// <see cref="ApiCallResult{T}"/> when the account has no active
    /// secret yet — UI should route through <see cref="TotpSetupAsync"/>.
    /// </summary>
    public Task<ApiCallResult<TotpSetupResponse>> TotpPairingAsync(
        string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<TotpSetupResponse>(HttpMethod.Get, "auth/totp/pairing", null, accessToken, cancellationToken);

    /// <summary>
    /// Verifies a code against the user's active authenticator
    /// secret without mutating state (no replay-guard, no flag, no audit).
    /// Used by the <c>/account/totp-pairing</c> page to confirm a scan
    /// succeeded.
    /// </summary>
    public Task<ApiCallResult<TotpPairingVerifyResponse>> TotpPairingVerifyAsync(
        TotpConfirmRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<TotpPairingVerifyResponse>(
            HttpMethod.Post, "auth/totp/pairing/verify",
            JsonContent.Create(request, options: JsonOptions), accessToken, cancellationToken);

    /// <summary>Confirms TOTP enrolment with a first valid code.</summary>
    public Task<ApiCallResult<TotpConfirmResponse>> TotpConfirmAsync(
        TotpConfirmRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<TotpConfirmResponse>(
            HttpMethod.Post, "auth/totp/confirm",
            JsonContent.Create(request, options: JsonOptions), accessToken, cancellationToken);

    /// <summary>Turns 2FA off after verifying a current code.</summary>
    public Task<ApiCallResult<TotpDisableResponse>> TotpDisableAsync(
        TotpDisableRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<TotpDisableResponse>(
            HttpMethod.Post, "auth/totp/disable",
            JsonContent.Create(request, options: JsonOptions), accessToken, cancellationToken);

    /// <summary>Uploads a new avatar.</summary>
    public Task<ApiCallResult<AvatarResponse>> UploadAvatarAsync(
        byte[] content, string contentType, string fileName, string accessToken,
        CancellationToken cancellationToken = default)
    {
        var multipart = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(content);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        multipart.Add(fileContent, "File", fileName);
        return SendAsync<AvatarResponse>(
            HttpMethod.Post, "account/avatar", multipart, accessToken, cancellationToken);
    }

    /// <summary>Removes the current avatar.</summary>
    public Task<ApiCallResult<AvatarResponse>> DeleteAvatarAsync(
        string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<AvatarResponse>(
            HttpMethod.Delete, "account/avatar", null, accessToken, cancellationToken);

    /// <summary>
    /// Mints a fresh batch of 10 single-use recovery codes — the response
    /// carries them plaintext exactly once.
    /// </summary>
    public Task<ApiCallResult<RecoveryCodesResponse>> RegenerateRecoveryCodesAsync(
        string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<RecoveryCodesResponse>(
            HttpMethod.Post, "account/recovery-codes/regenerate", null,
            accessToken, cancellationToken);

    /// <summary>
    /// Streams the user's avatar bytes back. Returns (statusCode, contentType,
    /// bytes); on a non-success status, <c>Bytes</c> is empty and
    /// <c>ContentType</c> is null — the caller forwards the status verbatim.
    /// </summary>
    public async Task<(int StatusCode, string? ContentType, byte[] Bytes)> FetchAvatarAsync(
        Guid userId, string accessToken, CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(
            HttpMethod.Get, $"{BasePath}account/avatar/{userId:N}");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        try
        {
            using var response = await http.SendAsync(message, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return ((int)response.StatusCode, null, []);
            }
            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            return (
                (int)response.StatusCode,
                response.Content.Headers.ContentType?.MediaType,
                bytes);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException
            or TaskCanceledException)
        {
            return ((int)HttpStatusCode.ServiceUnavailable, null, []);
        }
    }

    /// <summary>Returns the signed-in user's profile (decisions D-046 b,
    /// P8 — D-049; renamed from <c>GetVisitorProfileAsync</c>).</summary>
    public Task<ApiCallResult<UserProfileResponse>> GetUserProfileAsync(
        string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<UserProfileResponse>(
            HttpMethod.Get, "account/user-profile", null,
            accessToken, cancellationToken);

    /// <summary>Creates / updates the user's profile (D-046 b, P8).</summary>
    public Task<ApiCallResult<UserProfileResponse>> UpsertUserProfileAsync(
        UpsertUserProfileRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<UserProfileResponse>(
            HttpMethod.Post, "account/user-profile",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>Returns the supported nationality picker list (D-046 b, P8).</summary>
    public Task<ApiCallResult<CountryListResponse>> GetProfileCountriesAsync(
        string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<CountryListResponse>(
            HttpMethod.Get, "account/user-profile/countries", null,
            accessToken, cancellationToken);

    /// <summary>Returns the active interests for the visitor picker
    /// (P9 — D-050; الاهتمامات).</summary>
    public Task<ApiCallResult<InterestListResponse>> GetActiveInterestsAsync(
        string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<InterestListResponse>(
            HttpMethod.Get, "account/interests", null,
            accessToken, cancellationToken);

    /// <summary>B3 — D-221 (الجهة): searches the active organisations lookup for
    /// the registration picker. A blank term returns the first <paramref name="top"/>
    /// by Arabic name; otherwise it matches the term across Arabic / English
    /// name and city. Backs the Web profile + CP walk-in organisation picker.</summary>
    public Task<ApiCallResult<IReadOnlyList<OrganisationPickerItem>>> SearchOrganisationsAsync(
        string? search, int top, string accessToken,
        CancellationToken cancellationToken = default)
    {
        var query = $"organisations?top={top}";
        if (!string.IsNullOrWhiteSpace(search))
        {
            query += $"&search={Uri.EscapeDataString(search.Trim())}";
        }
        return SendAsync<IReadOnlyList<OrganisationPickerItem>>(
            HttpMethod.Get, query, null, accessToken, cancellationToken);
    }

    // -- P12 — D-053: in-app notifications -----------------------------------

    /// <summary>One page of the signed-in user's notifications.</summary>
    public Task<ApiCallResult<GridPage<NotificationDto>>> ListNotificationsAsync(
        GridQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<NotificationDto>>(
            HttpMethod.Post, "account/notifications/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>Count of unread notifications for the signed-in user.</summary>
    public Task<ApiCallResult<UnreadCountResponse>> GetUnreadNotificationCountAsync(
        string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<UnreadCountResponse>(
            HttpMethod.Get, "account/notifications/unread-count", null,
            accessToken, cancellationToken);

    /// <summary>Mark one notification as read (idempotent).</summary>
    public Task<ApiCallResult<bool>> MarkNotificationReadAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Post, $"account/notifications/{id}/read", null,
            accessToken, cancellationToken);

    /// <summary>Mark every unread notification as read.</summary>
    public Task<ApiCallResult<bool>> MarkAllNotificationsReadAsync(
        string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Post, "account/notifications/read-all", null,
            accessToken, cancellationToken);

    /// <summary>Delete one notification (idempotent).</summary>
    public Task<ApiCallResult<bool>> DeleteNotificationAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Delete, $"account/notifications/{id}", null,
            accessToken, cancellationToken);

    /// <summary>Uploads the user's ID-document image (D-046 b, P8). The
    /// bytes are magic-byte gated server-side and encrypted-at-rest with
    /// AES-GCM.</summary>
    public Task<ApiCallResult<bool>> UploadUserIdDocumentAsync(
        byte[] content, string contentType, string fileName, string accessToken,
        CancellationToken cancellationToken = default)
    {
        var multipart = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(content);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        multipart.Add(fileContent, "File", fileName);
        return SendAsync<bool>(
            HttpMethod.Post, "account/user-profile/id-image",
            multipart, accessToken, cancellationToken);
    }

    /// <summary>Streams the user's decrypted ID-document image bytes
    /// (D-046 b, P8).</summary>
    public async Task<(int StatusCode, string? ContentType, byte[] Bytes)> FetchUserIdDocumentAsync(
        string accessToken, CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(
            HttpMethod.Get, $"{BasePath}account/profile/id-image");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        try
        {
            using var response = await http.SendAsync(message, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return ((int)response.StatusCode, null, []);
            }
            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            return (
                (int)response.StatusCode,
                response.Content.Headers.ContentType?.MediaType,
                bytes);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException
            or TaskCanceledException)
        {
            return ((int)HttpStatusCode.ServiceUnavailable, null, []);
        }
    }

    private async Task<ApiCallResult<T>> SendAsync<T>(
        HttpMethod method, string path, HttpContent? content,
        string accessToken, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(method, BasePath + path)
        {
            Content = content,
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        try
        {
            using var response = await http.SendAsync(message, cancellationToken);
            // ApiEnvelope, not ReadFromJsonAsync: an error body carries
            // "data": null, which cannot bind to a value-typed T, and four
            // methods here return ApiCallResult<bool>. See ApiEnvelope.
            var body = await ApiEnvelope.ReadAsync<T>(
                response.Content, JsonOptions, cancellationToken);
            return new ApiCallResult<T>(
                (int)response.StatusCode,
                body ?? TransportFailure<T>(
                    "The server returned an empty response.",
                    "أعاد الخادم استجابة فارغة."));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException
            or TaskCanceledException or JsonException or NotSupportedException)
        {
            return new ApiCallResult<T>(
                (int)HttpStatusCode.ServiceUnavailable,
                TransportFailure<T>(
                    "The SIMF service could not be reached. Please try again.",
                    "تعذّر الوصول إلى خدمة SIMF. حاول مرة أخرى."));
        }
    }

    private static ApiResult<T> TransportFailure<T>(
        string message, string messageArabic) =>
        ApiResult<T>.Fail(new ApiError
        {
            Code = ErrorCodes.InternalError,
            Message = message,
            MessageArabic = messageArabic,
        });
}
