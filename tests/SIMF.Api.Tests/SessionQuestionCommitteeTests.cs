// P3.3 — D-212/D-234 (Completion Programme §5.3): the Scientific-Committee
// central Q&A queue (stage 2). Covers the queue, approve→moderator-desk,
// hide→off-desk, escalate-to-a-role, the empty-role guard, and the
// non-committee rejection.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Sessions;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Programme;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class SessionQuestionCommitteeTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public SessionQuestionCommitteeTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Pending_question_appears_in_the_committee_queue()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var session = await SeedPreSessionAsync();
        var qid = await SubmitQuestionAsync(session.Id);

        var queue = await ListQueueAsync(admin, session.Id);
        Assert.Contains(queue, q => q.Id == qid && q.Status == QuestionStatus.Pending);
    }

    [Fact]
    public async Task Approve_moves_it_off_the_queue_and_onto_the_moderator_desk()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var session = await SeedPreSessionAsync();
        var qid = await SubmitQuestionAsync(session.Id);

        var approve = await PutAuthAsync($"/api/v1/admin/questions/{qid}/approve", new { }, admin);
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);

        var pendingQueue = await ListQueueAsync(admin, session.Id);
        Assert.DoesNotContain(pendingQueue, q => q.Id == qid);

        var desk = await GetAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/questions/moderate", admin);
        var rows = (await desk.Content
            .ReadFromJsonAsync<ApiResult<IReadOnlyList<SessionQuestionModeratorRow>>>())!.Data!;
        Assert.Contains(rows, r => r.Id == qid && r.Status == QuestionStatus.Approved);
    }

    [Fact]
    public async Task Hide_keeps_it_off_the_moderator_desk()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var session = await SeedPreSessionAsync();
        var qid = await SubmitQuestionAsync(session.Id);

        await PutAuthAsync($"/api/v1/admin/questions/{qid}/hide", new { }, admin);

        var desk = await GetAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/questions/moderate", admin);
        var rows = (await desk.Content
            .ReadFromJsonAsync<ApiResult<IReadOnlyList<SessionQuestionModeratorRow>>>())!.Data!;
        Assert.DoesNotContain(rows, r => r.Id == qid);

        // It is recoverable through the queue's Hidden filter.
        var hidden = await ListQueueAsync(admin, session.Id, QuestionStatus.Hidden);
        Assert.Contains(hidden, q => q.Id == qid);
    }

    [Fact]
    public async Task Re_approving_a_hidden_question_clears_the_hidden_marker()
    {
        // Regression (review finding): with IsHidden derived from Status, a
        // hidden→approved round-trip must surface as Approved + not-hidden, not a
        // stale Hidden marker on an otherwise-approved row.
        var admin = await CreateAdministratorAndSignInAsync();
        var session = await SeedPreSessionAsync();
        var qid = await SubmitQuestionAsync(session.Id);

        await PutAuthAsync($"/api/v1/admin/questions/{qid}/hide", new { }, admin);
        await PutAuthAsync($"/api/v1/admin/questions/{qid}/approve", new { }, admin);

        var desk = await GetAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/questions/moderate", admin);
        var rows = (await desk.Content
            .ReadFromJsonAsync<ApiResult<IReadOnlyList<SessionQuestionModeratorRow>>>())!.Data!;
        Assert.Contains(rows, r =>
            r.Id == qid && r.Status == QuestionStatus.Approved && !r.IsHidden);
    }

    [Fact]
    public async Task Committee_hide_of_a_pushed_question_clears_the_on_stage_flag()
    {
        // Regression (Round-1 audit #11): a question pushed on stage then retracted
        // via the Committee 'hide' surface must not keep IsPushed=true, or it
        // resurfaces on the moderator desk as already-on-stage the moment the
        // Committee re-approves it — without any fresh push after the retraction.
        var admin = await CreateAdministratorAndSignInAsync();
        var session = await SeedPreSessionAsync();
        var qid = await SubmitQuestionAsync(session.Id);

        await PutAuthAsync($"/api/v1/admin/questions/{qid}/approve", new { }, admin);
        var push = await PutAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/questions/{qid}/push", new { }, admin);
        Assert.Equal(HttpStatusCode.OK, push.StatusCode);
        var pushed = (await push.Content
            .ReadFromJsonAsync<ApiResult<SessionQuestionModeratorRow>>())!.Data!;
        Assert.True(pushed.IsPushed);

        // Committee retracts (hide) then re-approves the same question.
        await PutAuthAsync($"/api/v1/admin/questions/{qid}/hide", new { }, admin);
        await PutAuthAsync($"/api/v1/admin/questions/{qid}/approve", new { }, admin);

        var desk = await GetAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/questions/moderate", admin);
        var rows = (await desk.Content
            .ReadFromJsonAsync<ApiResult<IReadOnlyList<SessionQuestionModeratorRow>>>())!.Data!;
        // Back on the desk as Approved, but NOT flagged on stage.
        Assert.Contains(rows, r =>
            r.Id == qid && r.Status == QuestionStatus.Approved
            && !r.IsPushed && r.PushedAt is null);
    }

    [Fact]
    public async Task Escalate_sets_the_role_and_keeps_it_pending()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var session = await SeedPreSessionAsync();
        var qid = await SubmitQuestionAsync(session.Id);

        var escalate = await PutAuthAsync(
            $"/api/v1/admin/questions/{qid}/escalate",
            new EscalateQuestionRequest { Role = "ScientificCommittee" }, admin);
        Assert.Equal(HttpStatusCode.OK, escalate.StatusCode);
        var row = (await escalate.Content
            .ReadFromJsonAsync<ApiResult<SessionQuestionQueueRow>>())!.Data!;
        Assert.Equal("ScientificCommittee", row.AssignedToRole);
        Assert.Equal(QuestionStatus.Pending, row.Status);

        // Still in the Pending queue (escalation routes, it does not resolve).
        var queue = await ListQueueAsync(admin, session.Id);
        Assert.Contains(queue, q => q.Id == qid && q.AssignedToRole == "ScientificCommittee");
    }

    [Fact]
    public async Task Escalate_with_an_empty_role_is_400()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var session = await SeedPreSessionAsync();
        var qid = await SubmitQuestionAsync(session.Id);

        var response = await PutAuthAsync(
            $"/api/v1/admin/questions/{qid}/escalate",
            new EscalateQuestionRequest { Role = "   " }, admin);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SessionQuestionInvalid, body.Error!.Code);
    }

    [Fact]
    public async Task Non_committee_caller_is_forbidden_on_the_queue()
    {
        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);
        var response = await GetAuthAsync("/api/v1/admin/questions/queue", tokens.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // -- S-8: moderator desk push / reorder guards ----------------------------

    // S-8 — only an approved (desk-visible) question can be pushed to the speaker.
    [Fact]
    public async Task Pushing_a_pending_question_is_400()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var session = await SeedPreSessionAsync();
        var qid = await SubmitQuestionAsync(session.Id);

        var push = await PutAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/questions/{qid}/push", new { }, admin);
        Assert.Equal(HttpStatusCode.BadRequest, push.StatusCode);
        var body = (await push.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SessionQuestionInvalid, body.Error!.Code);
    }

    [Fact]
    public async Task Pushing_a_hidden_question_is_400()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var session = await SeedPreSessionAsync();
        var qid = await SubmitQuestionAsync(session.Id);
        await PutAuthAsync($"/api/v1/admin/questions/{qid}/hide", new { }, admin);

        var push = await PutAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/questions/{qid}/push", new { }, admin);
        Assert.Equal(HttpStatusCode.BadRequest, push.StatusCode);
        var body = (await push.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SessionQuestionInvalid, body.Error!.Code);
    }

    // S-8 — reorder validates + renumbers the Committee-approved DESK subset only.
    [Fact]
    public async Task Reorder_over_the_approved_subset_succeeds_and_rejects_other_sets()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var session = await SeedPreSessionAsync();
        var q1 = await SubmitQuestionAsync(session.Id);
        var q2 = await SubmitQuestionAsync(session.Id);
        await PutAuthAsync($"/api/v1/admin/questions/{q1}/approve", new { }, admin);
        await PutAuthAsync($"/api/v1/admin/questions/{q2}/approve", new { }, admin);

        // Exactly the two approved ids → OK, and the desk order follows the list.
        var ok = await PutAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/questions/reorder",
            new { orderedQuestionIds = new[] { q2, q1 } }, admin);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);

        var desk = await GetAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/questions/moderate", admin);
        var rows = (await desk.Content
            .ReadFromJsonAsync<ApiResult<IReadOnlyList<SessionQuestionModeratorRow>>>())!.Data!;
        Assert.Equal(new[] { q2, q1 }, rows.Select(r => r.Id).ToArray());

        // Omitting an approved id → 400.
        var missing = await PutAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/questions/reorder",
            new { orderedQuestionIds = new[] { q1 } }, admin);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal(ErrorCodes.SessionQuestionInvalid,
            (await missing.Content.ReadFromJsonAsync<ApiResult<object>>())!.Error!.Code);

        // Including an off-desk (hidden) id → 400.
        var q3 = await SubmitQuestionAsync(session.Id);
        await PutAuthAsync($"/api/v1/admin/questions/{q3}/hide", new { }, admin);
        var withHidden = await PutAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/questions/reorder",
            new { orderedQuestionIds = new[] { q1, q2, q3 } }, admin);
        Assert.Equal(HttpStatusCode.BadRequest, withHidden.StatusCode);
        Assert.Equal(ErrorCodes.SessionQuestionInvalid,
            (await withHidden.Content.ReadFromJsonAsync<ApiResult<object>>())!.Error!.Code);
    }

    // -- Helpers --------------------------------------------------------------

    private async Task<IReadOnlyList<SessionQuestionQueueRow>> ListQueueAsync(
        string token, Guid sessionId, QuestionStatus? status = null)
    {
        var url = $"/api/v1/admin/questions/queue?sessionId={sessionId}";
        if (status is { } s) { url += $"&status={s}"; }
        var response = await GetAuthAsync(url, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content
            .ReadFromJsonAsync<ApiResult<IReadOnlyList<SessionQuestionQueueRow>>>())!.Data!;
    }

    private async Task<Guid> SubmitQuestionAsync(Guid sessionId)
    {
        var token = await SignInApprovedVisitorAsync();
        var response = await PostAuthAsync(
            $"/api/v1/app/sessions/{sessionId}/questions",
            // A pre-question (future session, no venue gate) that lands Pending.
            new SubmitSessionQuestionRequest { QuestionText = "Pipeline question?", IsAtVenue = false },
            token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content
            .ReadFromJsonAsync<ApiResult<SessionQuestionSubmitted>>())!.Data!.Id;
    }

    /// <summary>Create + sign in a visitor bumped to Approved so the submit
    /// endpoint's RequireApprovedAccount policy lets them through.</summary>
    private async Task<string> SignInApprovedVisitorAsync()
    {
        var email = $"cq-visitor-{Guid.NewGuid():N}@simf.test";
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
        var envelope = (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        return envelope.Data!.Tokens!.AccessToken;
    }

    // Owner 2026-07-19 (two-path Q&A): the Scientific Committee handles PRE
    // questions only (a LIVE question auto-approves straight to the moderator
    // desk), so the committee tests seed a FUTURE session — every submitted
    // question lands Pending for the committee, which is what these tests need.
    private async Task<Session> SeedPreSessionAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var hall = new Hall
        {
            Id = Guid.NewGuid(),
            Code = "H-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Committee Hall", NameArabic = "قاعة اللجنة",
            Capacity = 100, IsActive = true, CreatedAt = SimfClock.Now,
        };
        db.Halls.Add(hall);
        var session = new Session
        {
            Id = Guid.NewGuid(),
            Code = "SES-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Title = "Committee Session", TitleArabic = "جلسة اللجنة",
            HallId = hall.Id,
            Start = SimfClock.Now.AddHours(2),
            End = SimfClock.Now.AddHours(3),
            IsActive = true, CreatedAt = SimfClock.Now,
        };
        db.Sessions.Add(session);
        await db.SaveChangesAsync();
        return session;
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"committee-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Committee Admin",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, AdministratorRole);
        }

        return await AuthFlow.SignInControlPanelAsync(_client, _factory, email);
    }

    private Task<HttpResponseMessage> GetAuthAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
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
}
