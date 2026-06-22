// P4.1b — D-238 (Completion Programme §6.4.1): the Scientific-Committee session-
// summary / محضر desk. Covers AI-draft (Echo provider), hand-written save,
// publish → public read → unpublish, the desk list, length validation, the
// publish-without-summary 404, and the non-committee forbidden case.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Programme;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Profiles;
using SIMF.Domain.Programme;
using SIMF.Domain.SessionQuestions;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class SessionSummaryCommitteeTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public SessionSummaryCommitteeTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Generate_drafts_a_summary_through_the_ai_seam()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var sessionId = await SeedSessionAsync();

        var detail = await GenerateAsync(sessionId, admin);
        // The seeded prompt routes to the Echo provider — a deterministic draft.
        Assert.NotNull(detail.AiModel);
        // The prompt produces Arabic, so the draft lands in the Arabic column
        // only; the English column stays empty for the Committee to fill.
        Assert.False(string.IsNullOrWhiteSpace(detail.FullTextArabic));
        Assert.True(string.IsNullOrEmpty(detail.FullText));
        Assert.False(detail.IsPublished);
    }

    [Fact]
    public async Task Generate_feeds_the_session_subtitle_transcript_into_the_ai_draft()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        // A session whose live subtitle/transcript carries a distinctive phrase.
        var sessionId = await SeedSessionAsync(
            liveCaptionsArabic: "تعاون الغوّاصات وأمن الملاحة البحرية");

        var detail = await GenerateAsync(sessionId, admin);

        // The Echo provider echoes the substituted user prompt, which now carries
        // the session's transcript — proving LiveCaptions flows into the AI input.
        Assert.Contains("تعاون الغوّاصات", detail.FullTextArabic!);
    }

    [Fact]
    public async Task Save_creates_a_hand_written_draft_with_no_ai_model()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var sessionId = await SeedSessionAsync();

        var detail = await SaveAsync(sessionId, new SaveSessionSummaryRequest
        {
            KeyPoints = "Point one\nPoint two",
            Recommendations = "Strengthen cooperation.",
            Speakers = "Speaker A · Speaker B",
            FullText = "The session covered maritime supply-chain security.",
        }, admin);

        Assert.Null(detail.AiModel);
        Assert.Equal("Point one\nPoint two", detail.KeyPoints);
        Assert.False(detail.IsPublished);
    }

    [Fact]
    public async Task Publish_exposes_the_public_read_then_unpublish_hides_it()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var sessionId = await SeedSessionAsync();
        await SaveAsync(sessionId, new SaveSessionSummaryRequest
        {
            FullText = "Minutes.", FullTextArabic = "محضر.",
        }, admin);

        // Before publish: the public read is 404.
        Assert.Equal(HttpStatusCode.NotFound,
            (await _client.GetAsync(PublicUrl(sessionId))).StatusCode);

        await PutAuthAsync($"/api/v1/admin/session-summaries/{sessionId}/publish", new { }, admin);

        var published = await _client.GetAsync(PublicUrl(sessionId));
        Assert.Equal(HttpStatusCode.OK, published.StatusCode);
        var body = (await published.Content
            .ReadFromJsonAsync<ApiResult<PublicSessionSummary>>())!.Data!;
        Assert.Equal("محضر.", body.FullTextArabic);

        await PutAuthAsync($"/api/v1/admin/session-summaries/{sessionId}/unpublish", new { }, admin);
        Assert.Equal(HttpStatusCode.NotFound,
            (await _client.GetAsync(PublicUrl(sessionId))).StatusCode);
    }

    [Fact]
    public async Task The_desk_list_reflects_the_summary_state()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var sessionId = await SeedSessionAsync();
        await GenerateAsync(sessionId, admin);
        await PutAuthAsync($"/api/v1/admin/session-summaries/{sessionId}/publish", new { }, admin);

        var response = await GetAuthAsync("/api/v1/admin/session-summaries", admin);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rows = (await response.Content
            .ReadFromJsonAsync<ApiResult<IReadOnlyList<AdminSessionSummaryRow>>>())!.Data!;
        var row = Assert.Single(rows, r => r.SessionId == sessionId);
        Assert.True(row.HasSummary);
        Assert.True(row.GeneratedByAi);
        Assert.True(row.IsPublished);
    }

    [Fact]
    public async Task An_oversized_section_is_rejected_400()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var sessionId = await SeedSessionAsync();

        var response = await PutAuthAsync(
            $"/api/v1/admin/session-summaries/{sessionId}",
            new SaveSessionSummaryRequest { FullText = new string('x', 8001) }, admin);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Publishing_a_session_with_no_summary_is_404()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var sessionId = await SeedSessionAsync();

        var response = await PutAuthAsync(
            $"/api/v1/admin/session-summaries/{sessionId}/publish", new { }, admin);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_non_committee_account_is_forbidden_from_the_desk()
    {
        var visitor = await SeedApprovedVisitorAsync();

        var response = await GetAuthAsync("/api/v1/admin/session-summaries", visitor);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // -- D-472 (#9): the team review/approval workflow + host/moderator read ---

    [Fact]
    public async Task Submit_then_approve_marks_the_summary_ready_and_the_desk_reflects_it()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var sessionId = await SeedSessionAsync();
        await SaveAsync(sessionId, new SaveSessionSummaryRequest { FullTextArabic = "محضر." }, admin);

        var submitted = await PutAuthAsync(
            $"/api/v1/admin/session-summaries/{sessionId}/submit-review", new { }, admin);
        Assert.Equal(HttpStatusCode.OK, submitted.StatusCode);
        var inReview = (await submitted.Content
            .ReadFromJsonAsync<ApiResult<AdminSessionSummaryDetail>>())!.Data!;
        Assert.True(inReview.IsInReview);
        Assert.False(inReview.IsApproved);

        var approved = await PutAuthAsync(
            $"/api/v1/admin/session-summaries/{sessionId}/approve", new { }, admin);
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        var detail = (await approved.Content
            .ReadFromJsonAsync<ApiResult<AdminSessionSummaryDetail>>())!.Data!;
        Assert.True(detail.IsApproved);
        Assert.False(detail.IsInReview);
        Assert.NotNull(detail.ApprovedAt);

        var rows = await ListAsync(admin);
        var row = Assert.Single(rows, r => r.SessionId == sessionId);
        Assert.True(row.IsApproved);
    }

    [Fact]
    public async Task Approving_without_submitting_is_400()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var sessionId = await SeedSessionAsync();
        await SaveAsync(sessionId, new SaveSessionSummaryRequest { FullTextArabic = "محضر." }, admin);

        var response = await PutAuthAsync(
            $"/api/v1/admin/session-summaries/{sessionId}/approve", new { }, admin);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Editing_a_submitted_summary_returns_it_to_draft()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var sessionId = await SeedSessionAsync();
        await SaveAsync(sessionId, new SaveSessionSummaryRequest { FullTextArabic = "محضر." }, admin);
        await PutAuthAsync(
            $"/api/v1/admin/session-summaries/{sessionId}/submit-review", new { }, admin);

        // Any edit invalidates the review → back to Draft.
        var detail = await SaveAsync(
            sessionId, new SaveSessionSummaryRequest { FullTextArabic = "محضر معدّل." }, admin);
        Assert.False(detail.IsInReview);
        Assert.False(detail.IsApproved);
    }

    [Fact]
    public async Task Return_to_draft_clears_an_approval()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var sessionId = await SeedSessionAsync();
        await SaveAsync(sessionId, new SaveSessionSummaryRequest { FullTextArabic = "محضر." }, admin);
        await PutAuthAsync($"/api/v1/admin/session-summaries/{sessionId}/submit-review", new { }, admin);
        await PutAuthAsync($"/api/v1/admin/session-summaries/{sessionId}/approve", new { }, admin);

        var returned = await PutAuthAsync(
            $"/api/v1/admin/session-summaries/{sessionId}/return-to-draft", new { }, admin);
        Assert.Equal(HttpStatusCode.OK, returned.StatusCode);
        var detail = (await returned.Content
            .ReadFromJsonAsync<ApiResult<AdminSessionSummaryDetail>>())!.Data!;
        Assert.False(detail.IsApproved);
        Assert.False(detail.IsInReview);
    }

    [Fact]
    public async Task A_session_moderator_reads_the_approved_summary_and_404_before_approval()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var sessionId = await SeedSessionAsync();
        var (modToken, modUserId) = await CreateApprovedVisitorAsync();
        await SeedSessionModeratorAsync(sessionId, modUserId);
        await SaveAsync(sessionId, new SaveSessionSummaryRequest { FullTextArabic = "محضر معتمد." }, admin);

        // Authorized, but no approved summary yet → 404.
        Assert.Equal(HttpStatusCode.NotFound,
            (await GetAuthAsync(HostUrl(sessionId), modToken)).StatusCode);

        await PutAuthAsync($"/api/v1/admin/session-summaries/{sessionId}/submit-review", new { }, admin);
        await PutAuthAsync($"/api/v1/admin/session-summaries/{sessionId}/approve", new { }, admin);

        var read = await GetAuthAsync(HostUrl(sessionId), modToken);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        var body = (await read.Content
            .ReadFromJsonAsync<ApiResult<HostSessionSummary>>())!.Data!;
        Assert.Equal("محضر معتمد.", body.FullTextArabic);
    }

    [Fact]
    public async Task The_session_host_reads_the_approved_summary()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var sessionId = await SeedSessionAsync();
        var (hostToken, hostUserId) = await CreateApprovedVisitorAsync();
        await SeedSessionHostAsync(sessionId, hostUserId);
        await SaveAsync(sessionId, new SaveSessionSummaryRequest { FullTextArabic = "محضر." }, admin);
        await PutAuthAsync($"/api/v1/admin/session-summaries/{sessionId}/submit-review", new { }, admin);
        await PutAuthAsync($"/api/v1/admin/session-summaries/{sessionId}/approve", new { }, admin);

        var read = await GetAuthAsync(HostUrl(sessionId), hostToken);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
    }

    [Fact]
    public async Task A_deactivated_host_is_forbidden_from_the_approved_read()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var sessionId = await SeedSessionAsync();
        var (hostToken, hostUserId) = await CreateApprovedVisitorAsync();
        // The host's speaker row is soft-deleted → no longer a host.
        await SeedSessionHostAsync(sessionId, hostUserId, speakerActive: false);
        await SaveAsync(sessionId, new SaveSessionSummaryRequest { FullTextArabic = "محضر." }, admin);
        await PutAuthAsync($"/api/v1/admin/session-summaries/{sessionId}/submit-review", new { }, admin);
        await PutAuthAsync($"/api/v1/admin/session-summaries/{sessionId}/approve", new { }, admin);

        var read = await GetAuthAsync(HostUrl(sessionId), hostToken);
        Assert.Equal(HttpStatusCode.Forbidden, read.StatusCode);
    }

    [Fact]
    public async Task A_non_moderator_non_host_visitor_is_forbidden_from_the_approved_read()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var sessionId = await SeedSessionAsync();
        var (token, _) = await CreateApprovedVisitorAsync();
        await SaveAsync(sessionId, new SaveSessionSummaryRequest { FullTextArabic = "محضر." }, admin);
        await PutAuthAsync($"/api/v1/admin/session-summaries/{sessionId}/submit-review", new { }, admin);
        await PutAuthAsync($"/api/v1/admin/session-summaries/{sessionId}/approve", new { }, admin);

        var read = await GetAuthAsync(HostUrl(sessionId), token);
        Assert.Equal(HttpStatusCode.Forbidden, read.StatusCode);
    }

    // -- Helpers --------------------------------------------------------------

    private static string PublicUrl(Guid sessionId) =>
        $"/api/v1/app/programme/sessions/{sessionId}/summary";

    private static string HostUrl(Guid sessionId) =>
        $"/api/v1/app/programme/sessions/{sessionId}/summary/approved";

    private async Task<IReadOnlyList<AdminSessionSummaryRow>> ListAsync(string token)
    {
        var response = await GetAuthAsync("/api/v1/admin/session-summaries", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content
            .ReadFromJsonAsync<ApiResult<IReadOnlyList<AdminSessionSummaryRow>>>())!.Data!;
    }

    private async Task<(string Token, Guid UserId)> CreateApprovedVisitorAsync()
    {
        var email = $"sum-reader-{Guid.NewGuid():N}@simf.test";
        Guid userId;
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
            var user = new SimfUser
            {
                UserName = email, Email = email, EmailConfirmed = true,
                DisplayName = "Summary Reader",
                AccountState = AccountState.Approved,
                UserType = UserType.Visitor,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            userId = user.Id;
        }
        var sign = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest { Email = email, Password = AuthFlow.Password });
        var token = (await sign.Content
            .ReadFromJsonAsync<ApiResult<SignInResponse>>())!.Data!.Tokens!.AccessToken;
        return (token, userId);
    }

    private async Task SeedSessionModeratorAsync(Guid sessionId, Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        db.SessionModerators.Add(new SessionModerator
        {
            SessionId = sessionId,
            UserId = userId,
            AssignedByUserId = Guid.NewGuid(),
            AssignedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    private async Task SeedSessionHostAsync(Guid sessionId, Guid userId, bool speakerActive = true)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var profile = new UserProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = "Host User",
            NameArabic = "المحاور",
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.UserProfiles.Add(profile);
        var speaker = new Speaker
        {
            Id = Guid.NewGuid(),
            Code = "SPK-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Host User",
            NameArabic = "المحاور",
            UserProfileId = profile.Id,
            IsActive = speakerActive,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Speakers.Add(speaker);
        db.SessionSpeakers.Add(new SessionSpeaker
        {
            SessionId = sessionId,
            SpeakerId = speaker.Id,
            DisplayOrder = 0,
            Role = SessionSpeakerRole.Host,
        });
        await db.SaveChangesAsync();
    }

    private async Task<AdminSessionSummaryDetail> GenerateAsync(Guid sessionId, string token)
    {
        var response = await PostAuthAsync(
            $"/api/v1/admin/session-summaries/{sessionId}/generate", new { }, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminSessionSummaryDetail>>())!.Data!;
    }

    private async Task<AdminSessionSummaryDetail> SaveAsync(
        Guid sessionId, SaveSessionSummaryRequest request, string token)
    {
        var response = await PutAuthAsync(
            $"/api/v1/admin/session-summaries/{sessionId}", request, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminSessionSummaryDetail>>())!.Data!;
    }

    private async Task<Guid> SeedSessionAsync(
        string? liveCaptions = null, string? liveCaptionsArabic = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var hall = new Hall
        {
            Id = Guid.NewGuid(),
            Code = "H-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Summary Hall", NameArabic = "قاعة الملخص",
            Capacity = 100, IsActive = true, CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Halls.Add(hall);
        var session = new Session
        {
            Id = Guid.NewGuid(),
            Code = "SES-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Title = "Maritime Supply-Chain Security", TitleArabic = "أمن سلاسل الإمداد البحرية",
            HallId = hall.Id,
            StartUtc = DateTimeOffset.UtcNow.AddMinutes(-90),
            EndUtc = DateTimeOffset.UtcNow.AddMinutes(-30),
            LiveCaptions = liveCaptions,
            LiveCaptionsArabic = liveCaptionsArabic,
            IsActive = true, CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Sessions.Add(session);
        await db.SaveChangesAsync();
        return session.Id;
    }

    private async Task<string> SeedApprovedVisitorAsync()
    {
        var email = $"sum-visitor-{Guid.NewGuid():N}@simf.test";
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
            var user = new SimfUser
            {
                UserName = email, Email = email, EmailConfirmed = true,
                DisplayName = "Summary Visitor",
                AccountState = AccountState.Approved,
                UserType = UserType.Visitor,
            };
            await users.CreateAsync(user, AuthFlow.Password);
        }
        var sign = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest { Email = email, Password = AuthFlow.Password });
        var body = (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        return body.Data!.Tokens!.AccessToken;
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"sum-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Summary Admin",
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
                Email = email, Password = AuthFlow.Password, Audience = SignInAudience.Cp,
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

    private Task<HttpResponseMessage> PostAuthAsync<TBody>(string url, TBody body, string token)
        where TBody : class
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> PutAuthAsync<TBody>(string url, TBody body, string token)
        where TBody : class
    {
        var request = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
