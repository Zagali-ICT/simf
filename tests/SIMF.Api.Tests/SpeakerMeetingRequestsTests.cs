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
using SIMF.Domain.BusinessMeetings;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Profiles;
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

    [Fact]
    public async Task Accept_with_a_hall_binds_the_slot_and_awaits_the_speaker()
    {
        // D-716 (GAP-2) — accepting with a hall + free slot binds the meeting and
        // moves the request to AwaitingSpeaker (Option A: the hall slot is the time).
        var speaker = await SeedSpeakerAsync(allowsMeetings: true);
        var visitor = await SignInApprovedVisitorAsync();
        var created = await SubmitAsync(speaker.Id, "Visitor", "Topic", visitor);
        var admin = await CreateAdministratorAndSignInAsync();

        var hallId = await SeedMeetingHallAsync();
        var slots = await CreateHallWindowAndGetSlotsAsync(hallId, admin);
        var slot = slots[0];

        var respond = await PutAuthAsync(
            $"/api/v1/admin/speaker-meeting-requests/{created.Id}/respond",
            new RespondToSpeakerMeetingRequestRequest
            {
                Status = MeetingRequestStatus.Accepted,
                HallId = hallId,
                SlotStartUtc = slot.StartUtc,
                SlotEndUtc = slot.EndUtc,
            }, admin);
        Assert.Equal(HttpStatusCode.OK, respond.StatusCode);
        var detail = (await respond.Content
            .ReadFromJsonAsync<ApiResult<AdminSpeakerMeetingRequestDetail>>())!.Data!;
        Assert.Equal(MeetingRequestStatus.AwaitingSpeaker, detail.Status);
        Assert.Equal(hallId, detail.HallId);
        Assert.Equal(slot.StartUtc, detail.SlotStartUtc);
        Assert.Equal(slot.EndUtc, detail.SlotEndUtc);
    }

    [Fact]
    public async Task Accept_with_a_hall_but_no_slot_is_400()
    {
        var speaker = await SeedSpeakerAsync(allowsMeetings: true);
        var visitor = await SignInApprovedVisitorAsync();
        var created = await SubmitAsync(speaker.Id, "Visitor", "Topic", visitor);
        var admin = await CreateAdministratorAndSignInAsync();
        var hallId = await SeedMeetingHallAsync();

        var respond = await PutAuthAsync(
            $"/api/v1/admin/speaker-meeting-requests/{created.Id}/respond",
            new RespondToSpeakerMeetingRequestRequest
            {
                Status = MeetingRequestStatus.Accepted,
                HallId = hallId, // no slot supplied
            }, admin);
        Assert.Equal(HttpStatusCode.BadRequest, respond.StatusCode);
        var body = (await respond.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SpeakerMeetingRequestInvalid, body.Error!.Code);
    }

    [Fact]
    public async Task Binding_a_hall_slot_makes_it_unavailable_to_a_second_meeting()
    {
        // D-716 (GAP-2) — a second accept onto the same hall slot (a different
        // speaker, so the speaker-busy guard is not what fires) is a 409: the
        // taken-filter removed the slot from the hall's free set.
        var admin = await CreateAdministratorAndSignInAsync();
        var hallId = await SeedMeetingHallAsync();
        var slot = (await CreateHallWindowAndGetSlotsAsync(hallId, admin))[0];

        var speaker1 = await SeedSpeakerAsync(allowsMeetings: true);
        var visitor1 = await SignInApprovedVisitorAsync();
        var req1 = await SubmitAsync(speaker1.Id, "V1", "T1", visitor1);
        var bind1 = await PutAuthAsync(
            $"/api/v1/admin/speaker-meeting-requests/{req1.Id}/respond",
            new RespondToSpeakerMeetingRequestRequest
            {
                Status = MeetingRequestStatus.Accepted,
                HallId = hallId, SlotStartUtc = slot.StartUtc, SlotEndUtc = slot.EndUtc,
            }, admin);
        Assert.Equal(HttpStatusCode.OK, bind1.StatusCode);

        var speaker2 = await SeedSpeakerAsync(allowsMeetings: true);
        var visitor2 = await SignInApprovedVisitorAsync();
        var req2 = await SubmitAsync(speaker2.Id, "V2", "T2", visitor2);
        var bind2 = await PutAuthAsync(
            $"/api/v1/admin/speaker-meeting-requests/{req2.Id}/respond",
            new RespondToSpeakerMeetingRequestRequest
            {
                Status = MeetingRequestStatus.Accepted,
                HallId = hallId, SlotStartUtc = slot.StartUtc, SlotEndUtc = slot.EndUtc,
            }, admin);
        Assert.Equal(HttpStatusCode.Conflict, bind2.StatusCode);
        var body = (await bind2.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SpeakerMeetingRequestInvalid, body.Error!.Code);
    }

    [Fact]
    public async Task Accepting_a_slot_the_speaker_already_holds_as_AwaitingSpeaker_is_409()
    {
        // D-716 regression — the legacy accept-WITHOUT-hall re-check must see a
        // hall-bound AwaitingSpeaker meeting for the same speaker (it occupies the
        // speaker's calendar via the same SlotStartUtc/EndUtc columns), not only
        // Accepted rows. Before the fix this accept slipped through and double-booked
        // the speaker.
        var speaker = await SeedSpeakerAsync(allowsMeetings: true);
        var admin = await CreateAdministratorAndSignInAsync();

        var slotStart = new DateTimeOffset(2031, 5, 1, 10, 0, 0, TimeSpan.Zero);
        var slotEnd = slotStart.AddMinutes(30);

        // R1 already holds the speaker's 10:00 slot as AwaitingSpeaker.
        await SeedSpeakerRequestAsync(
            speaker.Id, MeetingRequestStatus.AwaitingSpeaker, slotStart, slotEnd);
        // R2 is a pending VIP-style request for the SAME speaker + overlapping slot.
        var r2 = await SeedSpeakerRequestAsync(
            speaker.Id, MeetingRequestStatus.Pending, slotStart, slotEnd);

        var respond = await PutAuthAsync(
            $"/api/v1/admin/speaker-meeting-requests/{r2}/respond",
            new RespondToSpeakerMeetingRequestRequest { Status = MeetingRequestStatus.Accepted },
            admin);
        Assert.Equal(HttpStatusCode.Conflict, respond.StatusCode);
        var body = (await respond.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SpeakerMeetingRequestInvalid, body.Error!.Code);
    }

    // -- Helpers --------------------------------------------------------------

    // D-716 — seed a SpeakerMeetingRequest row directly (bypasses the submit
    // guard) to set up multi-request race/overlap scenarios.
    private async Task<Guid> SeedSpeakerRequestAsync(
        Guid speakerId, MeetingRequestStatus status,
        DateTimeOffset? slotStart = null, DateTimeOffset? slotEnd = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var req = new SpeakerMeetingRequest
        {
            Id = Guid.NewGuid(),
            SpeakerId = speakerId,
            RequestedByUserId = Guid.NewGuid(),
            RequesterName = "Seed", Subject = "Seed",
            SlotStartUtc = slotStart, SlotEndUtc = slotEnd,
            Status = status,
            CreatedAt = DateTimeOffset.UtcNow,
            RespondedAt = status == MeetingRequestStatus.Pending ? null : DateTimeOffset.UtcNow,
        };
        db.SpeakerMeetingRequests.Add(req);
        await db.SaveChangesAsync();
        return req.Id;
    }

    // D-716 — a Meeting-purpose hall for the accept-with-hall flow.
    private static readonly DateTimeOffset HallWindowStart =
        new(2031, 3, 1, 9, 0, 0, TimeSpan.Zero);

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

    private async Task<IReadOnlyList<HallAvailableSlot>> CreateHallWindowAndGetSlotsAsync(
        Guid hallId, string admin)
    {
        var create = await PostAuthAsync(
            $"/api/v1/admin/halls/{hallId}/availability-windows",
            new CreateHallAvailabilityWindowRequest
            {
                StartUtc = HallWindowStart, EndUtc = HallWindowStart.AddMinutes(60), SlotMinutes = 30,
            }, admin);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var slots = await GetAuthAsync(
            $"/api/v1/admin/halls/{hallId}/available-slots", admin);
        var list = (await slots.Content
            .ReadFromJsonAsync<ApiResult<IReadOnlyList<HallAvailableSlot>>>())!.Data!;
        Assert.NotEmpty(list);
        return list;
    }

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
        // D-729 (owner item 15) — speaker meetings are now VIP-only, so the
        // flow requester used by these submit + admin-respond tests must be a
        // VIP tier (AllowsVipMeetingSlots). The dedicated VIP-gate coverage lives
        // in SpeakerMeetingVipSlotTests.
        await AssignVipProfileAsync(email);
        var sign = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest { Email = email, Password = AuthFlow.Password });
        var body = (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        return body.Data!.Tokens!.AccessToken;
    }

    // D-729 — assign a VIP-tier profile (AllowsVipMeetingSlots) to the signed-up
    // requester, reusing the seeded VVIP/VIP type when present, so the VIP-only
    // speaker-meeting gate lets the flow tests through.
    private async Task AssignVipProfileAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var user = await users.FindByEmailAsync(email)
            ?? throw new InvalidOperationException($"User {email} was not found.");
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var vipType = await appDb.ProfileTypes
            .FirstOrDefaultAsync(p => p.AllowsVipMeetingSlots && p.IsForVisitor);
        if (vipType is null)
        {
            vipType = new UserProfileType
            {
                Id = Guid.NewGuid(),
                Name = "VIP", NameArabic = "VIP", PageColor = "#FFD700",
                IsForVisitor = true, AllowsVipMeetingSlots = true, IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            appDb.ProfileTypes.Add(vipType);
        }
        var profile = await appDb.UserProfiles
            .FirstOrDefaultAsync(p => p.UserId == user.Id);
        if (profile is null)
        {
            appDb.UserProfiles.Add(new UserProfile
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                ProfileTypeId = vipType.Id,
                Name = "SMR Visitor", NameArabic = "زائر",
                CreatedAt = DateTimeOffset.UtcNow,
            });
        }
        else
        {
            profile.ProfileTypeId = vipType.Id;
        }
        await appDb.SaveChangesAsync();
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
