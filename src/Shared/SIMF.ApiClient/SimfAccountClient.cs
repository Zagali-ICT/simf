using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SIMF.Common;
using SIMF.Contracts.Authentication;

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
    private const string BasePath = "api/v1/";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Returns the signed-in user's profile.</summary>
    public Task<ApiCallResult<ProfileResponse>> GetProfileAsync(
        string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<ProfileResponse>(HttpMethod.Get, "account/profile", null, accessToken, cancellationToken);

    /// <summary>Begins authenticator-app enrolment — returns the secret, otpauth URI and QR.</summary>
    public Task<ApiCallResult<TotpSetupResponse>> TotpSetupAsync(
        string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<TotpSetupResponse>(HttpMethod.Post, "auth/totp/setup", null, accessToken, cancellationToken);

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
            var body = await response.Content.ReadFromJsonAsync<ApiResult<T>>(
                JsonOptions, cancellationToken);
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
