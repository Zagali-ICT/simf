// DEF-MOD-001 / DEF-MOD-002 — the moderator desk's "answered" mark used to live
// only in a Dart Set on the screen (gone on exit / restart / co-moderator), and a
// rejected question was unrecoverable from the app because the desk list returned
// Approved rows only. These tests pin the persisted status + the status-filtered
// list, including that neither leaks a hidden question to an attendee.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Sessions;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Programme;
using SIMF.Domain.SessionQuestions;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class ModeratorDeskStateTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public ModeratorDeskStateTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Answered_persists_stays_on_the_desk_and_round_trips()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var session = await SeedLiveSessionAsync();
        var qid = await SubmitLiveQuestionAsync(session.Id, "Answer me");

        var mark = await PutAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/questions/{qid}/answered",
            new { isAnswered = true }, admin);
        Assert.Equal(HttpStatusCode.OK, mark.StatusCode);
        var row = (await mark.Content
            .ReadFromJsonAsync<ApiResult<SessionQuestionModeratorRow>>())!.Data!;
        Assert.Equal(QuestionStatus.Answered, row.Status);

        // Persisted — a brand-new read (i.e. re-opening the desk) still sees it.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            var stored = await db.SessionQuestions.AsNoTracking()
                .SingleAsync(q => q.Id == qid);
            Assert.Equal(QuestionStatus.Answered, stored.Status);
        }

        // …and it is still on the working desk (its own tab), not dropped.
        var desk = await GetAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/questions/moderate", admin);
        var deskRows = (await desk.Content
            .ReadFromJsonAsync<ApiResult<IReadOnlyList<SessionQuestionModeratorRow>>>())!.Data!;
        Assert.Contains(deskRows, r => r.Id == qid && r.Status == QuestionStatus.Answered);

        // Un-marking returns it to Approved; both calls are idempotent.
        var unmark = await PutAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/questions/{qid}/answered",
            new { isAnswered = false }, admin);
        var back = (await unmark.Content
            .ReadFromJsonAsync<ApiResult<SessionQuestionModeratorRow>>())!.Data!;
        Assert.Equal(QuestionStatus.Approved, back.Status);

        var again = await PutAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/questions/{qid}/answered",
            new { isAnswered = false }, admin);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
    }

    [Fact]
    public async Task Answered_on_a_pending_question_is_rejected()
    {
        // A question that has not cleared the Committee is not on the desk, so it
        // cannot be "answered on stage".
        var admin = await CreateAdministratorAndSignInAsync();
        var session = await SeedFutureSessionAsync();
        var qid = await SubmitQuestionAsync(session.Id, "Ask ahead");

        var mark = await PutAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/questions/{qid}/answered",
            new { isAnswered = true }, admin);

        Assert.Equal(HttpStatusCode.BadRequest, mark.StatusCode);
        var body = (await mark.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SessionQuestionInvalid, body.Error!.Code);
    }

    [Fact]
    public async Task Answered_requires_the_moderator_grant()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var session = await SeedLiveSessionAsync();
        var qid = await SubmitLiveQuestionAsync(session.Id, "Not yours");
        var outsider = await AuthFlow.SignInApprovedVisitorWithoutTwoFactorAsync(_client, _factory);

        var mark = await PutAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/questions/{qid}/answered",
            new { isAnswered = true }, outsider.AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden, mark.StatusCode);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var stored = await db.SessionQuestions.AsNoTracking().SingleAsync(q => q.Id == qid);
        Assert.Equal(QuestionStatus.Approved, stored.Status);
        // Guard the whole endpoint set, not just this one call.
        Assert.Equal(HttpStatusCode.Forbidden,
            (await GetAuthAsync(
                $"/api/v1/app/sessions/{session.Id}/questions/moderate?status=Hidden",
                outsider.AccessToken)).StatusCode);
    }

    [Fact]
    public async Task Rejected_questions_are_retrievable_and_restorable_by_the_desk()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var session = await SeedLiveSessionAsync();
        var qid = await SubmitLiveQuestionAsync(session.Id, "Mis-clicked");

        await PutAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/questions/{qid}/hide",
            new SetQuestionHiddenRequest { IsHidden = true }, admin);

        // It has left the working desk …
        var desk = await GetAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/questions/moderate", admin);
        Assert.DoesNotContain(
            (await desk.Content
                .ReadFromJsonAsync<ApiResult<IReadOnlyList<SessionQuestionModeratorRow>>>())!.Data!,
            r => r.Id == qid);

        // … but the desk can still LIST it under the rejected tab (DEF-MOD-002).
        var rejected = await GetAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/questions/moderate?status=Hidden", admin);
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        var rejectedRows = (await rejected.Content
            .ReadFromJsonAsync<ApiResult<IReadOnlyList<SessionQuestionModeratorRow>>>())!.Data!;
        Assert.Contains(rejectedRows, r => r.Id == qid && r.IsHidden);

        // … and restore it back onto the desk.
        await PutAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/questions/{qid}/hide",
            new SetQuestionHiddenRequest { IsHidden = false }, admin);
        var deskAgain = await GetAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/questions/moderate", admin);
        Assert.Contains(
            (await deskAgain.Content
                .ReadFromJsonAsync<ApiResult<IReadOnlyList<SessionQuestionModeratorRow>>>())!.Data!,
            r => r.Id == qid);
    }

    [Fact]
    public async Task A_hidden_question_never_reaches_an_attendee()
    {
        // The status filter is only reachable through the moderator gate; the
        // attendee's own surfaces must not carry the hidden row.
        var admin = await CreateAdministratorAndSignInAsync();
        var session = await SeedLiveSessionAsync();
        var visitor = await AuthFlow.SignInApprovedVisitorWithoutTwoFactorAsync(_client, _factory);

        var submit = await PostAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/questions",
            new SubmitSessionQuestionRequest { QuestionText = "Hide me", IsAtVenue = true },
            visitor.AccessToken);
        var qid = (await submit.Content
            .ReadFromJsonAsync<ApiResult<SessionQuestionSubmitted>>())!.Data!.Id;
        await PutAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/questions/{qid}/hide",
            new SetQuestionHiddenRequest { IsHidden = true }, admin);

        // The submitter — an ordinary attendee — is forbidden on the desk, with
        // or without the status filter: no leak path.
        Assert.Equal(HttpStatusCode.Forbidden,
            (await GetAuthAsync(
                $"/api/v1/app/sessions/{session.Id}/questions/moderate",
                visitor.AccessToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await GetAuthAsync(
                $"/api/v1/app/sessions/{session.Id}/questions/moderate?status=Hidden",
                visitor.AccessToken)).StatusCode);
    }

    [Fact]
    public async Task A_granted_moderator_can_work_the_answered_and_rejected_tabs()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var session = await SeedLiveSessionAsync();
        var qid = await SubmitLiveQuestionAsync(session.Id, "Grant path");
        var moderator = await AuthFlow.SignInApprovedVisitorWithoutTwoFactorAsync(_client, _factory);
        var moderatorId = Guid.Parse(SubjectFromToken(moderator.AccessToken));

        using (var scope = _factory.Services.CreateScope())
        {
            var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            appDb.SessionModerators.Add(new SessionModerator
            {
                SessionId = session.Id,
                UserId = moderatorId,
                AssignedByUserId = moderatorId,
                AssignedAt = DateTimeOffset.UtcNow,
            });
            await appDb.SaveChangesAsync();
        }

        var mark = await PutAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/questions/{qid}/answered",
            new { isAnswered = true }, moderator.AccessToken);
        Assert.Equal(HttpStatusCode.OK, mark.StatusCode);

        var answered = await GetAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/questions/moderate?status=Answered",
            moderator.AccessToken);
        Assert.Equal(HttpStatusCode.OK, answered.StatusCode);
        Assert.Contains(
            (await answered.Content
                .ReadFromJsonAsync<ApiResult<IReadOnlyList<SessionQuestionModeratorRow>>>())!.Data!,
            r => r.Id == qid);
    }

    [Fact]
    public async Task Pending_is_not_a_desk_tab_and_is_refused()
    {
        // DEF-MOD-002 (r2) — the desk tab filter is an allow-list. A per-session
        // moderator works stage 3; a Pending question is still inside the
        // Scientific Committee's stage-2 gate (D-212), so the desk must not be
        // able to enumerate — let alone READ — its text. A bad value is a 400, not
        // a silently ignored filter.
        var admin = await CreateAdministratorAndSignInAsync();
        var session = await SeedFutureSessionAsync();
        const string secret = "Committee eyes only";
        await SubmitQuestionAsync(session.Id, secret);

        var pending = await GetAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/questions/moderate?status=Pending", admin);

        Assert.Equal(HttpStatusCode.BadRequest, pending.StatusCode);
        var payload = await pending.Content.ReadAsStringAsync();
        Assert.DoesNotContain(secret, payload);
        var body = (await pending.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SessionQuestionInvalid, body.Error!.Code);

        // The three real tabs still answer 200 (the fix is a filter, not a lock).
        foreach (var tab in new[] { "Approved", "Answered", "Hidden" })
        {
            var response = await GetAuthAsync(
                $"/api/v1/app/sessions/{session.Id}/questions/moderate?status={tab}", admin);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task Un_hiding_an_answered_question_keeps_the_answered_mark()
    {
        // D-771 — hide/restore is a recovery path, not a demotion: the answered
        // mark DEF-MOD-001 made durable must survive it.
        var admin = await CreateAdministratorAndSignInAsync();
        var session = await SeedLiveSessionAsync();
        var qid = await SubmitLiveQuestionAsync(session.Id, "Answered then mis-clicked");

        await PutAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/questions/{qid}/answered",
            new { isAnswered = true }, admin);
        await PutAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/questions/{qid}/hide",
            new SetQuestionHiddenRequest { IsHidden = true }, admin);

        var restored = await PutAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/questions/{qid}/hide",
            new SetQuestionHiddenRequest { IsHidden = false }, admin);

        var row = (await restored.Content
            .ReadFromJsonAsync<ApiResult<SessionQuestionModeratorRow>>())!.Data!;
        Assert.Equal(QuestionStatus.Answered, row.Status);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var stored = await db.SessionQuestions.AsNoTracking().SingleAsync(q => q.Id == qid);
        Assert.Equal(QuestionStatus.Answered, stored.Status);
    }

    [Fact]
    public async Task Restoring_a_committee_rejected_question_returns_it_to_the_committee()
    {
        // D-771 — the rejected tab lists everything hidden on the session,
        // including questions the COMMITTEE rejected at stage 2. Restoring one
        // must put it back where it came from (Pending — the Committee queue), not
        // promote it onto the desk from where it could be pushed on stage and
        // published into the public recorded archive.
        var admin = await CreateAdministratorAndSignInAsync();
        var session = await SeedFutureSessionAsync();
        var qid = await SubmitQuestionAsync(session.Id, "Rejected by the committee");

        var committeeHide = await PutAuthAsync(
            $"/api/v1/admin/questions/{qid}/hide", new { }, admin);
        Assert.Equal(HttpStatusCode.OK, committeeHide.StatusCode);

        await PutAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/questions/{qid}/hide",
            new SetQuestionHiddenRequest { IsHidden = false }, admin);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            var stored = await db.SessionQuestions.AsNoTracking().SingleAsync(q => q.Id == qid);
            Assert.Equal(QuestionStatus.Pending, stored.Status);
        }

        // It is NOT on the working desk …
        var desk = await GetAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/questions/moderate", admin);
        Assert.DoesNotContain(
            (await desk.Content
                .ReadFromJsonAsync<ApiResult<IReadOnlyList<SessionQuestionModeratorRow>>>())!.Data!,
            r => r.Id == qid);
        // … and it cannot be pushed on stage.
        var push = await PutAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/questions/{qid}/push", new { }, admin);
        Assert.Equal(HttpStatusCode.BadRequest, push.StatusCode);
    }

    [Fact]
    public async Task Reorder_accepts_the_desk_including_its_answered_rows()
    {
        // DEF-MOD-001 put Answered rows on the working desk, so the desk sends
        // them back on a drag-and-drop reorder. Validating the payload against the
        // Approved rows only made every reorder on a session with one answered
        // question a 400.
        var admin = await CreateAdministratorAndSignInAsync();
        var session = await SeedLiveSessionAsync();
        var first = await SubmitLiveQuestionAsync(session.Id, "First up");
        var second = await SubmitLiveQuestionAsync(session.Id, "Second up");

        await PutAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/questions/{first}/answered",
            new { isAnswered = true }, admin);

        var reorder = await PutAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/questions/reorder",
            new ReorderQuestionsRequest
            {
                OrderedQuestionIds = new List<Guid> { second, first },
            },
            admin);

        Assert.Equal(HttpStatusCode.OK, reorder.StatusCode);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        Assert.Equal(0, (await db.SessionQuestions.AsNoTracking()
            .SingleAsync(q => q.Id == second)).Order);
        Assert.Equal(1, (await db.SessionQuestions.AsNoTracking()
            .SingleAsync(q => q.Id == first)).Order);
    }

    // -- Helpers --------------------------------------------------------------

    /// <summary>Submits a question on a LIVE session — the owner's two-path Q&amp;A
    /// lands it straight on the moderator desk (Approved), no Committee step.</summary>
    private async Task<Guid> SubmitLiveQuestionAsync(Guid sessionId, string text) =>
        await SubmitQuestionAsync(sessionId, text);

    private async Task<Guid> SubmitQuestionAsync(Guid sessionId, string text)
    {
        var visitor = await AuthFlow.SignInApprovedVisitorWithoutTwoFactorAsync(_client, _factory);
        var submit = await PostAuthAsync(
            $"/api/v1/app/sessions/{sessionId}/questions",
            new SubmitSessionQuestionRequest { QuestionText = text, IsAtVenue = true },
            visitor.AccessToken);
        return (await submit.Content
            .ReadFromJsonAsync<ApiResult<SessionQuestionSubmitted>>())!.Data!.Id;
    }

    private Task<Session> SeedLiveSessionAsync() => SeedSessionAsync(
        DateTimeOffset.UtcNow.AddMinutes(-15), DateTimeOffset.UtcNow.AddMinutes(45));

    private Task<Session> SeedFutureSessionAsync() => SeedSessionAsync(
        DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(1).AddHours(1));

    private async Task<Session> SeedSessionAsync(
        DateTimeOffset start, DateTimeOffset end)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var hall = new Hall
        {
            Id = Guid.NewGuid(),
            Code = "H-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Hall D", NameArabic = "قاعة د",
            Capacity = 100, IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Halls.Add(hall);
        var session = new Session
        {
            Id = Guid.NewGuid(),
            Code = "SES-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Title = "Desk State", TitleArabic = "حالة المكتب",
            HallId = hall.Id,
            Start = start, End = end,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Sessions.Add(session);
        await db.SaveChangesAsync();
        return session;
    }

    private static string SubjectFromToken(string accessToken)
    {
        var payload = accessToken.Split('.')[1];
        switch (payload.Length % 4)
        {
            case 2: payload += "=="; break;
            case 3: payload += "="; break;
        }
        var bytes = Convert.FromBase64String(payload.Replace('-', '+').Replace('_', '/'));
        using var doc = System.Text.Json.JsonDocument.Parse(
            System.Text.Encoding.UTF8.GetString(bytes));
        return doc.RootElement.GetProperty("sub").GetString()!;
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"mds-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "MDS Admin",
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
