// D-206 regression coverage for the Control Panel /login page (SignIn.razor),
// specifically the forced-password-change popup.
//
// The bug this guards: a string-typed component parameter passed WITHOUT the
// `@` prefix (`ErrorMessage="_changeError"`) is a *literal string*, not a field
// binding — so ChangePasswordCard received the constant "_changeError", which is
// non-null, and rendered it as a permanent red error banner whenever the popup
// opened. The fix is `ErrorMessage="@_changeError"`. This test opens the popup
// and asserts the banner is absent; it fails on the literal-binding bug.
//
// SimfAuthClient is sealed over HttpClient, so the sign-in response is driven by
// a stub HttpMessageHandler returning a canned ApiResult<SignInResponse>
// (web JSON defaults, matching the client).
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bunit;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using SIMF.ApiClient;
using SIMF.Common;
using SIMF.Contracts.Authentication;
using SIMF.ControlPanel;
using SIMF.ControlPanel.Components.Pages.Auth;

namespace SIMF.ControlPanel.Tests;

public sealed class SignInPageTests : CpComponentTestBase
{
    private readonly StubAuthHandler _handler = new();
    private readonly SimfAuthSession _session = new();
    private readonly SignInTicketStore _tickets =
        new(new MemoryCache(new MemoryCacheOptions()));

    public SignInPageTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose; // SimfThemeToggle + SimfModal use JS.
        Services.AddSingleton(new SimfAuthClient(new HttpClient(_handler)
        {
            BaseAddress = new Uri("https://api.test/"),
        }));
        Services.AddSingleton(_session);
        Services.AddSingleton(_tickets);
    }

    [Fact]
    public void Forced_change_credential_opens_the_popup_with_no_spurious_error_banner()
    {
        // CP sign-in for a PasswordChangeRequired account returns a ticket
        // (no session) — the page must open the change-password popup.
        _handler.Respond(ApiResult<SignInResponse>.Ok(new SignInResponse(
            MfaRequired: false, MfaToken: null, OtpToken: null,
            Tokens: null, AccountState: null,
            PasswordChangeToken: "pct-forced-change-123")));

        var cut = RenderComponent<SignIn>();
        FillCredentials(cut, "admin@simf.test", "Secret123!");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() =>
        {
            // The popup is open (its intro copy is present).
            Assert.Contains("Auth.ChangePassword.Intro", cut.Markup);
            // Regression: no error banner, and the field name never leaks into
            // the DOM as a literal (the pre-fix `ErrorMessage="_changeError"`).
            Assert.DoesNotContain("_changeError", cut.Markup);
            Assert.DoesNotContain("simf-alert--error", cut.Markup);
        });
    }

    [Fact]
    public void Failed_sign_in_renders_the_error_alert()
    {
        _handler.Respond(ApiResult<SignInResponse>.Fail(new ApiError
        {
            Code = "AUTH_INVALID_CREDENTIALS",
            Message = "The email address or password is not correct.",
            MessageArabic = "البريد الإلكتروني أو كلمة المرور غير صحيحة.",
        }));

        var cut = RenderComponent<SignIn>();
        FillCredentials(cut, "admin@simf.test", "WrongPass1!");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() =>
            Assert.Contains("The email address or password is not correct.", cut.Markup));
    }

    private static void FillCredentials(IRenderedComponent<SignIn> cut, string email, string password)
    {
        // Re-query before each Change — a Change re-renders and invalidates
        // handles captured by a prior FindAll (bUnit UnknownEventHandlerId).
        cut.FindAll("input")[0].Change(email);
        cut.FindAll("input")[1].Change(password);
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
