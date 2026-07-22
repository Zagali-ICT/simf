// D-735 — integration tests for the CP admin email-template surface: the DB
// holds only overrides, the code catalogue backs every read so the grid always
// shows all nine templates, save validates the copy references no unknown token
// and bumps a version, reset drops the override, preview renders sample values.
// All routes are gated RequireApprovedAccount + an EmailTemplates permission and
// return the ApiResult<T> envelope; {type} is the EmailTemplateType name
// (case-insensitive). Mirrors AiModuleTests' fixture + admin-auth idioms.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Email;
using SIMF.Domain.IdentityAccess;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class EmailTemplateAdminTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public EmailTemplateAdminTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    // -- List ----------------------------------------------------------------

    [Fact]
    public async Task List_returns_all_nine_templates_clean_by_default()
    {
        // Order-independent: every mutating test in this class resets its
        // override so, with parallelism disabled, the DB is clean at every
        // test boundary and the grid shows the nine catalogue defaults (#24 added
        // EmailChangeVerification + EmailChangedNotice; the assertion was stale at
        // 6 on the base branch after D-751 added BulkBadgeDelivery, the 7th).
        var admin = await CreateAdministratorAndSignInAsync();

        var response = await PostAuthAsync(
            "/api/v1/admin/email/templates/list", new GridQuery { Top = 100 }, admin);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = (await response.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminEmailTemplateSummary>>>())!.Data!;
        Assert.Equal(9, page.Items.Count);
        Assert.All(page.Items, row =>
        {
            Assert.False(row.IsOverride);
            Assert.Equal(0, row.Version);
        });
    }

    // -- Get -----------------------------------------------------------------

    [Fact]
    public async Task Get_signin_otp_returns_the_code_default_detail()
    {
        var admin = await CreateAdministratorAndSignInAsync();

        var detail = await GetDetailAsync(EmailTemplateType.SignInOtp, admin);

        Assert.False(detail.IsOverride);
        Assert.Equal("SIMF sign-in code", detail.Subject);
        Assert.Equal(2, detail.Tokens.Count);
        Assert.Equal("SIMF sign-in code", detail.DefaultSubject);
    }

    // -- Update (version bump) -----------------------------------------------

    [Fact]
    public async Task Update_saves_the_override_and_bumps_the_version()
    {
        // PasswordReset is only mutated here (and reset in the finally), so it
        // is clean when this test starts regardless of test order.
        const EmailTemplateType type = EmailTemplateType.PasswordReset;
        var admin = await CreateAdministratorAndSignInAsync();

        try
        {
            var first = await UpdateAsync(type, admin, new UpdateEmailTemplateRequest
            {
                Subject = "SIMF reset (edited)",
                BodyEn = "<p>Your reset code is {Code}.</p>",
                BodyAr = "<p>رمز إعادة التعيين هو {Code}.</p>",
                IsActive = true,
            });
            Assert.Equal(1, first.Version);
            Assert.True(first.IsOverride);

            var second = await UpdateAsync(type, admin, new UpdateEmailTemplateRequest
            {
                Subject = "SIMF reset (edited again)",
                BodyEn = "<p>Your reset code is {Code}, valid {ExpiryMinutes} minutes.</p>",
                BodyAr = "<p>رمزك هو {Code} صالح {ExpiryMinutes} دقائق.</p>",
                IsActive = true,
            });
            Assert.Equal(2, second.Version);
            Assert.True(second.IsOverride);
        }
        finally
        {
            // Revert so the clean-by-default list test is order-independent.
            await PostAuthAsync(RouteFor(type, "reset"), EmptyBody, admin);
        }
    }

    // -- Update (validation) -------------------------------------------------

    [Fact]
    public async Task Update_rejects_an_unknown_token_and_an_empty_body_with_400()
    {
        const EmailTemplateType type = EmailTemplateType.BadgeActivation;
        var admin = await CreateAdministratorAndSignInAsync();

        // Unknown placeholder {Foo} — a 400 that never writes an override row.
        var unknown = await UpdateRawAsync(type, admin, new UpdateEmailTemplateRequest
        {
            Subject = "X",
            BodyEn = "<p>{Foo}</p>",
            BodyAr = "<p>{Foo}</p>",
            IsActive = true,
        });
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        var unknownBody = (await unknown.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.EmailTemplateInvalid, unknownBody.Error!.Code);

        // Empty subject + bodies — same error code.
        var empty = await UpdateRawAsync(type, admin, new UpdateEmailTemplateRequest
        {
            Subject = string.Empty,
            BodyEn = string.Empty,
            BodyAr = string.Empty,
            IsActive = true,
        });
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        var emptyBody = (await empty.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.EmailTemplateInvalid, emptyBody.Error!.Code);
    }

    // -- Reset ---------------------------------------------------------------

    [Fact]
    public async Task Reset_removes_the_override_and_reverts_to_the_default()
    {
        const EmailTemplateType type = EmailTemplateType.BiometricStepUp;
        var admin = await CreateAdministratorAndSignInAsync();
        const string customSubject = "SIMF biometric (customised)";

        var saved = await UpdateAsync(type, admin, new UpdateEmailTemplateRequest
        {
            Subject = customSubject,
            BodyEn = "<p>Confirm with {Code}.</p>",
            BodyAr = "<p>أكّد بالرمز {Code}.</p>",
            IsActive = true,
        });
        Assert.True(saved.IsOverride);

        var reset = await PostAuthAsync(RouteFor(type, "reset"), EmptyBody, admin);
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);

        var afterReset = await GetDetailAsync(type, admin);
        Assert.False(afterReset.IsOverride);
        Assert.NotEqual(customSubject, afterReset.Subject);
        Assert.Equal(afterReset.DefaultSubject, afterReset.Subject);
    }

    // -- Preview -------------------------------------------------------------

    [Fact]
    public async Task Preview_substitutes_sample_values_into_the_html_body()
    {
        var admin = await CreateAdministratorAndSignInAsync();

        var response = await PostAuthAsync(
            RouteFor(EmailTemplateType.SignInOtp, "preview"),
            new PreviewEmailTemplateRequest
            {
                Subject = "Preview",
                BodyEn = "<p>{Code}</p>",
                BodyAr = "<p>{Code}</p>",
            },
            admin);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var preview = (await response.Content
            .ReadFromJsonAsync<ApiResult<EmailTemplatePreviewResult>>())!.Data!;
        // "123456" is the catalogue's sample value for the Code token.
        Assert.Contains("123456", preview.HtmlBody, StringComparison.Ordinal);
        Assert.Empty(preview.UnknownTokens);
    }

    [Fact]
    public async Task Preview_reports_unknown_tokens_in_the_supplied_copy()
    {
        var admin = await CreateAdministratorAndSignInAsync();

        var response = await PostAuthAsync(
            RouteFor(EmailTemplateType.SignInOtp, "preview"),
            new PreviewEmailTemplateRequest
            {
                Subject = "Preview",
                BodyEn = "<p>{Foo}</p>",
                BodyAr = "<p>{Code}</p>",
            },
            admin);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var preview = (await response.Content
            .ReadFromJsonAsync<ApiResult<EmailTemplatePreviewResult>>())!.Data!;
        Assert.NotEmpty(preview.UnknownTokens);
    }

    // -- Unknown type --------------------------------------------------------

    [Fact]
    public async Task Unknown_template_type_returns_404()
    {
        var admin = await CreateAdministratorAndSignInAsync();

        var response = await GetAuthAsync(
            "/api/v1/admin/email/templates/NopeNope", admin);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.EmailTemplateNotFound, body.Error!.Code);
    }

    // -- Permission gate -----------------------------------------------------

    [Fact]
    public async Task Anonymous_list_call_is_unauthorized()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/admin/email/templates/list", new GridQuery());
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Approved_non_admin_list_call_is_forbidden()
    {
        // An approved visitor authenticates but holds no EmailTemplates.View
        // permission, so the gate returns 403 (mirrors AiModuleTests).
        var visitor = await SignInApprovedVisitorAsync();

        var response = await PostAuthAsync(
            "/api/v1/admin/email/templates/list", new GridQuery(), visitor);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // -- Helpers -------------------------------------------------------------

    private static readonly object EmptyBody = new();

    private static string RouteFor(EmailTemplateType type) =>
        $"/api/v1/admin/email/templates/{type}";

    private static string RouteFor(EmailTemplateType type, string action) =>
        $"/api/v1/admin/email/templates/{type}/{action}";

    private async Task<AdminEmailTemplateDetail> GetDetailAsync(
        EmailTemplateType type, string token)
    {
        var response = await GetAuthAsync(RouteFor(type), token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminEmailTemplateDetail>>())!.Data!;
    }

    private async Task<AdminEmailTemplateDetail> UpdateAsync(
        EmailTemplateType type, string token, UpdateEmailTemplateRequest request)
    {
        var response = await UpdateRawAsync(type, token, request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminEmailTemplateDetail>>())!.Data!;
    }

    private Task<HttpResponseMessage> UpdateRawAsync(
        EmailTemplateType type, string token, UpdateEmailTemplateRequest request) =>
        PutAuthAsync(RouteFor(type), request, token);

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"email-tpl-admin-{Guid.NewGuid():N}@simf.test";
        using (var scope = _factory.Services.CreateScope())
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<SimfRole>>();
            if (!await roles.RoleExistsAsync(AdministratorRole))
            {
                await roles.CreateAsync(new SimfRole { Name = AdministratorRole });
            }
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
            var user = new SimfUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = "Email Template Admin",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, AdministratorRole);
        }

        var sign = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest
            {
                Email = email,
                Password = AuthFlow.Password,
                Audience = SignInAudience.Cp,
            });
        var body = (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        return body.Data!.Tokens!.AccessToken;
    }

    private async Task<string> SignInApprovedVisitorAsync()
    {
        var email = $"email-tpl-visitor-{Guid.NewGuid():N}@simf.test";
        await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-up",
            new SignUpRequest
            {
                Email = email,
                Password = AuthFlow.Password,
                ConfirmPassword = AuthFlow.Password,
            });
        await _client.PostAsJsonAsync(
            "/api/v1/app/auth/verify-email",
            new VerifyEmailRequest
            {
                Email = email,
                Code = AuthFlow.GetActiveCode(
                    _factory, email, AccountCodePurpose.EmailVerification),
            });
        AuthFlow.SetAccountState(_factory, email, AccountState.Approved);
        AuthFlow.DisableTwoFactor(_factory, email);

        var sign = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest { Email = email, Password = AuthFlow.Password });
        var body = (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        return body.Data!.Tokens!.AccessToken;
    }

    private Task<HttpResponseMessage> PostAuthAsync<TBody>(
        string url, TBody body, string token) where TBody : class
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> PutAuthAsync<TBody>(
        string url, TBody body, string token) where TBody : class
    {
        var request = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> GetAuthAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
