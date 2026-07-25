// D-179 (gap doc G12 hardening) — backend hardening of the AI
// module flagged by D-178 review agents:
//   (1) per-admin rate limit on /admin/ai/prompts/{id}/test
//   (2) Inputs.Count + value length cap at Test endpoint
//   (3) structured JSON audit Detail (was free-text)
//   (4) SHA-256 prompt-content hash in AiPromptUpdated audit
//   (5) input redaction at SerialiseInputs (was a comment promise)
//   (6) SOC drill-down GetInvocationDetail endpoint
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Ai;
using SIMF.Contracts.Authentication;
using SIMF.Domain.IdentityAccess;
using SIMF.Infrastructure.Ai;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class AiHardeningTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public AiHardeningTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    // -- (2) Inputs cap ------------------------------------------------------

    [Fact]
    public async Task Test_endpoint_rejects_more_than_MaxInputsCount()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var prompt = await GetSeededPromptIdAsync("assistance");

        var oversized = new Dictionary<string, string>();
        for (var i = 0; i < AiService.MaxInputsCount + 1; i++) oversized[$"k{i}"] = "v";

        var response = await PostAuthAsync(
            $"/api/v1/admin/ai/prompts/{prompt}/test",
            new TestAiPromptRequest { Inputs = oversized }, admin);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.AiInputInvalid, body.Error!.Code);
    }

    [Fact]
    public async Task Test_endpoint_rejects_value_length_over_cap()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var prompt = await GetSeededPromptIdAsync("assistance");

        var huge = new string('x', AiService.MaxInputValueLength + 1);
        var response = await PostAuthAsync(
            $"/api/v1/admin/ai/prompts/{prompt}/test",
            new TestAiPromptRequest
            {
                Inputs = new Dictionary<string, string> { ["message"] = huge },
            }, admin);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.AiInputInvalid, body.Error!.Code);
    }

    [Fact]
    public async Task Public_FAQ_endpoint_also_rejects_oversized_input()
    {
        // D-179 (review-pass) — the chokepoint is now IAiService.InvokeAsync,
        // so public feature endpoints also enforce the cap. This closes the
        // bypass reality-checker flagged: an approved visitor cannot paste a
        // megabyte question into /api/v1/app/ai/faq.
        var huge = new string('x', AiService.MaxInputValueLength + 1);
        var response = await _client.PostAsJsonAsync(
            "/api/v1/app/ai/faq", new AskFaqRequest { Question = huge });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.AiInputInvalid, body.Error!.Code);
    }

    // -- (5) Input redaction -------------------------------------------------

    [Fact]
    public void SerialiseAndRedact_masks_email_jwt_apikey_and_pan()
    {
        // Real JWT shape: base64(header).base64(payload).base64(signature)
        // where header + payload start with "eyJ" (base64 of "{") but
        // signature is opaque bytes — does NOT start with eyJ.
        var inputs = new Dictionary<string, string>
        {
            ["email"] = "captain@simf.test",
            ["jwt"] = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9"
                + ".eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4ifQ"
                + ".SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c",
            ["openai"] = "sk-abcdefghijklmnopqrstuvwxyz12345",
            ["pan"] = "4242 4242 4242 4242",
        };
        var json = AiAuditDetail.SerialiseAndRedact(inputs);
        Assert.Contains("[REDACTED_EMAIL]", json);
        Assert.Contains("[REDACTED_JWT]", json);
        Assert.Contains("[REDACTED_KEY]", json);
        Assert.Contains("[REDACTED_PAN]", json);
        Assert.DoesNotContain("captain@simf.test", json);
        Assert.DoesNotContain("4242 4242 4242 4242", json);
        Assert.DoesNotContain("sk-abcdefghijklmnop", json);
        Assert.DoesNotContain("SflKxwRJSMeKKF2QT4", json);
    }

    [Fact]
    public async Task FAQ_endpoint_persists_redacted_input_to_invocation_row()
    {
        var pii = "Reach me at captain.ahmed@simf.test";
        var response = await _client.PostAsJsonAsync(
            "/api/v1/app/ai/faq", new AskFaqRequest { Question = pii });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<AiCallResult>>())!.Data!;

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var invocation = await db.AiInvocations.AsNoTracking()
            .SingleAsync(i => i.Id == body.InvocationId);
        Assert.Contains("[REDACTED_EMAIL]", invocation.InputJson);
        Assert.DoesNotContain("captain.ahmed@simf.test", invocation.InputJson);
    }

    // -- (3) + (4) Structured JSON audit + prompt-content hash ---------------

    [Fact]
    public async Task AiPromptUpdated_audit_carries_old_and_new_content_hash()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var (id, originalSystem, originalUser) =
            await GetSeededPromptAsync("assistance");

        // First update — change SystemPrompt; hash should change.
        var update = await PutAuthAsync(
            $"/api/v1/admin/ai/prompts/{id}",
            new UpdateAiPromptRequest
            {
                Feature = AiFeature.Assistance,
                DisplayName = "Visitor Concierge",
                DisplayNameArabic = "خدمة الزوّار",
                Provider = AiProvider.Echo,
                Model = "echo",
                SystemPrompt = originalSystem + " (D-179 change)",
                UserPromptTemplate = originalUser,
                Temperature = 0.2,
                MaxOutputTokens = 512,
                IsActive = true,
            }, admin);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        // D-179 (review-pass) — filter by promptId so a parallel test
        // editing a different prompt cannot win the race.
        var auditRow = await db.OperationLog.AsNoTracking()
            .Where(e => e.EventType == "AiPrompt.Updated"
                && e.Detail!.Contains(id.ToString()))
            .OrderByDescending(e => e.Timestamp)
            .FirstAsync();

        Assert.NotNull(auditRow.Detail);
        var detail = JsonDocument.Parse(auditRow.Detail!);
        Assert.True(detail.RootElement.TryGetProperty("contentHashOld", out var oldHash));
        Assert.True(detail.RootElement.TryGetProperty("contentHashNew", out var newHash));
        Assert.True(detail.RootElement.TryGetProperty("contentChanged", out var changed));
        Assert.NotEqual(oldHash.GetString(), newHash.GetString());
        Assert.True(changed.GetBoolean());
    }

    [Fact]
    public void PromptContentHash_treats_field_swap_as_different()
    {
        // D-179 (review-pass) — code-reviewer's regression test: without
        // a proper field separator, ("AB", "C") collides with ("A", "BC").
        // This test pins the 0x1F () unit-separator semantics.
        var a = AiAuditDetail.PromptContentHash("AB", "C");
        var b = AiAuditDetail.PromptContentHash("A", "BC");
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void PromptContentHash_is_deterministic_and_v1_prefixed()
    {
        // D-181 — HMAC-SHA256 over the same input MUST produce the same
        // hash within one process. SOC drift detection depends on this:
        // an Updated audit row's `contentHashOld` is the previous Created
        // / Updated row's `contentHashNew`, and SIEM joins on equality.
        // The `v1:` prefix exists for future key-rotation versioning.
        var h1 = AiAuditDetail.PromptContentHash("Hello", "World");
        var h2 = AiAuditDetail.PromptContentHash("Hello", "World");
        Assert.Equal(h1, h2);
        Assert.StartsWith("v1:", h1);
        Assert.Equal(3 + 64, h1.Length); // "v1:" + 64-char hex
        Assert.Matches("^v1:[0-9a-f]{64}$", h1);
    }

    [Fact]
    public void PromptContentHash_known_answer_under_a_fixed_key()
    {
        // D-181 (review-pass, code-reviewer suggestion) — known-answer
        // test pins both the HMAC algorithm AND the input concatenation
        // formula. A future swap (e.g. HMAC-SHA1, SHA3-256, different
        // delimiter) breaks this without breaking the determinism +
        // length tests above. Snapshot the test-fixture key + hash.
        AiAuditDetail.ConfigureHmacKey("test-key-for-known-answer-d181");
        try
        {
            var hash = AiAuditDetail.PromptContentHash("Hello", "World");
            // The pinned hex below was produced by the implementation at
            // commit time with HMAC-SHA256(test-key-for-known-answer-d181,
            // "HelloWorld"). If this changes, the algorithm OR the
            // delimiter changed — either is a load-bearing regression.
            Assert.Equal(
                "v1:e9ef841849a7e4da5ee311f445f5f9cd"
                + "5cecea118702d7af557703e2274e4214",
                hash);
        }
        finally
        {
            // Restore the test fixture's key (the rest of the suite
            // relies on the SimfApiFactory-installed key).
            // SimfApiFactory.EnsureDatabaseCreated re-registers DI,
            // but explicit reset keeps subsequent same-class tests
            // independent.
            AiAuditDetail.ConfigureHmacKey(null);
        }
    }

    [Theory]
    [InlineData("My ID is 1098765432.", "[REDACTED_NID]", "1098765432")]
    [InlineData("Call me on 0501234567 please", "[REDACTED_PHONE]", "0501234567")]
    [InlineData("Wire to SA0380000000608010167519", "[REDACTED_IBAN]", "SA0380000000608010167519")]
    [InlineData("AKIAIOSFODNN7EXAMPLE leaked", "[REDACTED_KEY]", "AKIAIOSFODNN7EXAMPLE")]
    [InlineData("ghp_abcdefghijklmnopqrstuvwxyz0123456789", "[REDACTED_KEY]", "ghp_abcdefghijklmnopqrstuvwxyz0123456789")]
    [InlineData("AIzaSyDxabcdefghijklmnopqrstuvwxyz12345", "[REDACTED_KEY]", "AIzaSyDxabcdefghijklmnopqrstuvwxyz12345")]
    [InlineData("xoxb-12345-abcdefghij-klmnopqrstuv", "[REDACTED_KEY]", "xoxb-12345-abcdefghij-klmnopqrstuv")]
    [InlineData("-----BEGIN RSA PRIVATE KEY-----\nMIIE", "[REDACTED_PEM]", "BEGIN RSA PRIVATE KEY")]
    public void RedactValue_masks_the_new_D181_patterns(
        string input, string expectedMarker, string mustNotAppear)
    {
        // D-181 — new redaction patterns: Saudi NID + mobile + IBAN,
        // AWS AKIA/ASIA, GitHub PATs, Google AIza, Slack xox, PEM
        // private-key block. Each test row asserts both the
        // redaction marker AND absence of the raw secret prefix so a
        // half-match regex regression still fails.
        var redacted = AiAuditDetail.RedactValue(input);
        Assert.Contains(expectedMarker, redacted);
        Assert.DoesNotContain(mustNotAppear, redacted);
    }

    [Theory]
    // D-188 — AWS secret-key context redaction. The value has no
    // prefix so the high-fidelity signal is the surrounding context
    // (`aws_secret_access_key=...`). These rows cover the common
    // config-file and env-var shapes.
    [InlineData("aws_secret_access_key=wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY")]
    [InlineData("AWS_SECRET_ACCESS_KEY=wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY")]
    [InlineData("aws_secret_access_key: wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY")]
    [InlineData("aws-secret-access-key=wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY")]
    public void RedactValue_masks_aws_secret_access_key_by_context(string input)
    {
        // The whole prefix=value span is redacted (not just the
        // value) so the SOC pipeline cannot reconstruct either half.
        var redacted = AiAuditDetail.RedactValue(input);
        Assert.Contains("[REDACTED_KEY]", redacted);
        Assert.DoesNotContain("wJalrXUtnFEMI", redacted);
        Assert.DoesNotContain("aws_secret_access_key", redacted,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("aws-secret-access-key", redacted,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RedactValue_does_not_panic_or_hang_on_adversarial_input()
    {
        // D-181 — NonBacktracking guarantees O(n) — a 50KB string of
        // digits (worst case for the PAN regex's bounded backtracker)
        // should run in milliseconds, not seconds.
        // D-181 (review-pass, test analyst) — warm the regexes via a
        // tiny prior call so first-run JIT + regex-compile costs don't
        // count against the 1s budget on a cold CI agent.
        AiAuditDetail.RedactValue("warmup");
        var adversarial = new string('9', 50_000);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var redacted = AiAuditDetail.RedactValue(adversarial);
        sw.Stop();
        Assert.True(sw.ElapsedMilliseconds < 1000,
            $"RedactValue took {sw.ElapsedMilliseconds}ms on 50KB digit run — NonBacktracking should keep this sub-second.");
        Assert.NotNull(redacted);
    }

    [Fact]
    public async Task AiPromptCreated_audit_carries_content_hash()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var key = $"hash-{Guid.NewGuid().ToString("N")[..8]}";
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
                SystemPrompt = "Be helpful.",
                UserPromptTemplate = "{message}",
                Temperature = 0.1,
                MaxOutputTokens = 100,
            }, admin);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var auditRow = await db.OperationLog.AsNoTracking()
            .Where(e => e.EventType == "AiPrompt.Created"
                && e.Detail!.Contains(key))
            .OrderByDescending(e => e.Timestamp)
            .FirstAsync();

        var detail = JsonDocument.Parse(auditRow.Detail!);
        Assert.True(detail.RootElement.TryGetProperty("contentHash", out var hash));
        // D-181 (review-pass) — hash now carries a `v1:` version prefix
        // so a future HMAC key rotation can bump to `v2:` and SOC
        // rules can explicitly skip cross-version drift compares.
        var hashStr = hash.GetString()!;
        Assert.StartsWith("v1:", hashStr);
        Assert.Equal(3 + 64, hashStr.Length); // "v1:" + 64-char hex
        Assert.Matches("^v1:[0-9a-f]{64}$", hashStr);
    }

    [Fact]
    public async Task AiInvocationSucceeded_audit_is_valid_json()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/app/ai/faq", new AskFaqRequest { Question = "Q" });
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<AiCallResult>>())!.Data!;

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var auditRow = await db.OperationLog.AsNoTracking()
            .Where(e => e.EventType == "AiInvocation.Succeeded"
                && e.Detail!.Contains(body.InvocationId.ToString()))
            .FirstAsync();

        var detail = JsonDocument.Parse(auditRow.Detail!);
        Assert.Equal("faq-answer",
            detail.RootElement.GetProperty("promptKey").GetString());
        Assert.Equal("Faq",
            detail.RootElement.GetProperty("feature").GetString());
        Assert.Equal("Echo",
            detail.RootElement.GetProperty("provider").GetString());
    }

    // -- (6) GetInvocationDetail endpoint ------------------------------------

    [Fact]
    public async Task Admin_can_drill_down_to_invocation_detail()
    {
        // Trigger an invocation we can look up.
        var response = await _client.PostAsJsonAsync(
            "/api/v1/app/ai/faq", new AskFaqRequest { Question = "Drill-down test" });
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<AiCallResult>>())!.Data!;

        var admin = await CreateAdministratorAndSignInAsync();
        var detail = await GetAuthAsync(
            $"/api/v1/admin/ai/invocations/{body.InvocationId}", admin);
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        var payload = (await detail.Content
            .ReadFromJsonAsync<ApiResult<AdminAiInvocationDetail>>())!.Data!;
        Assert.Equal(body.InvocationId, payload.Id);
        Assert.Contains("Drill-down test", payload.InputJson);
        Assert.NotNull(payload.OutputText);
    }

    [Fact]
    public async Task GetInvocationDetail_returns_404_for_unknown_id()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var detail = await GetAuthAsync(
            $"/api/v1/admin/ai/invocations/{Guid.NewGuid()}", admin);
        Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode);
        var body = (await detail.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.NotFound, body.Error!.Code);
    }

    // -- Helpers --------------------------------------------------------------

    private async Task<Guid> GetSeededPromptIdAsync(string key)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        return await db.AiPrompts.AsNoTracking()
            .Where(p => p.Key == key).Select(p => p.Id).SingleAsync();
    }

    private async Task<(Guid Id, string SystemPrompt, string UserPromptTemplate)>
        GetSeededPromptAsync(string key)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var p = await db.AiPrompts.AsNoTracking()
            .SingleAsync(p => p.Key == key);
        return (p.Id, p.SystemPrompt, p.UserPromptTemplate);
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"ai-hardening-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "AI Hardening Admin",
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

    // -- D-188 — AiPromptHistory snapshot tests ---------------------------------

    [Fact]
    public async Task Update_writes_an_AiPromptHistory_snapshot_capturing_the_prior_state()
    {
        // D-188: every successful Update snapshots the PRE-mutation
        // state into AiPromptHistory before the version bump. The
        // snapshot's Version equals the live version that was about
        // to be replaced, and the snapshot's content matches what
        // the live row carried at that moment.
        var admin = await CreateAdministratorAndSignInAsync();
        var (id, originalSystem, originalUser) =
            await GetSeededPromptAsync("translate");

        var update = await PutAuthAsync(
            $"/api/v1/admin/ai/prompts/{id}",
            new UpdateAiPromptRequest
            {
                Feature = AiFeature.Translate,
                DisplayName = "Translate",
                DisplayNameArabic = "ترجمة",
                Provider = AiProvider.Echo,
                Model = "echo",
                SystemPrompt = originalSystem + " (D-188 change)",
                UserPromptTemplate = originalUser,
                Temperature = 0.1,
                MaxOutputTokens = 256,
                IsActive = true,
            }, admin);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var snapshot = await db.AiPromptHistory.AsNoTracking()
            .Where(h => h.AiPromptId == id)
            .OrderByDescending(h => h.Version)
            .FirstOrDefaultAsync();
        Assert.NotNull(snapshot);
        // The snapshot's Version is the PRE-bump value (i.e. the
        // live row was at this version when the snapshot ran).
        Assert.Equal(1, snapshot!.Version);
        Assert.Equal(originalSystem, snapshot.SystemPrompt);
        Assert.Equal(originalUser, snapshot.UserPromptTemplate);
        Assert.StartsWith("v1:", snapshot.ContentHash);
    }

    [Fact]
    public async Task Get_history_endpoint_returns_snapshots_newest_first()
    {
        // D-188: GET /admin/ai/prompts/{id}/history returns the
        // append-only snapshot list ordered by Version descending.
        var admin = await CreateAdministratorAndSignInAsync();
        var (id, originalSystem, originalUser) =
            await GetSeededPromptAsync("faq-answer");

        // Two updates -> two snapshot rows (v1 then v2).
        for (var i = 1; i <= 2; i++)
        {
            await PutAuthAsync(
                $"/api/v1/admin/ai/prompts/{id}",
                new UpdateAiPromptRequest
                {
                    Feature = AiFeature.Faq,
                    DisplayName = "FAQ",
                    DisplayNameArabic = "أسئلة",
                    Provider = AiProvider.Echo,
                    Model = "echo",
                    SystemPrompt = $"{originalSystem} v{i + 1}",
                    UserPromptTemplate = originalUser,
                    Temperature = 0.2,
                    MaxOutputTokens = 256,
                    IsActive = true,
                }, admin);
        }

        var response = await GetAuthAsync(
            $"/api/v1/admin/ai/prompts/{id}/history", admin);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var history = (await response.Content
            .ReadFromJsonAsync<ApiResult<List<AdminAiPromptHistoryEntry>>>())!.Data!;
        Assert.Equal(2, history.Count);
        Assert.Equal(2, history[0].Version);  // newest first
        Assert.Equal(1, history[1].Version);
    }
}
