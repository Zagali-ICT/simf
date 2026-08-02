using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.ApiClient.Tests;

/// <summary>
/// The typed admin client must surface the API's OWN error, not replace it with
/// "the service could not be reached".
///
/// <para>Found on a live render: the Control Panel's Reset-2FA page showed
/// "The SIMF service could not be reached. Please try again." while the API had
/// answered <c>400 ADMIN_CANNOT_RESET_SELF</c> — a clean, specific, actionable
/// refusal. The operator was told the system was down when the system had in
/// fact explained itself.</para>
///
/// <para><b>Why it happened.</b> An error envelope carries <c>"data": null</c>.
/// <c>ApiResult&lt;T&gt;.Data</c> is declared <c>T?</c>, and for an unconstrained
/// <c>T</c> that annotation is erased for value types — so with <c>T = bool</c>
/// the property really is <c>bool</c>, and System.Text.Json throws rather than
/// binding null to it. The throw landed in the same catch that handles a dead
/// socket, which turns every typed refusal on such an endpoint into a fake
/// outage. <c>SimfAdminClient</c> alone has 66 <c>ApiCallResult&lt;bool&gt;</c>
/// methods, so the blast radius is every one of them.</para>
///
/// <para>These tests pin BOTH halves: a value-typed error is read correctly, and
/// a genuine transport failure still reports as one. Fixing the first by
/// swallowing the second would be worse than the bug.</para>
/// </summary>
public sealed class SimfAdminClientErrorEnvelopeTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static readonly AdminResetTwoFactorRequest Request =
        new() { Email = "someone@simf.test", Reason = "Lost the authenticator." };

    [Fact]
    public async Task A_typed_refusal_on_a_bool_endpoint_reaches_the_caller()
    {
        // The exact shape the API returns for ADMIN_CANNOT_RESET_SELF: a real
        // status, success=false, data=null, and a bilingual error.
        var client = Client(_ => Json400(
            """
            {
              "success": false,
              "data": null,
              "error": {
                "code": "ADMIN_CANNOT_RESET_SELF",
                "message": "You cannot reset your own two-factor authentication.",
                "messageArabic": "لا يمكنك إعادة تعيين التحقق بخطوتين لحسابك."
              },
              "meta": null
            }
            """));

        var result = await client.ResetTwoFactorAsync(Request, "token");

        Assert.Equal(400, result.StatusCode);
        Assert.False(result.Body.Success);
        Assert.Equal("ADMIN_CANNOT_RESET_SELF", result.Body.Error!.Code);
        Assert.Equal(
            "You cannot reset your own two-factor authentication.",
            result.Body.Error.Message);
    }

    [Fact]
    public async Task A_successful_bool_response_is_unchanged()
    {
        // The happy path must be byte-identical to before the fix.
        var client = Client(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(ApiResult<bool>.Ok(true), options: Json),
        });

        var result = await client.ResetTwoFactorAsync(Request, "token");

        Assert.Equal(200, result.StatusCode);
        Assert.True(result.Body.Success);
        Assert.True(result.Body.Data);
    }

    [Fact]
    public async Task An_error_on_a_reference_typed_endpoint_still_reaches_the_caller()
    {
        // Reference-typed payloads never hit the null-to-bool problem, so this
        // guards the fix against breaking what already worked.
        var client = Client(_ => Json400(
            """
            {
              "success": false,
              "data": null,
              "error": { "code": "VALIDATION_FAILED", "message": "Invalid.", "messageArabic": "غير صالح." }
            }
            """));

        var result = await client.CreateAdminAsync(
            new AdminCreateAdminRequest { Email = "x@simf.test", DisplayName = "X" }, "token");

        Assert.Equal(400, result.StatusCode);
        Assert.Equal("VALIDATION_FAILED", result.Body.Error!.Code);
    }

    [Fact]
    public async Task A_dead_socket_is_still_reported_as_unreachable()
    {
        // The behaviour the message was written for, and the one the fix must
        // not trade away: nothing answered, so "could not be reached" is true.
        var client = new SimfAdminClient(
            new HttpClient(new ThrowingHandler()) { BaseAddress = new Uri("http://localhost/") });

        var result = await client.ResetTwoFactorAsync(Request, "token");

        Assert.Equal(503, result.StatusCode);
        Assert.False(result.Body.Success);
        Assert.Contains("could not be reached", result.Body.Error!.Message);
    }

    [Fact]
    public async Task A_non_json_error_body_is_reported_as_unreachable()
    {
        // An HTML error page from a proxy is NOT the API explaining itself, so
        // the transport-failure message is the honest answer here.
        var client = Client(_ => new HttpResponseMessage(HttpStatusCode.BadGateway)
        {
            Content = new StringContent("<html>502 Bad Gateway</html>", Encoding.UTF8, "text/html"),
        });

        var result = await client.ResetTwoFactorAsync(Request, "token");

        Assert.Equal(503, result.StatusCode);
        Assert.Contains("could not be reached", result.Body.Error!.Message);
    }

    [Fact]
    public async Task An_empty_error_body_is_reported_as_unreachable()
    {
        var client = Client(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent(string.Empty, Encoding.UTF8, "application/json"),
        });

        var result = await client.ResetTwoFactorAsync(Request, "token");

        Assert.Equal(503, result.StatusCode);
        Assert.Contains("could not be reached", result.Body.Error!.Message);
    }

    [Fact]
    public async Task A_refusal_declared_in_another_charset_is_still_read()
    {
        // The envelope read buffers the body before parsing it. Doing that over
        // raw bytes would assume UTF-8 and mangle anything else — including the
        // Arabic half of every SIMF error — so it goes through the charset the
        // response declares, exactly as the ReadFromJsonAsync it replaced did.
        // The API emits UTF-8; this pins the decoding so that stays a fact about
        // the API rather than a requirement of the client.
        const string body = """
            {
              "success": false,
              "data": null,
              "error": {
                "code": "ADMIN_CANNOT_RESET_SELF",
                "message": "You cannot reset your own two-factor authentication.",
                "messageArabic": "لا يمكنك إعادة تعيين التحقق بخطوتين لحسابك."
              }
            }
            """;
        var client = Client(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent(body, Encoding.Unicode, "application/json"),
        });

        var result = await client.ResetTwoFactorAsync(Request, "token");

        Assert.Equal(400, result.StatusCode);
        Assert.Equal("ADMIN_CANNOT_RESET_SELF", result.Body.Error!.Code);
        Assert.Equal(
            "لا يمكنك إعادة تعيين التحقق بخطوتين لحسابك.",
            result.Body.Error.MessageArabic);
    }

    [Fact]
    public async Task A_success_envelope_with_no_payload_is_NOT_reported_as_a_completed_call()
    {
        // The dangerous shape, and the reason the recovery is guarded on Error
        // rather than just returning default(T). This body claims success but
        // carries no payload, so on a bool endpoint there is no honest answer:
        // reporting it as a completed call that returned "false" would tell the
        // operator the action ran and declined, when in truth nothing is known.
        // It must land on the transport-failure arm instead.
        var client = Client(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{ "success": true, "data": null, "error": null }""",
                Encoding.UTF8, "application/json"),
        });

        var result = await client.ResetTwoFactorAsync(Request, "token");

        Assert.Equal(503, result.StatusCode);
        Assert.False(result.Body.Success);
        Assert.Contains("could not be reached", result.Body.Error!.Message);
    }

    [Fact]
    public async Task The_account_client_recovers_a_typed_refusal_too()
    {
        // SimfAccountClient carries the same reader and four of the same
        // bool-returning methods. Without this, reverting its half of the change
        // would be invisible to the whole suite.
        var client = new SimfAccountClient(
            new HttpClient(new StubHandler(_ => Json400(
                """
                {
                  "success": false,
                  "data": null,
                  "error": { "code": "NOTIFICATION_NOT_FOUND", "message": "Not found.", "messageArabic": "غير موجود." }
                }
                """)))
            { BaseAddress = new Uri("http://localhost/") });

        var result = await client.MarkNotificationReadAsync(Guid.NewGuid(), "token");

        Assert.Equal(400, result.StatusCode);
        Assert.Equal("NOTIFICATION_NOT_FOUND", result.Body.Error!.Code);
    }

    private static HttpResponseMessage Json400(string body) =>
        new(HttpStatusCode.BadRequest)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };

    private static SimfAdminClient Client(Func<HttpRequestMessage, HttpResponseMessage> responder) =>
        new(new HttpClient(new StubHandler(responder)) { BaseAddress = new Uri("http://localhost/") });

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
