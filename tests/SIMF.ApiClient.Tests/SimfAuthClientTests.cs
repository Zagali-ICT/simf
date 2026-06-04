using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.ApiClient.Tests;

/// <summary>Tests for the typed Login-API client.</summary>
public sealed class SimfAuthClientTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Sign_in_returns_the_success_envelope()
    {
        var envelope = ApiResult<SignInResponse>.Ok(new SignInResponse(true, "mfa-token", null));
        var client = ClientReturning(HttpStatusCode.OK, envelope);

        var result = await client.SignInAsync(
            new SignInRequest { Email = "admin@simf.test", Password = "secret" });

        Assert.True(result.Success);
        Assert.Equal("mfa-token", result.Data!.MfaToken);
    }

    [Fact]
    public async Task Sign_in_surfaces_a_failure_envelope_returned_with_an_error_status()
    {
        var envelope = ApiResult<SignInResponse>.Fail(new ApiError
        {
            Code = ErrorCodes.AuthInvalidCredentials,
            Message = "Incorrect email or password.",
            MessageArabic = "البريد الإلكتروني أو كلمة المرور غير صحيحة.",
        });
        var client = ClientReturning(HttpStatusCode.Unauthorized, envelope);

        var result = await client.SignInAsync(
            new SignInRequest { Email = "admin@simf.test", Password = "wrong" });

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.AuthInvalidCredentials, result.Error!.Code);
    }

    [Fact]
    public async Task A_transport_failure_is_mapped_to_a_failed_envelope()
    {
        var client = new SimfAuthClient(new HttpClient(new ThrowingHandler())
        {
            BaseAddress = new Uri("http://localhost/"),
        });

        var result = await client.SignInAsync(
            new SignInRequest { Email = "admin@simf.test", Password = "secret" });

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.InternalError, result.Error!.Code);
    }

    [Fact]
    public async Task Change_password_attaches_the_bearer_access_token()
    {
        HttpRequestMessage? captured = null;
        var handler = new StubHandler(request =>
        {
            captured = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(
                    ApiResult<ChangePasswordResponse>.Ok(new ChangePasswordResponse(true)), options: Json),
            };
        });
        var client = new SimfAuthClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") });

        await client.ChangePasswordAsync(
            new ChangePasswordRequest
            {
                CurrentPassword = "old-secret",
                NewPassword = "New1Secret",
                ConfirmPassword = "New1Secret",
            },
            "access-token-123");

        Assert.Equal("Bearer", captured!.Headers.Authorization!.Scheme);
        Assert.Equal("access-token-123", captured.Headers.Authorization.Parameter);
    }

    [Fact]
    public async Task Sign_out_attaches_the_bearer_access_token()
    {
        HttpRequestMessage? captured = null;
        var handler = new StubHandler(request =>
        {
            captured = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(
                    ApiResult<SignOutResponse>.Ok(new SignOutResponse(true)), options: Json),
            };
        });
        var client = new SimfAuthClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") });

        await client.SignOutAsync("access-token-456");

        Assert.Equal("Bearer", captured!.Headers.Authorization!.Scheme);
        Assert.Equal("access-token-456", captured.Headers.Authorization.Parameter);
    }

    [Fact]
    public async Task An_empty_response_body_is_mapped_to_a_failed_envelope()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(string.Empty),
        });
        var client = new SimfAuthClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") });

        var result = await client.SignInAsync(
            new SignInRequest { Email = "admin@simf.test", Password = "secret" });

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.InternalError, result.Error!.Code);
    }

    private static SimfAuthClient ClientReturning<T>(HttpStatusCode status, ApiResult<T> envelope)
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(status)
        {
            Content = JsonContent.Create(envelope, options: Json),
        });
        return new SimfAuthClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") });
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(responder(request));
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("The connection was refused.");
    }
}
