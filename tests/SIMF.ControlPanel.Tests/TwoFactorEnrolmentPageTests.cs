// #2 (Q1, 2026-07-30) — the Control Panel half of enrolment-first.
//
// The API withholds the session for a Cp-audience admin with no authenticator
// paired and returns an enrolment ticket instead. If the CP does not branch on
// that field, the sign-in page silently falls through to "no MfaToken and no
// Tokens" and leaves the operator on /login with no error and no way forward —
// the fix would look like a lockout even though the API behaved correctly.
//
// These render the two pages against a stub HttpMessageHandler (SimfAuthClient
// is sealed over HttpClient), the same shape SignInPageTests uses.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using SIMF.ApiClient;
using SIMF.Common;
using SIMF.Contracts.Authentication;
using SIMF.ControlPanel;
using SIMF.ControlPanel.Components.Pages.Auth;

namespace SIMF.ControlPanel.Tests;

public sealed class TwoFactorEnrolmentPageTests : CpComponentTestBase
{
    private readonly StubAuthHandler _handler = new();
    private readonly SimfAuthSession _session = new();
    private readonly SignInTicketStore _tickets =
        new(new MemoryCache(new MemoryCacheOptions()));

    public TwoFactorEnrolmentPageTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose; // SimfThemeToggle uses JS.
        Services.AddSingleton(new SimfAuthClient(new HttpClient(_handler)
        {
            BaseAddress = new Uri("https://api.test/"),
        }));
        Services.AddSingleton(_session);
        Services.AddSingleton(_tickets);
    }

    /// <summary>The routing branch. Without it the sign-in page falls through to
    /// the "no MfaToken, no Tokens" case and strands the operator on /login.</summary>
    [Fact]
    public void An_enrolment_challenge_routes_the_sign_in_page_to_the_enrolment_page()
    {
        _handler.Respond(ApiResult<SignInResponse>.Ok(new SignInResponse(
            MfaRequired: false, MfaToken: null, OtpToken: null,
            Tokens: null, AccountState: null, PasswordChangeToken: null,
            TwoFactorEnrolmentToken: "enrol-ticket-123")));

        var navigation = (FakeNavigationManager)Services.GetRequiredService<NavigationManager>();
        var cut = RenderComponent<SignIn>();
        cut.FindAll("input")[0].Change("admin@simf.test");
        cut.FindAll("input")[1].Change("Secret123!");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() =>
        {
            Assert.EndsWith("/login/enrol-2fa", navigation.Uri, StringComparison.Ordinal);
            // The ticket has to survive the navigation, or the enrolment page
            // bounces straight back to /login.
            Assert.Equal("enrol-ticket-123", _session.PendingEnrolmentToken);
        });
    }

    /// <summary>The enrolment page is a step of a sign-in, not a public page: with
    /// no ticket in the circuit it must not stage a secret for anybody.</summary>
    [Fact]
    public void The_enrolment_page_bounces_to_sign_in_when_no_ticket_is_held()
    {
        var navigation = (FakeNavigationManager)Services.GetRequiredService<NavigationManager>();

        var cut = RenderComponent<TwoFactorEnrolment>();

        Assert.EndsWith("/login", navigation.Uri, StringComparison.Ordinal);
        // No card, no QR, no code field — nothing was staged for a caller who
        // never proved a password.
        Assert.DoesNotContain("simf-qr", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Auth.Enrol2fa.Instruction", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>The golden render: the QR and the typable base32 key are both on
    /// the page, because an admin whose scanner fails still has to get in.</summary>
    [Fact]
    public void The_enrolment_page_renders_the_qr_and_the_typable_key()
    {
        _session.PendingEnrolmentToken = "enrol-ticket-123";
        _handler.Respond(ApiResult<TotpSetupResponse>.Ok(new TotpSetupResponse(
            Secret: "JBSWY3DPEHPK3PXPTEST",
            OtpAuthUri: "otpauth://totp/SIMF:admin@simf.test?secret=JBSWY3DPEHPK3PXPTEST",
            QrCodeSvg: "<svg role='img'></svg>")));

        var cut = RenderComponent<TwoFactorEnrolment>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("<svg", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("JBSWY3DPEHPK3PXPTEST", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Auth.Enrol2fa.Instruction", cut.Markup, StringComparison.Ordinal);
        });
    }

    /// <summary>A failed start must say so and offer the way back, rather than
    /// render an empty card the operator cannot act on.</summary>
    [Fact]
    public void A_failed_enrolment_start_shows_the_error_and_a_link_back_to_sign_in()
    {
        _session.PendingEnrolmentToken = "expired-ticket";
        _handler.Respond(ApiResult<TotpSetupResponse>.Fail(new ApiError
        {
            Code = ErrorCodes.AuthTwoFactorEnrolmentRequired,
            Message = "Two-factor enrolment must be started from a fresh sign-in.",
            MessageArabic = "يجب بدء تسجيل المصادقة الثنائية من تسجيل دخول جديد.",
        }));

        var cut = RenderComponent<TwoFactorEnrolment>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains(
                "Two-factor enrolment must be started from a fresh sign-in.",
                cut.Markup, StringComparison.Ordinal);
            Assert.Contains("href=\"/login\"", cut.Markup, StringComparison.Ordinal);
            // Probe the QR block specifically rather than "<svg" anywhere: the
            // back-to-sign-in link renders its own simf-icon svg, so a bare
            // DoesNotContain("<svg") contradicts the assertion right above it.
            // What must not reach the page on a failed start is the QR and the
            // shared secret.
            Assert.DoesNotContain("simf-qr", cut.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain(
                "simf-totp-secret", cut.Markup, StringComparison.Ordinal);
        });
    }

    /// <summary>Stub handler returning a configured ApiResult envelope,
    /// serialised with the same web defaults SimfAuthClient reads with.</summary>
    private sealed class StubAuthHandler : HttpMessageHandler
    {
        private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
        private string _body = "{}";

        public void Respond<T>(ApiResult<T> envelope) =>
            _body = JsonSerializer.Serialize(envelope, Web);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(JsonSerializer.Deserialize<object>(_body, Web)),
            });
    }
}
