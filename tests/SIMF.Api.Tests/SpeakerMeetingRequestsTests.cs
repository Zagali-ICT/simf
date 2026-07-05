// D-269 (Mockup page 20 "Speaker profile") — SpeakerMeetingRequest submit + admin respond.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Programme;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Programme;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class SpeakerMeetingRequestsTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public SpeakerMeetingRequestsTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Submit_to_a_speaker_that_allows_meetings_returns_pending()
    {
        var speaker = await SeedSpeakerAsync(allowsMeetings: true);
        var visitor = await SignInApprovedVisitorAsync();
        var submit = await PostAuthAsync(
            $"/api/v1/app/speakers/{speaker.Id}/meeting-requests",
            new SubmitSpeakerMeetingRequestRequest
            {
                RequesterName = "Captain Ahmed",
                Subject = "I'd like to discuss naval cybersecurity.",
            },
            visitor);
        Assert.Equal(HttpStatusCode.OK, submit.StatusCode);
        var body = (await submit.Content
            .ReadFromJsonAsync<ApiResult<SpeakerMeetingRequestSubmitted>>())!.Data!;
        Assert.Equal(speaker.Id, body.SpeakerId);
        Assert.Equal(MeetingRequestStatus.Pending, body.Status);
    }

    [Fact]
    public async Task Submit_to_a_speaker_that_does_not_accept_meetings_is_409()
    {
        var speaker = await SeedSpeakerAsync(allowsMeetings: false);
        var visitor = await SignInApprovedVisitorAsync();
        var response = await PostAuthAsync(
            $"/api/v1/app/speakers/{speaker.Id}/meeting-requests",
            new SubmitSpeakerMeetingRequestRequest
            {
                RequesterName = "V", Subject = "T",
            }, visitor);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SpeakerMeetingRequestsNotAllowed, body.Error!.Code);
    }

    [Fact]
    public async Task Submit_requires_login()
    {
        // The speaker reads are anonymous, but the meeting request is login-only.
        var speaker = await SeedSpeakerAsync(allowsMeetings: true);
        var response = await _client.PostAsJsonAsync(
            $"/api/v1/app/speakers/{speaker.Id}/meeting-requests",
            new SubmitSpeakerMeetingRequestRequest { RequesterName = "V", Subject = "T" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Submit_to_unknown_speaker_is_404()
    {
        var visitor = await SignInApprovedVisitorAsync();
        var response = await PostAuthAsync(
            $"/api/v1/app/speakers/{Guid.NewGuid()}/meeting-requests",
            new SubmitSpeakerMeetingRequestRequest { RequesterName = "V", Subject = "T" },
            visitor);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SpeakerNotFound, body.Error!.Code);
    }

    [Fact]
    public async Task Submit_with_empty_subject_is_invalid()
    {
        var speaker = await SeedSpeakerAsync(allowsMeetings: true);
        var visitor = await SignInApprovedVisitorAsync();
        var response = await PostAuthAsync(
            $"/api/v1/app/speakers/{speaker.Id}/meeting-requests",
            new SubmitSpeakerMeetingRequestRequest { RequesterName = "Captain", Subject = "  " },
            visitor);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SpeakerMeetingRequestInvalid, body.Error!.Code);
    }

    [Fact]
    public async Task Admin_lists_then_responds_with_Accepted()
    {
        var speaker = await SeedSpeakerAsync(allowsMeetings: true);
        var visitor = await SignInApprovedVisitorAsync();
        var created = await SubmitAsync(speaker.Id, "Visitor", "Test topic", visitor);

        var admin = await CreateAdministratorAndSignInAsync();
        var list = await PostAuthAsync(
            "/api/v1/admin/speaker-meeting-requests/list",
            new GridQuery { Top = 100 }, admin);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var page = (await list.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminSpeakerMeetingRequestRow>>>())!.Data!;
        var row = Assert.Single(page.Items, r => r.Id == created.Id);
        Assert.Equal(speaker.Name, row.SpeakerName);

        var respond = await PutAuthAsync(
            $"/api/v1/admin/speaker-meeting-requests/{created.Id}/respond",
            new RespondToSpeakerMeetingRequestRequest
            {
                Status = MeetingRequestStatus.Accepted,
                ResponseNote = "Confirmed for tomorrow at 10am.",
            }, admin);
        Assert.Equal(HttpStatusCode.OK, respond.StatusCode);
        var responded = (await respond.Content
            .ReadFromJsonAsync<ApiResult<AdminSpeakerMeetingRequestDetail>>())!.Data!;
        Assert.Equal(MeetingRequestStatus.Accepted, responded.Status);
        Assert.NotNull(responded.RespondedAt);
    }

    [Fact]
    public async Task List_response_does_not_contain_requester_email()
    {
        // The list row must not carry RequesterEmail — bulk PII stays off the grid
        // (the D-185 pattern).
        var speaker = await SeedSpeakerAsync(allowsMeetings: true);
        var visitor = await SignInApprovedVisitorAsync();
        await SubmitAsync(speaker.Id, "Visitor", "T", visitor);

        var admin = await CreateAdministratorAndSignInAsync();
        var list = await PostAuthAsync(
            "/api/v1/admin/speaker-meeting-requests/list",
            new GridQuery { Top = 100 }, admin);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var page = (await list.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminSpeakerMeetingRequestRow>>>())!.Data!;
        Assert.NotEmpty(page.Items);
        var raw = await list.Content.ReadAsStringAsync();
        Assert.DoesNotContain("requesterEmail", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_requires_administrator_role()
    {
        var speaker = await SeedSpeakerAsync(allowsMeetings: true);
        var visitor = await SignInApprovedVisitorAsync();
        var created = await SubmitAsync(speaker.Id, "V", "T", visitor);

        var response = await GetAuthAsync(
            $"/api/v1/admin/speaker-meeting-requests/{created.Id}", visitor);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Get_returns_detail_with_email_and_speaker_name()
    {
        var speaker = await SeedSpeakerAsync(allowsMeetings: true);
        var visitor = await SignInApprovedVisitorAsync();
        var created = await SubmitAsync(speaker.Id, "V", "T", visitor);

        var admin = await CreateAdministratorAndSignInAsync();
        var get = await GetAuthAsync(
            $"/api/v1/admin/speaker-meeting-requests/{created.Id}", admin);
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var detail = (await get.Content
            .ReadFromJsonAsync<ApiResult<AdminSpeakerMeetingRequestDetail>>())!.Data!;
        Assert.Equal(created.Id, detail.Id);
        Assert.Equal(speaker.Name, detail.SpeakerName);
        Assert.False(string.IsNullOrEmpty(detail.RequesterEmail));
    }

    [Fact]
    public async Task Respond_with_Pending_status_returns_400()
    {
        var speaker = await SeedSpeakerAsync(allowsMeetings: true);
        var visitor = await SignInApprovedVisitorAsync();
        var created = await SubmitAsync(speaker.Id, "V", "T", visitor);

        var admin = await CreateAdministratorAndSignInAsync();
        var respond = await PutAuthAsync(
            $"/api/v1/admin/speaker-meeting-requests/{created.Id}/respond",
            new RespondToSpeakerMeetingRequestRequest { Status = MeetingRequestStatus.Pending },
            admin);
        Assert.Equal(HttpStatusCode.BadRequest, respond.StatusCode);
        var body = (await respond.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SpeakerMeetingRequestStatusInvalid, body.Error!.Code);
    }

    [Fact]
    public async Task Get_for_unknown_id_is_404()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var response = await GetAuthAsync(
            $"/api/v1/admin/speaker-meeting-requests/{Guid.NewGuid()}", admin);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_second_pending_request_for_the_same_speaker_is_rejected()
    {
        // A1 — one open request per (requester, speaker): the second submit while a
        // Pending one exists is a 409 duplicate.
        var speaker = await SeedSpeakerAsync(allowsMeetings: true);
        var visitor = await SignInApprovedVisitorAsync();
        await SubmitAsync(speaker.Id, "Visitor", "First topic", visitor);

        var second = await PostAuthAsync(
            $"/api/v1/app/speakers/{speaker.Id}/meeting-requests",
            new SubmitSpeakerMeetingRequestRequest { RequesterName = "Visitor", Subject = "Second topic" },
            visitor);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var body = (await second.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.AppRequestDuplicatePending, body.Error!.Code);
    }

    [Fact]
    public async Task Responding_to_an_already_decided_request_is_409()
    {
        // A1 — only a Pending request may be decided; a second respond is a 409.
        var speaker = await SeedSpeakerAsync(allowsMeetings: true);
        var visitor = await SignInApprovedVisitorAsync();
        var created = await SubmitAsync(speaker.Id, "Visitor", "Topic", visitor);
        var admin = await CreateAdministratorAndSignInAsync();

        var first = await PutAuthAsync(
            $"/api/v1/admin/speaker-meeting-requests/{created.Id}/respond",
            new RespondToSpeakerMeetingRequestRequest { Status = MeetingRequestStatus.Rejected },
            admin);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await PutAuthAsync(
            $"/api/v1/admin/speaker-meeting-requests/{created.Id}/respond",
            new RespondToSpeakerMeetingRequestRequest { Status = MeetingRequestStatus.Accepted },
            admin);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var body = (await second.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.AppRequestAlreadyResponded, body.Error!.Code);
    }

    [Fact]
    public async Task Responding_with_Cancelled_status_is_400()
    {
        // A1 (review) — only Accepted/Rejected are valid responses; Cancelled (a
        // requester-only state) or any other value must not corrupt the lifecycle.
        var speaker = await SeedSpeakerAsync(allowsMeetings: true);
        var visitor = await SignInApprovedVisitorAsync();
        var created = await SubmitAsync(speaker.Id, "Visitor", "Topic", visitor);
        var admin = await CreateAdministratorAndSignInAsync();

        var respond = await PutAuthAsync(
            $"/api/v1/admin/speaker-meeting-requests/{created.Id}/respond",
            new RespondToSpeakerMeetingRequestRequest { Status = MeetingRequestStatus.Cancelled },
            admin);
        Assert.Equal(HttpStatusCode.BadRequest, respond.StatusCode);
        var body = (await respond.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SpeakerMeetingRequestStatusInvalid, body.Error!.Code);
    }

    [Fact]
    public async Task List_writes_audit_event()
    {
        var (admin, adminId) = await CreateAdministratorAndSignInWithIdAsync();
        var list = await PostAuthAsync(
            "/api/v1/admin/speaker-meeting-requests/list",
            new GridQuery { Top = 25 }, admin);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var recorded = await db.OperationLog
            .Where(e => e.EventType == "Admin.SpeakerMeetingRequestsListed"
                        && e.ActorUserId == adminId)
            .OrderByDescending(e => e.TimestampUtc)
            .FirstOrDefaultAsync();
        Assert.NotNull(recorded);
        Assert.Equal(AuditOutcome.Success, recorded!.Outcome);
        Assert.Contains("\"count\"", recorded.Detail!);
    }

    // -- Helpers --------------------------------------------------------------

    private async Task<SpeakerMeetingRequestSubmitted> SubmitAsync(
        Guid speakerId, string name, string subject, string token)
    {
        var submit = await PostAuthAsync(
            $"/api/v1/app/speakers/{speakerId}/meeting-requests",
            new SubmitSpeakerMeetingRequestRequest { RequesterName = name, Subject = subject },
            token);
        Assert.Equal(HttpStatusCode.OK, submit.StatusCode);
        return (await submit.Content
            .ReadFromJsonAsync<ApiResult<SpeakerMeetingRequestSubmitted>>())!.Data!;
    }

    private async Task<Speaker> SeedSpeakerAsync(bool allowsMeetings)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var speaker = new Speaker
        {
            Id = Guid.NewGuid(),
            Code = "SPK-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Capt. Rashid Al-Subaie", NameArabic = "راشد بن طلال السبيعي",
            Rank = "Naval Captain",
            AllowsMeetingRequests = allowsMeetings,
            IsActive = true,
            DisplayOrder = 0,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Speakers.Add(speaker);
        await db.SaveChangesAsync();
        return speaker;
    }

    private async Task<string> SignInApprovedVisitorAsync()
    {
        var email = $"smr-visitor-{Guid.NewGuid():N}@simf.test";
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
        var (token, _) = await CreateAdministratorAndSignInWithIdAsync();
        return token;
    }

    private async Task<(string Token, Guid UserId)> CreateAdministratorAndSignInWithIdAsync()
    {
        var email = $"smr-admin-{Guid.NewGuid():N}@simf.test";
        Guid userId;
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
                DisplayName = "SMR Admin",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, AdministratorRole);
            userId = user.Id;
        }
        var sign = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest
            {
                Email = email, Password = AuthFlow.Password,
                Audience = SignInAudience.Cp,
            });
        var body = (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        return (body.Data!.Tokens!.AccessToken, userId);
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
