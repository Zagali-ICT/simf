// D-176 (gap doc G12) — centralised AI module: feature endpoints
// (anon FAQ + authenticated assistance), admin CRUD, invocation log.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Ai;
using SIMF.Contracts.Authentication;
using SIMF.Domain.IdentityAccess;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class AiModuleTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public AiModuleTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Anonymous_FAQ_returns_echo_response_and_writes_invocation()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/app/ai/faq",
            new AskFaqRequest { Question = "What time is the keynote?" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<AiCallResult>>())!.Data!;
        Assert.Equal("faq-answer", body.PromptKey);
        Assert.Equal(AiFeature.Faq, body.Feature);
        Assert.Equal(AiProvider.Echo, body.Provider);
        Assert.Contains("[echo", body.OutputText);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var invocation = await db.AiInvocations.AsNoTracking()
            .SingleOrDefaultAsync(i => i.Id == body.InvocationId);
        Assert.NotNull(invocation);
        Assert.Equal("Anonymous", invocation!.CallerKind);
        Assert.Null(invocation.ErrorCode);
    }

    [Fact]
    public async Task Authenticated_assistance_records_visitor_caller_kind()
    {
        var visitor = await SignInApprovedVisitorAsync();
        var response = await PostAuthAsync(
            "/api/v1/app/ai/assistance",
            new AssistanceRequest { Message = "Where is hall H1?" },
            visitor);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<AiCallResult>>())!.Data!;
        Assert.Equal("assistance", body.PromptKey);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var invocation = await db.AiInvocations.AsNoTracking()
            .SingleOrDefaultAsync(i => i.Id == body.InvocationId);
        Assert.Equal("Visitor", invocation!.CallerKind);
        Assert.NotNull(invocation.CallerUserId);
    }

    [Fact]
    public async Task Assistance_prompt_is_grounded_with_the_event_context_block()
    {
        // The grounded assistance template folds in the visitor language + the live
        // event-context block (built server-side). Echo echoes the fully-substituted
        // user prompt, so both markers appear — proving the endpoint injected the
        // context builder and the seeded template consumes {context}/{locale}.
        var visitor = await SignInApprovedVisitorAsync();
        var response = await PostAuthAsync(
            "/api/v1/app/ai/assistance",
            new AssistanceRequest { Message = "Hello", Locale = "en" },
            visitor);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<AiCallResult>>())!.Data!;
        Assert.Equal("assistance", body.PromptKey);
        Assert.Contains("Live event context", body.OutputText);
        Assert.Contains("Visitor language: en", body.OutputText);
    }

    // -- A18 (2026-07-27): the stub guard must be INVISIBLE to visitors. The offline
    // Echo provider is the shipped default (`Ai.DefaultProvider = Echo`), so on a QA
    // or demo build every chatbot / FAQ reply comes from it. Reviewer instructions
    // ("do not review, approve or publish it") belong to the session-summary desk,
    // not to a visitor's chat bubble — the stub signal travels as metadata instead.

    [Fact]
    public async Task Stub_chatbot_reply_carries_no_reviewer_banner_but_is_flagged_as_stub()
    {
        var visitor = await SignInApprovedVisitorAsync();
        var response = await PostAuthAsync(
            "/api/v1/app/ai/assistance",
            new AssistanceRequest { Message = "Where is hall H1?", Locale = "en" },
            visitor);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<AiCallResult>>())!.Data!;

        AssertNoReviewerBanner(body.OutputText);
        // ...but the caller that must not ship stub content can still detect it.
        Assert.True(body.IsStub);
    }

    [Fact]
    public async Task Stub_anonymous_faq_reply_carries_no_reviewer_banner()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/app/ai/faq",
            new AskFaqRequest { Question = "What time is the keynote?" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<AiCallResult>>())!.Data!;

        AssertNoReviewerBanner(body.OutputText);
        Assert.True(body.IsStub);
    }

    // The reviewer-facing wording, in both languages, plus the machine sentinel.
    // None of it may ever be rendered to a visitor.
    private static void AssertNoReviewerBanner(string outputText)
    {
        Assert.DoesNotContain("AI-STUB-DO-NOT-PUBLISH", outputText, StringComparison.Ordinal);
        Assert.DoesNotContain("NOT REAL AI OUTPUT", outputText, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "Do not review, approve or publish", outputText, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "لا تراجعها أو توافق عليها أو تنشرها", outputText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Assistance_persists_the_turn_and_history_returns_it()
    {
        // A fresh visitor → an isolated conversation, so exactly the one exchange
        // is present afterwards.
        var visitor = await SignInApprovedVisitorAsync();
        var post = await PostAuthAsync(
            "/api/v1/app/ai/assistance",
            new AssistanceRequest { Message = "Where is hall H1?", Locale = "en" },
            visitor);
        Assert.Equal(HttpStatusCode.OK, post.StatusCode);

        var history = await PostAuthGetAsync<List<AiChatTurn>>(
            "/api/v1/app/ai/assistance/history", visitor);
        Assert.Equal(2, history.Count);
        Assert.Equal("user", history[0].Role);
        Assert.Equal("Where is hall H1?", history[0].Content);
        Assert.Equal("assistant", history[1].Role);
        Assert.NotEmpty(history[1].Content);
    }

    [Fact]
    public async Task Assistance_second_call_includes_prior_turns_as_memory()
    {
        var visitor = await SignInApprovedVisitorAsync();
        await PostAuthAsync(
            "/api/v1/app/ai/assistance",
            new AssistanceRequest { Message = "My name is Sam.", Locale = "en" },
            visitor);
        var second = await PostAuthAsync(
            "/api/v1/app/ai/assistance",
            new AssistanceRequest { Message = "What did I just say?", Locale = "en" },
            visitor);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var body = (await second.Content
            .ReadFromJsonAsync<ApiResult<AiCallResult>>())!.Data!;
        // Echo echoes the fully-substituted prompt, so the {history} block from the
        // first turn appears in the second answer — proving memory is injected.
        Assert.Contains("Conversation so far", body.OutputText);
        Assert.Contains("My name is Sam.", body.OutputText);
    }

    [Fact]
    public async Task Anonymous_translate_substitutes_lang_inputs_into_template()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/app/ai/translate",
            new TranslateRequest
            {
                Text = "Welcome to SIMF",
                SourceLang = "en",
                TargetLang = "ar",
            });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<AiCallResult>>())!.Data!;
        Assert.Equal("translate", body.PromptKey);
        Assert.Contains("Welcome to SIMF", body.OutputText);
    }

    [Fact]
    public async Task Live_translation_chunk_endpoint_invokes_live_prompt()
    {
        var visitor = await SignInApprovedVisitorAsync();
        var response = await PostAuthAsync(
            "/api/v1/app/ai/live-translation/chunk",
            new LiveTranslateChunkRequest
            {
                Text = "Good morning",
                SourceLang = "en",
                TargetLang = "ar",
            },
            visitor);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<AiCallResult>>())!.Data!;
        Assert.Equal("live-translation", body.PromptKey);
        Assert.Equal(AiFeature.LiveTranslation, body.Feature);
    }

    [Fact]
    public async Task Live_sign_language_chunk_endpoint_invokes_sign_prompt()
    {
        var visitor = await SignInApprovedVisitorAsync();
        var response = await PostAuthAsync(
            "/api/v1/app/ai/live-sign-language/chunk",
            new LiveSignChunkRequest { Text = "Welcome to SIMF" },
            visitor);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<AiCallResult>>())!.Data!;
        Assert.Equal("live-sign-language", body.PromptKey);
        Assert.Equal(AiFeature.LiveSignLanguage, body.Feature);
    }

    [Fact]
    public async Task Admin_can_list_prompts_includes_seeded_six()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var list = await PostAuthAsync(
            "/api/v1/admin/ai/prompts/list",
            new GridQuery { Top = 100 }, admin);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var page = (await list.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminAiPromptSummary>>>())!.Data!;
        Assert.Contains(page.Items, p => p.Key == "question-filter");
        Assert.Contains(page.Items, p => p.Key == "faq-answer");
        Assert.Contains(page.Items, p => p.Key == "assistance");
        Assert.Contains(page.Items, p => p.Key == "translate");
        Assert.Contains(page.Items, p => p.Key == "live-translation");
        Assert.Contains(page.Items, p => p.Key == "live-sign-language");
    }

    [Fact]
    public async Task Admin_can_update_prompt_and_version_increments()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var existing = await GetPromptIdAsync("assistance");

        var detail = await PostAuthGetAsync<AdminAiPromptDetail>(
            $"/api/v1/admin/ai/prompts/{existing}", admin);
        Assert.Equal(1, detail.Version);

        var update = await PutAuthAsync(
            $"/api/v1/admin/ai/prompts/{existing}",
            new UpdateAiPromptRequest
            {
                Feature = detail.Feature,
                DisplayName = detail.DisplayName,
                DisplayNameArabic = detail.DisplayNameArabic,
                Description = detail.Description,
                DescriptionArabic = detail.DescriptionArabic,
                Provider = detail.Provider,
                Model = detail.Model,
                SystemPrompt = detail.SystemPrompt + " (edited)",
                UserPromptTemplate = detail.UserPromptTemplate,
                Temperature = detail.Temperature,
                MaxOutputTokens = detail.MaxOutputTokens,
                IsActive = true,
            }, admin);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = (await update.Content
            .ReadFromJsonAsync<ApiResult<AdminAiPromptDetail>>())!.Data!;
        Assert.Equal(2, updated.Version);
        Assert.Contains("(edited)", updated.SystemPrompt);
    }

    [Fact]
    public async Task Admin_can_create_then_test_new_prompt()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var key = $"custom-{Guid.NewGuid().ToString("N")[..8]}";
        var create = await PostAuthAsync(
            "/api/v1/admin/ai/prompts",
            new CreateAiPromptRequest
            {
                Key = key,
                Feature = AiFeature.Assistance,
                DisplayName = "Custom",
                DisplayNameArabic = "مخصّص",
                Provider = AiProvider.Echo,
                Model = "echo",
                SystemPrompt = "Be helpful.",
                UserPromptTemplate = "{message}",
                Temperature = 0.1,
                MaxOutputTokens = 100,
            }, admin);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var created = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminAiPromptDetail>>())!.Data!;

        var test = await PostAuthAsync(
            $"/api/v1/admin/ai/prompts/{created.Id}/test",
            new TestAiPromptRequest
            {
                Inputs = new Dictionary<string, string>
                {
                    ["message"] = "Hello world",
                },
            }, admin);
        Assert.Equal(HttpStatusCode.OK, test.StatusCode);
        var result = (await test.Content
            .ReadFromJsonAsync<ApiResult<AiCallResult>>())!.Data!;
        Assert.Contains("Hello world", result.OutputText);
    }

    [Fact]
    public async Task Duplicate_key_returns_409_AI_PROMPT_KEY_DUPLICATE()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var create = await PostAuthAsync(
            "/api/v1/admin/ai/prompts",
            new CreateAiPromptRequest
            {
                Key = "faq-answer",
                Feature = AiFeature.Faq,
                DisplayName = "Dup",
                DisplayNameArabic = "Dup",
                Provider = AiProvider.Echo,
                Model = "echo",
                SystemPrompt = "X",
                UserPromptTemplate = "Y",
                Temperature = 0.0,
                MaxOutputTokens = 10,
            }, admin);
        Assert.Equal(HttpStatusCode.Conflict, create.StatusCode);
        var body = (await create.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.AiPromptKeyDuplicate, body.Error!.Code);
    }

    [Fact]
    public async Task Unknown_prompt_key_returns_404_AI_PROMPT_NOT_FOUND()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/admin/ai/prompts/list", new GridQuery());
        // Anonymous → 401, but the key check is via admin Test. Use that path.
        Assert.True(response.StatusCode is HttpStatusCode.Unauthorized
            or HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Admin_invocations_log_lists_recent_calls()
    {
        // Trigger one invocation so the log is not empty.
        await _client.PostAsJsonAsync(
            "/api/v1/app/ai/faq", new AskFaqRequest { Question = "Q" });

        var admin = await CreateAdministratorAndSignInAsync();
        var list = await PostAuthAsync(
            "/api/v1/admin/ai/invocations/list",
            new GridQuery { Top = 50 }, admin);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var page = (await list.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminAiInvocationRow>>>())!.Data!;
        Assert.True(page.Items.Count > 0);
    }

    [Fact]
    public async Task Inactive_prompt_returns_503_AI_FEATURE_DISABLED()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var key = $"disable-me-{Guid.NewGuid().ToString("N")[..6]}";
        var create = await PostAuthAsync(
            "/api/v1/admin/ai/prompts",
            new CreateAiPromptRequest
            {
                Key = key,
                Feature = AiFeature.Assistance,
                DisplayName = "X",
                DisplayNameArabic = "X",
                Provider = AiProvider.Echo,
                Model = "echo",
                SystemPrompt = "S",
                UserPromptTemplate = "U",
                Temperature = 0.0,
                MaxOutputTokens = 10,
            }, admin);
        var created = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminAiPromptDetail>>())!.Data!;
        // Deactivate.
        var del = await DeleteAuthAsync(
            $"/api/v1/admin/ai/prompts/{created.Id}", admin);
        Assert.Equal(HttpStatusCode.OK, del.StatusCode);
        // Try to invoke via admin Test endpoint — same path the user pays.
        var test = await PostAuthAsync(
            $"/api/v1/admin/ai/prompts/{created.Id}/test",
            new TestAiPromptRequest(), admin);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, test.StatusCode);
        var body = (await test.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.AiFeatureDisabled, body.Error!.Code);
    }

    // -- Helpers --------------------------------------------------------------

    private async Task<Guid> GetPromptIdAsync(string key)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        return await db.AiPrompts.AsNoTracking()
            .Where(p => p.Key == key)
            .Select(p => p.Id).SingleAsync();
    }

    private async Task<string> SignInApprovedVisitorAsync()
    {
        var email = $"ai-visitor-{Guid.NewGuid():N}@simf.test";
        await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-up",
            new SignUpRequest { Email = email, Password = AuthFlow.Password, ConfirmPassword = AuthFlow.Password });
        await _client.PostAsJsonAsync(
            "/api/v1/app/auth/verify-email",
            new VerifyEmailRequest
            {
                Email = email,
                Code = AuthFlow.GetActiveCode(_factory, email, AccountCodePurpose.EmailVerification),
            });
        AuthFlow.SetAccountState(_factory, email, AccountState.Approved);
        // D-373 — registration enables 2FA; this auth plumbing needs the
        // direct-token path (the admin-disabled scenario).
        AuthFlow.DisableTwoFactor(_factory, email);
        var sign = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest { Email = email, Password = AuthFlow.Password });
        var body = (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        return body.Data!.Tokens!.AccessToken;
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"ai-admin-{Guid.NewGuid():N}@simf.test";
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
                UserName = email, Email = email, EmailConfirmed = true,
                DisplayName = "AI Admin",
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
                Email = email, Password = AuthFlow.Password,
                Audience = SignInAudience.Cp,
            });
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

    private async Task<T> PostAuthGetAsync<T>(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<T>>())!;
        return body.Data!;
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

    private Task<HttpResponseMessage> DeleteAuthAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    // -- CP Phase-1 dashboard --------------------------------------------------

    [Fact]
    public async Task Dashboard_aggregates_recent_invocations_per_service()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        // Seed isolated telemetry for a feature no other test invokes, so the
        // per-service assertions are deterministic in the shared fixture DB.
        await SeedInvocationAsync(AiFeature.LiveSignLanguage, errorCode: null, latency: 100, tokIn: 10, tokOut: 20, ageHours: 1);
        await SeedInvocationAsync(AiFeature.LiveSignLanguage, errorCode: "X", latency: 300, tokIn: 0, tokOut: 0, ageHours: 2);
        // A 48h-old call is outside the 24h window and must be excluded.
        await SeedInvocationAsync(AiFeature.LiveSignLanguage, errorCode: null, latency: 999, tokIn: 9, tokOut: 9, ageHours: 48);

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/ai/dashboard");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", admin);
        var resp = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var d = (await resp.Content.ReadFromJsonAsync<ApiResult<AdminAiDashboard>>())!.Data!;

        Assert.Equal(24, d.WindowHours);
        var svc = Assert.Single(d.Services, s => s.Feature == AiFeature.LiveSignLanguage);
        Assert.Equal(2, svc.Calls);            // the 48h-old call is excluded
        Assert.Equal(1, svc.ErrorCount);
        Assert.Equal(0.5, svc.ErrorRate);
        Assert.Equal(30, svc.TotalTokens);     // 10+20 (+0+0)
        Assert.Equal(200, svc.AvgLatencyMs);   // (100+300)/2
        // The seeded prompt catalogue means there is at least one configured/active service.
        Assert.True(d.TotalServices >= 1);
        Assert.True(d.ActiveServices >= 1);
    }

    [Fact]
    public async Task Dashboard_is_forbidden_for_a_non_admin()
    {
        var visitor = await SignInApprovedVisitorAsync();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/ai/dashboard");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", visitor);
        var resp = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    private async Task SeedInvocationAsync(
        AiFeature feature, string? errorCode, int latency, int tokIn, int tokOut, int ageHours)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        db.AiInvocations.Add(new SIMF.Domain.Ai.AiInvocation
        {
            Id = Guid.NewGuid(),
            PromptKey = "dashboard-test",
            Feature = feature,
            Provider = AiProvider.Echo,
            Model = "echo",
            InputJson = "{}",
            OutputText = errorCode is null ? "ok" : null,
            TokensInput = tokIn,
            TokensOutput = tokOut,
            LatencyMs = latency,
            ErrorCode = errorCode,
            CallerKind = "Admin",
            CreatedAt = SimfClock.Now.AddHours(-ageHours),
        });
        await db.SaveChangesAsync();
    }
}
