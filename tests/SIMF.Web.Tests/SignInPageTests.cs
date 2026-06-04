// D-194 — bUnit coverage for the Website /login page (SignIn.razor).
// Exercises the validation guard + the two post-API branches (direct
// tokens → ticket-stash + /auth/complete redirect; OTP → session +
// /login/verify redirect) + the API-failure error path. The page had
// zero coverage before D-194.
//
// SimfAuthClient is sealed over HttpClient, so the API responses are
// controlled with a stub HttpMessageHandler returning a canned
// ApiResult<SignInResponse> envelope (camelCase web defaults, matching
// the client's JsonSerializerDefaults.Web).
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bunit;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using SIMF.ApiClient;
using SIMF.Common;
using SIMF.Contracts.Authentication;
using SIMF.Web;
using SIMF.Web.Components.Pages.Auth;

namespace SIMF.Web.Tests;

public sealed class SignInPageTests : WebComponentTestBase
{
    private readonly StubAuthHandler _handler = new();
    private readonly SimfAuthSession _session = new();
    private readonly SignInTicketStore _tickets =
        new(new MemoryCache(new MemoryCacheOptions()));

    public SignInPageTests()
    {
        // The /login page is anonymous (no [Authorize]); it injects the
        // API client + session + ticket store + NavigationManager.
        JSInterop.Mode = JSRuntimeMode.Loose; // SimfThemeToggle uses JS.
        Services.AddSingleton(new SimfAuthClient(new HttpClient(_handler)
        {
            BaseAddress = new Uri("https://api.test/"),
        }));
        Services.AddSingleton(_session);
        Services.AddSingleton(_tickets);
    }

    [Fact]
    public void Submit_with_empty_fields_does_not_call_the_API()
    {
        var cut = RenderComponent<SignIn>();

        cut.Find("form").Submit();

        // The client-side validation guard short-circuits before the
        // network call — the canonical "don't POST a blank form" check.
        Assert.Equal(0, _handler.CallCount);
    }

    [Fact]
    public void Submit_with_an_invalid_email_does_not_call_the_API()
    {
        var cut = RenderComponent<SignIn>();
        FillCredentials(cut, "not-an-email", "Secret123!");

        // D-194 review-pass (test-analyzer MEDIUM): prove the email
        // actually bound before submit. Without this, a binding
        // regression would leave the email empty and the guard would
        // still short-circuit on the empty-email branch — passing this
        // test for the wrong reason. Asserting the round-trip forces
        // the invalid-FORMAT branch (non-empty, no '@') to be what's
        // exercised.
        Assert.Equal("not-an-email", cut.FindAll("input")[0].GetAttribute("value"));

        cut.Find("form").Submit();

        Assert.Equal(0, _handler.CallCount);
    }

    [Fact]
    public void Direct_tokens_response_stashes_a_ticket_and_redirects_to_complete()
    {
        // D-194 review-pass (security MEDIUM): use distinctive, long
        // token values so the negative assertions below cannot match
        // by accident on a short substring.
        const string AccessToken = "ACCESS_TOKEN_MUST_NOT_LEAK_a1b2c3d4";
        const string RefreshToken = "REFRESH_TOKEN_MUST_NOT_LEAK_e5f6g7h8";
        _handler.Respond(ApiResult<SignInResponse>.Ok(new SignInResponse(
            MfaRequired: false, MfaToken: null, OtpToken: null,
            Tokens: new AuthTokens(AccessToken, RefreshToken, "Bearer", 1800,
                new AuthUser(Guid.NewGuid(), "visitor@simf.test", "Visitor")))));

        var cut = RenderComponent<SignIn>();
        FillCredentials(cut, "visitor@simf.test", "Secret123!");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() =>
            Assert.Contains("/auth/complete?reference=", Navigation.Uri));

        // D-194 review-pass (security MEDIUM): the BFF invariant is that
        // the raw API tokens NEVER cross the browser boundary — only the
        // one-time ticket reference does (AuthEndpoints / AccountEndpoints
        // rationale). Pin it: the access + refresh tokens must NOT appear
        // in the redirect URI or the rendered markup.
        Assert.DoesNotContain(AccessToken, Navigation.Uri);
        Assert.DoesNotContain(RefreshToken, Navigation.Uri);
        Assert.DoesNotContain(AccessToken, cut.Markup);
        Assert.DoesNotContain(RefreshToken, cut.Markup);

        // The reference embedded in the redirect URI must redeem to the
        // tokens the page stashed (proves the ticket round-trip).
        var reference = Navigation.Uri.Split("reference=")[1];
        var payload = _tickets.Redeem(reference);
        Assert.NotNull(payload);
        Assert.Equal(AccessToken, payload!.Tokens.AccessToken);
    }

    [Fact]
    public void Otp_response_sets_pending_session_and_redirects_to_verify()
    {
        _handler.Respond(ApiResult<SignInResponse>.Ok(new SignInResponse(
            MfaRequired: false, MfaToken: null, OtpToken: "otp-token-xyz")));

        var cut = RenderComponent<SignIn>();
        FillCredentials(cut, "visitor@simf.test", "Secret123!");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.EndsWith("/login/verify", Navigation.Uri));
        Assert.Equal("visitor@simf.test", _session.PendingEmail);
        Assert.Equal("otp-token-xyz", _session.PendingOtpToken);
    }

    [Fact]
    public void Failed_sign_in_renders_the_error_alert()
    {
        _handler.Respond(ApiResult<SignInResponse>.Fail(new ApiError
        {
            Code = "AUTH_BAD_CREDENTIALS",
            Message = "Those credentials did not match.",
            MessageArabic = "بيانات الاعتماد غير متطابقة.",
        }));

        var cut = RenderComponent<SignIn>();
        FillCredentials(cut, "visitor@simf.test", "WrongPass1!");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("simf-alert--error", cut.Markup);
            Assert.Contains("Those credentials did not match.", cut.Markup);
        });
    }

    private static void FillCredentials(IRenderedComponent<SignIn> cut, string email, string password)
    {
        // Re-query before each Change: a Change triggers an onchange +
        // re-render, which invalidates element handles captured by a
        // prior FindAll (bUnit UnknownEventHandlerIdException otherwise).
        cut.FindAll("input")[0].Change(email);
        cut.FindAll("input")[1].Change(password);
    }

    /// <summary>A stub HttpMessageHandler that records call count and
    /// returns a configured ApiResult envelope serialised with the same
    /// web defaults SimfAuthClient reads with.</summary>
    private sealed class StubAuthHandler : HttpMessageHandler
    {
        private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
        private string _body = "{}";

        public int CallCount { get; private set; }

        public void Respond<T>(ApiResult<T> envelope) =>
            _body = JsonSerializer.Serialize(envelope, Web);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(
                    JsonSerializer.Deserialize<object>(_body, Web)),
            });
        }
    }
}
