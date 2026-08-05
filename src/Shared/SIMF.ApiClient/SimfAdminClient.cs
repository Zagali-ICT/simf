using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SIMF.Common;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Ai;
using SIMF.Contracts.Requests;
using SIMF.Contracts.Archive;
using SIMF.Contracts.Attendance;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.BusinessMeetings;
using SIMF.Contracts.Exhibition;
using SIMF.Contracts.Exhibitors;
using SIMF.Contracts.Email;
using SIMF.Contracts.Faq;
using SIMF.Contracts.Organisations;
using SIMF.Contracts.Feedback;
using SIMF.Contracts.Logs;
using SIMF.Contracts.Media;
using SIMF.Contracts.Organization;
using SIMF.Contracts.Programme;
using SIMF.Contracts.PublicRelations;
using SIMF.Contracts.Regions;
using SIMF.Contracts.Sessions;
using SIMF.Contracts.Statistics;
using SIMF.Contracts.Configuration;
using SIMF.Contracts.Ops;
using SIMF.Contracts.Support;

using SIMF.Common.Enums;

namespace SIMF.ApiClient;

/// <summary>
/// A typed client over the SIMF Admin API (decision D-041). Today: reset
/// another user's 2FA. The actor must hold the Administrator role; the
/// access-token check is at the API.
/// </summary>
public sealed partial class SimfAdminClient(HttpClient http)
{
    private const string BasePath = "api/v1/admin/";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Wipes the target user's 2FA after Administrator-role verification.</summary>
    public Task<ApiCallResult<bool>> ResetTwoFactorAsync(
        AdminResetTwoFactorRequest request,
        string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Post, "admins/reset-two-factor",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>The live status of every in-process background worker, for the CP
    /// services monitor. Reads the API's heartbeat-registry snapshot.</summary>
    public Task<ApiCallResult<WorkerStatusListResponse>> GetWorkerStatusesAsync(
        string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<WorkerStatusListResponse>(
            HttpMethod.Get, "ops/workers", content: null,
            accessToken, cancellationToken);


    /// <summary>P1.6 — POSTs a JSON body and returns the raw response bytes
    /// (an XLSX workbook). Shared by the read-only-grid exports; the response
    /// body is binary, so it bypasses the <c>ApiResult</c> envelope.</summary>
    private async Task<(int StatusCode, byte[] Bytes)> PostForBytesAsync(
        string relativePath, object body, string accessToken,
        CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, BasePath + relativePath)
        {
            Content = JsonContent.Create(body, options: JsonOptions),
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        try
        {
            using var response = await http.SendAsync(message, cancellationToken);
            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            return ((int)response.StatusCode, bytes);
        }
        catch (Exception exception) when (exception is HttpRequestException
            or TaskCanceledException)
        {
            return ((int)HttpStatusCode.ServiceUnavailable, Array.Empty<byte>());
        }
    }

    /// <summary>D-148 — operator-surface helper. Routes through the
    /// <c>/api/v1/app/gates/</c> prefix rather than <c>/api/v1/admin/</c>.</summary>
    private Task<ApiCallResult<T>> SendOperatorAsync<T>(
        HttpMethod method, string path, HttpContent? content,
        string accessToken, CancellationToken cancellationToken) =>
        SendWithBaseAsync<T>("api/v1/app/gates/", method, path, content,
            accessToken, cancellationToken);

    /// <summary>D-169 (gap doc G6) — sessions/questions helper. Routes
    /// through the <c>/api/v1/app/sessions/</c> prefix.</summary>
    private Task<ApiCallResult<T>> SendSessionsAsync<T>(
        HttpMethod method, string path, HttpContent? content,
        string accessToken, CancellationToken cancellationToken) =>
        SendWithBaseAsync<T>("api/v1/app/sessions/", method, path, content,
            accessToken, cancellationToken);

    private Task<ApiCallResult<T>> SendAsync<T>(
        HttpMethod method, string path, HttpContent? content,
        string accessToken, CancellationToken cancellationToken) =>
        SendWithBaseAsync<T>(BasePath, method, path, content, accessToken, cancellationToken);

    private async Task<ApiCallResult<T>> SendWithBaseAsync<T>(
        string basePath, HttpMethod method, string path, HttpContent? content,
        string accessToken, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(method, basePath + path) { Content = content };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        try
        {
            using var response = await http.SendAsync(message, cancellationToken);
            // ApiEnvelope, not ReadFromJsonAsync: an error body carries
            // "data": null, which cannot bind to a value-typed T, and 66 methods
            // here return ApiCallResult<bool>. See ApiEnvelope for the full note.
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

    private static ApiResult<T> TransportFailure<T>(string message, string messageArabic) =>
        ApiResult<T>.Fail(new ApiError
        {
            Code = ErrorCodes.InternalError,
            Message = message,
            MessageArabic = messageArabic,
        });
}
