// QA speaker-meeting round — regression coverage for the defects the lifecycle
// review found:
//   A24  a missing MeetingLinks:PublicWebBaseUrl silently skipped the speaker email;
//   A25  a speaker with no contact email silently stranded the request;
//   A29  the AwaitingSpeaker -> Pending revert told nobody;
//   B12  check-in was a bare status flip the requester never saw;
//   B13  cancelling left the speaker's emailed links alive and said nothing;
//   B20  a rejected / cancelled request could never be reopened.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using SIMF.Application.Auditing;
using SIMF.Application.Email;
using SIMF.Application.Notifications;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Programme;
using SIMF.Contracts.Requests;
using SIMF.Domain.BusinessMeetings;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Profiles;
using SIMF.Domain.Programme;
using SIMF.Infrastructure.Operations;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>QA A25/A29/B12/B13/B20 — a factory whose <see cref="IEmailQueue"/> is the
/// synchronous capturing <see cref="FakeEmailQueue"/>, so a test can assert on the
/// speaker-facing mail the lifecycle enqueues without racing the background sender.</summary>
public sealed class SpeakerMeetingQaApiFactory : SimfApiFactory
{
    public FakeEmailQueue Emails { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IEmailQueue>();
            services.AddSingleton<IEmailQueue>(Emails);
        });
    }
}

public sealed class SpeakerMeetingQaTests : IClassFixture<SpeakerMeetingQaApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SpeakerMeetingQaApiFactory _factory;
    private readonly HttpClient _client;

    public SpeakerMeetingQaTests(SpeakerMeetingQaApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    // -- A25: approve must not strand a request behind an undeliverable email ----

    [Fact]
    public async Task A25_Approve_for_a_speaker_with_no_contact_email_is_refused_and_mints_no_tokens()
    {
        // Every seeded speaker ships with a NULL Email, so before the fix Approve
        // moved the request to AwaitingSpeaker with a valid token pair that nobody
        // could ever receive, and it sat there until the 72h expiry worker reverted it.
        var speaker = await SeedSpeakerAsync(email: null);
        var visitor = await SignInEligibleVisitorAsync();
        var created = await SubmitAsync(speaker.Id, visitor);
        var admin = await CreateAdministratorAndSignInAsync();

        var hallId = await SeedMeetingHallAsync();
        var slot = (await CreateHallWindowAndGetSlotsAsync(hallId, admin))[0];

        var respond = await PutAuthAsync(
            $"/api/v1/admin/speaker-meeting-requests/{created.Id}/respond",
            new RespondToSpeakerMeetingRequestRequest
            {
                Status = MeetingRequestStatus.Accepted,
                HallId = hallId, SlotStart = slot.Start, SlotEnd = slot.End,
            }, admin);

        Assert.Equal(HttpStatusCode.Conflict, respond.StatusCode);
        var body = (await respond.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SpeakerMeetingContactMissing, body.Error!.Code);
        // Bilingual, and precise about what the admin must fix.
        Assert.Contains("contact email", body.Error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(string.IsNullOrWhiteSpace(body.Error.MessageArabic));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        // Nothing was minted and the request never left Pending.
        Assert.False(await db.MeetingActionTokens
            .AnyAsync(t => t.SpeakerMeetingRequestId == created.Id));
        var row = await db.SpeakerMeetingRequests.SingleAsync(r => r.Id == created.Id);
        Assert.Equal(MeetingRequestStatus.Pending, row.Status);
    }

    [Fact]
    public async Task A25_Confirm_still_works_for_a_speaker_with_no_contact_email()
    {
        // The guard is only on the path that NEEDS the email. Confirm (the admin
        // already has the speaker's verbal agreement) mints no link, so it must
        // still book the meeting straight away.
        var speaker = await SeedSpeakerAsync(email: null);
        var visitor = await SignInEligibleVisitorAsync();
        var created = await SubmitAsync(speaker.Id, visitor);
        var admin = await CreateAdministratorAndSignInAsync();

        var hallId = await SeedMeetingHallAsync();
        var slot = (await CreateHallWindowAndGetSlotsAsync(hallId, admin))[0];

        var respond = await PutAuthAsync(
            $"/api/v1/admin/speaker-meeting-requests/{created.Id}/respond",
            new RespondToSpeakerMeetingRequestRequest
            {
                Status = MeetingRequestStatus.Accepted,
                VerbalConfirmed = true,
                HallId = hallId, SlotStart = slot.Start, SlotEnd = slot.End,
            }, admin);

        Assert.Equal(HttpStatusCode.OK, respond.StatusCode);
        var detail = (await respond.Content
            .ReadFromJsonAsync<ApiResult<AdminSpeakerMeetingRequestDetail>>())!.Data!;
        Assert.Equal(MeetingRequestStatus.Accepted, detail.Status);
    }

    [Fact]
    public async Task A25_Resend_confirmation_for_a_speaker_with_no_contact_email_is_refused()
    {
        // The re-send exists to fix a confirmation that never arrived; re-minting a
        // pair that still cannot be delivered is the same silent no-op.
        var speaker = await SeedSpeakerAsync(email: "resend-speaker@simf.test");
        var visitor = await SignInEligibleVisitorAsync();
        var created = await SubmitAsync(speaker.Id, visitor);
        var admin = await CreateAdministratorAndSignInAsync();

        var hallId = await SeedMeetingHallAsync();
        var slot = (await CreateHallWindowAndGetSlotsAsync(hallId, admin))[0];
        var bind = await PutAuthAsync(
            $"/api/v1/admin/speaker-meeting-requests/{created.Id}/respond",
            new RespondToSpeakerMeetingRequestRequest
            {
                Status = MeetingRequestStatus.Accepted,
                HallId = hallId, SlotStart = slot.Start, SlotEnd = slot.End,
            }, admin);
        Assert.Equal(HttpStatusCode.OK, bind.StatusCode);

        // The speaker's email is cleared after the approve (e.g. an admin edit).
        await ClearSpeakerEmailAsync(speaker.Id);

        var resend = await PostAuthAsync(
            $"/api/v1/admin/speaker-meeting-requests/{created.Id}/resend-confirmation",
            new { }, admin);
        Assert.Equal(HttpStatusCode.Conflict, resend.StatusCode);
        var body = (await resend.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SpeakerMeetingContactMissing, body.Error!.Code);
    }

    // -- A22: the requester is emailed the speaker's own decision -----------------

    [Fact]
    public async Task A22_The_speakers_own_approval_emails_the_requester_not_just_an_in_app_row()
    {
        // The whole speaker lifecycle end-to-end: approve-with-hall emails the speaker
        // a real link (A24/A25 happy path), and consuming that link tells the requester
        // by EMAIL as well as in-app. Before the fix the speaker's Approve / Decline
        // set SendEmail = false, so a requester who was not in the app learned nothing.
        var speaker = await SeedSpeakerAsync(email: "a22-speaker@simf.test");
        var (visitor, visitorId) = await SignInEligibleVisitorWithIdAsync();
        var requesterEmail = await ResolveEmailAsync(visitorId);
        var created = await SubmitAsync(speaker.Id, visitor);
        var admin = await CreateAdministratorAndSignInAsync();

        var hallId = await SeedMeetingHallAsync();
        var slot = (await CreateHallWindowAndGetSlotsAsync(hallId, admin))[0];
        var bind = await PutAuthAsync(
            $"/api/v1/admin/speaker-meeting-requests/{created.Id}/respond",
            new RespondToSpeakerMeetingRequestRequest
            {
                Status = MeetingRequestStatus.Accepted,
                HallId = hallId, SlotStart = slot.Start, SlotEnd = slot.End,
            }, admin);
        Assert.Equal(HttpStatusCode.OK, bind.StatusCode);

        // The speaker really was emailed a usable link.
        var speakerMail = Assert.Single(_factory.Emails.Messages,
            m => m.To == "a22-speaker@simf.test");
        var approveToken = ExtractFirstToken(speakerMail.HtmlBody);
        Assert.False(string.IsNullOrWhiteSpace(approveToken));

        // The speaker approves from their inbox (anonymous, single-use).
        var apply = await _client.PostAsync(
            $"/api/v1/app/meeting-actions/{Uri.EscapeDataString(approveToken)}", null);
        Assert.Equal(HttpStatusCode.OK, apply.StatusCode);

        // The requester is emailed the outcome, not only shown an in-app row.
        Assert.Contains(_factory.Emails.Messages, m => m.To == requesterEmail);

        using var scope = _factory.Services.CreateScope();
        var identityDb = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var confirmed = await identityDb.Notifications
            .AnyAsync(n => n.UserId == visitorId
                && n.RelatedEntityId == created.Id
                && n.Kind == NotificationKind.MeetingRequestConfirmed);
        Assert.True(confirmed);
    }

    // -- B12: the check-in must reach the requester ------------------------------

    [Fact]
    public async Task B12_Check_in_notifies_the_requester_and_surfaces_as_CheckedIn_on_their_feed()
    {
        // Before the fix CheckInAsync set Status/CheckedInAt/CheckedInByUserId and
        // nothing else: no notification, and the feed folded Done back to Accepted
        // with no way to tell the two apart, so the requester never learned anything.
        var speaker = await SeedSpeakerAsync(email: "checkin-speaker@simf.test");
        var (visitor, visitorId) = await SignInEligibleVisitorWithIdAsync();
        var created = await SubmitAsync(speaker.Id, visitor);
        var admin = await CreateAdministratorAndSignInAsync();

        // Straight to Accepted (no hall) so the row is check-in-able.
        var accept = await PutAuthAsync(
            $"/api/v1/admin/speaker-meeting-requests/{created.Id}/respond",
            new RespondToSpeakerMeetingRequestRequest
            {
                Status = MeetingRequestStatus.Accepted,
            }, admin);
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);

        var checkIn = await PostAuthAsync(
            $"/api/v1/admin/speaker-meeting-requests/{created.Id}/check-in", new { }, admin);
        Assert.Equal(HttpStatusCode.OK, checkIn.StatusCode);

        // The requester is told, in the Meetings group.
        using (var scope = _factory.Services.CreateScope())
        {
            var identityDb = scope.ServiceProvider
                .GetRequiredService<SimfIdentityDbContext>();
            // The accept already produced one row, and the test clock is frozen, so
            // match on the check-in notice itself rather than on "the newest row".
            var rows = await identityDb.Notifications
                .Where(n => n.UserId == visitorId && n.RelatedEntityId == created.Id)
                .ToListAsync();
            var notification = Assert.Single(rows,
                n => n.Title.Contains("attendance", StringComparison.OrdinalIgnoreCase));
            Assert.False(string.IsNullOrWhiteSpace(notification.TitleArabic));
        }

        // And their own feed now says "checked in" rather than a plain accepted row.
        var mine = await GetAuthAsync("/api/v1/app/my-requests", visitor);
        Assert.Equal(HttpStatusCode.OK, mine.StatusCode);
        var items = (await mine.Content
            .ReadFromJsonAsync<ApiResult<List<AppRequestItem>>>())!.Data!;
        var row = Assert.Single(items, i => i.Id == created.Id);
        Assert.True(row.CheckedIn);
        // The shipped wire contract is preserved — Done still folds onto Accepted.
        Assert.Equal(MeetingRequestStatus.Accepted, row.Status);
    }

    // -- B13: cancelling voids the speaker's links and tells them ----------------

    [Fact]
    public async Task B13_Cancelling_voids_the_live_speaker_tokens_and_emails_the_speaker()
    {
        var speaker = await SeedSpeakerAsync(email: "cancel-speaker@simf.test");
        var visitor = await SignInEligibleVisitorAsync();
        var created = await SubmitAsync(speaker.Id, visitor);
        var admin = await CreateAdministratorAndSignInAsync();

        var hallId = await SeedMeetingHallAsync();
        var slot = (await CreateHallWindowAndGetSlotsAsync(hallId, admin))[0];
        var bind = await PutAuthAsync(
            $"/api/v1/admin/speaker-meeting-requests/{created.Id}/respond",
            new RespondToSpeakerMeetingRequestRequest
            {
                Status = MeetingRequestStatus.Accepted,
                HallId = hallId, SlotStart = slot.Start, SlotEnd = slot.End,
            }, admin);
        Assert.Equal(HttpStatusCode.OK, bind.StatusCode);

        var cancel = await PostAuthAsync(
            "/api/v1/app/my-requests/cancel",
            new CancelMyRequestBody
            {
                Kind = AppRequestKind.SpeakerMeeting,
                Id = created.Id,
            }, visitor);
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var tokens = await db.MeetingActionTokens
            .Where(t => t.SpeakerMeetingRequestId == created.Id)
            .ToListAsync();
        Assert.NotEmpty(tokens);
        // Every emailed link is dead — not merely inert because of a status re-check.
        Assert.All(tokens, t => Assert.NotNull(t.UsedAt));

        Assert.Contains(_factory.Emails.Messages,
            m => m.To == "cancel-speaker@simf.test"
                && m.Subject.Contains("withdrawn", StringComparison.OrdinalIgnoreCase));
    }

    // -- B20: a rejected / cancelled request can be reopened ---------------------

    [Fact]
    public async Task B20_Reopening_a_rejected_request_puts_it_back_to_Pending_and_is_audited()
    {
        var speaker = await SeedSpeakerAsync(email: "reopen-speaker@simf.test");
        var visitor = await SignInEligibleVisitorAsync();
        var created = await SubmitAsync(speaker.Id, visitor);
        var (admin, adminId) = await CreateAdministratorAndSignInWithIdAsync();

        var decline = await PutAuthAsync(
            $"/api/v1/admin/speaker-meeting-requests/{created.Id}/respond",
            new RespondToSpeakerMeetingRequestRequest
            {
                Status = MeetingRequestStatus.Rejected,
                ResponseNote = "Declined by mistake.",
            }, admin);
        Assert.Equal(HttpStatusCode.OK, decline.StatusCode);

        var reopen = await PostAuthAsync(
            $"/api/v1/admin/speaker-meeting-requests/{created.Id}/reopen", new { }, admin);
        Assert.Equal(HttpStatusCode.OK, reopen.StatusCode);
        var detail = (await reopen.Content
            .ReadFromJsonAsync<ApiResult<AdminSpeakerMeetingRequestDetail>>())!.Data!;
        Assert.Equal(MeetingRequestStatus.Pending, detail.Status);
        Assert.Null(detail.RespondedAt);
        Assert.Null(detail.ResponseNote);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var audited = await db.OperationLog.AnyAsync(e =>
            e.EventType == AuditEvents.SpeakerMeetingRequestReopened
            && e.ActorUserId == adminId
            && e.Detail!.Contains(created.Id.ToString()));
        Assert.True(audited);
    }

    [Fact]
    public async Task B20_Reopening_an_accepted_request_is_409()
    {
        // Accepted / AwaitingSpeaker / Done all HOLD a slot; reopening one would free
        // a booked hall slot behind the parties' backs, so those stay one-way.
        var speaker = await SeedSpeakerAsync(email: "reopen-guard@simf.test");
        var visitor = await SignInEligibleVisitorAsync();
        var created = await SubmitAsync(speaker.Id, visitor);
        var admin = await CreateAdministratorAndSignInAsync();

        var accept = await PutAuthAsync(
            $"/api/v1/admin/speaker-meeting-requests/{created.Id}/respond",
            new RespondToSpeakerMeetingRequestRequest
            {
                Status = MeetingRequestStatus.Accepted,
            }, admin);
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);

        var reopen = await PostAuthAsync(
            $"/api/v1/admin/speaker-meeting-requests/{created.Id}/reopen", new { }, admin);
        Assert.Equal(HttpStatusCode.Conflict, reopen.StatusCode);
        var body = (await reopen.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SpeakerMeetingRequestStatusInvalid, body.Error!.Code);
    }

    [Fact]
    public async Task B20_Reopen_requires_the_manage_permission()
    {
        var visitor = await SignInEligibleVisitorAsync();
        var reopen = await PostAuthAsync(
            $"/api/v1/admin/speaker-meeting-requests/{Guid.NewGuid()}/reopen",
            new { }, visitor);
        Assert.Equal(HttpStatusCode.Forbidden, reopen.StatusCode);
    }

    // -- A29: the AwaitingSpeaker -> Pending revert tells the requester ----------

    [Fact]
    public async Task A29_The_expiry_revert_notifies_the_requester()
    {
        var now = DateTimeOffset.UtcNow;
        // A REAL requester account — the notification row is written to the Identity
        // DB against this user, so a synthetic Guid would be silently dropped.
        var (_, requesterId) = await SignInEligibleVisitorWithIdAsync();
        var requestId = await SeedExpiredAwaitingRequestAsync(now, requesterId);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var auditLog = scope.ServiceProvider.GetRequiredService<IAuditLog>();
        var notifications = scope.ServiceProvider
            .GetRequiredService<INotificationDispatcher>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger(nameof(SpeakerMeetingQaTests));

        var reverted = await MeetingAwaitingSpeakerExpiryWorker.RunExpiryScanAsync(
            db, auditLog, notifications, logger, now, CancellationToken.None);
        Assert.Equal(1, reverted);

        var identityDb = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var notification = await identityDb.Notifications
            .Where(n => n.UserId == requesterId && n.RelatedEntityId == requestId)
            .SingleOrDefaultAsync();
        Assert.NotNull(notification);
        Assert.False(string.IsNullOrWhiteSpace(notification!.TitleArabic));
    }

    // -- Helpers ----------------------------------------------------------------

    // Pull the first `token=…` value out of an emailed HTML link.
    private static string ExtractFirstToken(string html)
    {
        const string marker = "token=";
        var start = html.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0, "The speaker email carried no action link.");
        start += marker.Length;
        var end = html.IndexOfAny(new[] { '"', '&', '<', ' ' }, start);
        var raw = end < 0 ? html[start..] : html[start..end];
        return Uri.UnescapeDataString(raw);
    }

    private async Task<string> ResolveEmailAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var user = await users.FindByIdAsync(userId.ToString())
            ?? throw new InvalidOperationException($"User {userId} was not found.");
        return user.Email!;
    }

    private async Task<Guid> SeedExpiredAwaitingRequestAsync(
        DateTimeOffset now, Guid requesterId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var speaker = new Speaker
        {
            Id = Guid.NewGuid(),
            Code = "SPK" + suffix,
            Name = "Expiry Speaker", NameArabic = "متحدّث",
            IsActive = true, AllowsMeetingRequests = true,
            CreatedAt = now,
        };
        db.Speakers.Add(speaker);
        var req = new SpeakerMeetingRequest
        {
            Id = Guid.NewGuid(),
            SpeakerId = speaker.Id,
            RequestedByUserId = requesterId,
            RequesterName = "Requester", Subject = "Topic",
            Status = MeetingRequestStatus.AwaitingSpeaker,
            SlotStart = now.AddDays(3), SlotEnd = now.AddDays(3).AddMinutes(30),
            RespondedAt = now, CreatedAt = now,
        };
        db.SpeakerMeetingRequests.Add(req);
        db.MeetingActionTokens.Add(new MeetingActionToken
        {
            Id = Guid.NewGuid(),
            SpeakerMeetingRequestId = req.Id,
            Action = MeetingActionType.Approve,
            TokenHash = "qa-hash-" + Guid.NewGuid().ToString("N"),
            Expires = now.AddHours(-1),
            CreatedAt = now.AddHours(-73),
        });
        await db.SaveChangesAsync();
        return req.Id;
    }

    private async Task ClearSpeakerEmailAsync(Guid speakerId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var speaker = await db.Speakers.SingleAsync(s => s.Id == speakerId);
        speaker.Email = null;
        await db.SaveChangesAsync();
    }

    private async Task<Speaker> SeedSpeakerAsync(string? email)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var speaker = new Speaker
        {
            Id = Guid.NewGuid(),
            Code = "SPK-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Capt. QA Speaker", NameArabic = "متحدّث الاختبار",
            Rank = "Naval Captain",
            Email = email,
            AllowsMeetingRequests = true,
            IsActive = true,
            DisplayOrder = 0,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Speakers.Add(speaker);
        await db.SaveChangesAsync();
        return speaker;
    }

    private async Task<Guid> SeedMeetingHallAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var hall = new Hall
        {
            Id = Guid.NewGuid(),
            Code = "MH-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Meeting Hall", NameArabic = "قاعة الاجتماعات",
            Purpose = HallPurpose.Meeting, Capacity = 10, IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Halls.Add(hall);
        await db.SaveChangesAsync();
        return hall.Id;
    }

    private static DateTimeOffset _hallWindowCursor =
        new(2032, 3, 1, 9, 0, 0, TimeSpan.Zero);

    private async Task<IReadOnlyList<HallAvailableSlot>> CreateHallWindowAndGetSlotsAsync(
        Guid hallId, string admin)
    {
        // Each hall gets its own window in a distinct hour so two tests in this class
        // never contend for the same slot instant.
        var start = _hallWindowCursor;
        _hallWindowCursor = _hallWindowCursor.AddHours(4);

        var create = await PostAuthAsync(
            $"/api/v1/admin/halls/{hallId}/availability-windows",
            new CreateHallAvailabilityWindowRequest
            {
                Start = start, End = start.AddMinutes(60), SlotMinutes = 30,
            }, admin);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var slots = await GetAuthAsync($"/api/v1/admin/halls/{hallId}/available-slots", admin);
        var list = (await slots.Content
            .ReadFromJsonAsync<ApiResult<IReadOnlyList<HallAvailableSlot>>>())!.Data!;
        Assert.NotEmpty(list);
        return list;
    }

    private async Task<SpeakerMeetingRequestSubmitted> SubmitAsync(Guid speakerId, string token)
    {
        var submit = await PostAuthAsync(
            $"/api/v1/app/speakers/{speakerId}/meeting-requests",
            new SubmitSpeakerMeetingRequestRequest
            {
                RequesterName = "QA Requester",
                Subject = "Maritime cooperation",
            }, token);
        Assert.Equal(HttpStatusCode.OK, submit.StatusCode);
        return (await submit.Content
            .ReadFromJsonAsync<ApiResult<SpeakerMeetingRequestSubmitted>>())!.Data!;
    }

    private async Task<string> SignInEligibleVisitorAsync()
    {
        var (token, _) = await SignInEligibleVisitorWithIdAsync();
        return token;
    }

    private async Task<(string Token, Guid UserId)> SignInEligibleVisitorWithIdAsync()
    {
        var email = $"smq-visitor-{Guid.NewGuid():N}@simf.test";
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
        var userId = await GrantSpeakerMeetingAccessAsync(email);
        var sign = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest { Email = email, Password = AuthFlow.Password });
        var body = (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        return (body.Data!.Tokens!.AccessToken, userId);
    }

    // Bi-Meeting rework — eligibility is the per-user UserProfile.AllowsSpeakerMeeting
    // flag, so the QA requester needs a profile carrying it.
    private async Task<Guid> GrantSpeakerMeetingAccessAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var user = await users.FindByEmailAsync(email)
            ?? throw new InvalidOperationException($"User {email} was not found.");
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var profileType = await appDb.ProfileTypes
            .FirstOrDefaultAsync(p => p.IsForVisitor);
        if (profileType is null)
        {
            profileType = new UserProfileType
            {
                Id = Guid.NewGuid(),
                Name = "QA Visitor", NameArabic = "زائر", PageColor = "#FFD700",
                IsForVisitor = true, IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            appDb.ProfileTypes.Add(profileType);
        }
        var profile = await appDb.UserProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id);
        if (profile is null)
        {
            appDb.UserProfiles.Add(new UserProfile
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                ProfileTypeId = profileType.Id,
                AllowsSpeakerMeeting = true,
                Name = "QA Requester", NameArabic = "مقدّم الطلب",
                CreatedAt = DateTimeOffset.UtcNow,
            });
        }
        else
        {
            profile.ProfileTypeId = profileType.Id;
            profile.AllowsSpeakerMeeting = true;
        }
        await appDb.SaveChangesAsync();
        return user.Id;
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var (token, _) = await CreateAdministratorAndSignInWithIdAsync();
        return token;
    }

    private async Task<(string Token, Guid UserId)> CreateAdministratorAndSignInWithIdAsync()
    {
        var email = $"smq-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "SMQ Admin",
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
