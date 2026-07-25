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
    public async Task Respond_with_an_over_length_response_note_is_400_not_a_false_slot_conflict()
    {
        // #9 regression — ResponseNote maps to nvarchar(2000). A note longer than
        // 2000 chars must be rejected up front as a clean 400 validation error, NOT
        // fall through to SaveChanges and surface as a misleading "That slot is no
        // longer available." 409 (the DbUpdateException-masking-truncation bug).
        var speaker = await SeedSpeakerAsync(allowsMeetings: true);
        var visitor = await SignInApprovedVisitorAsync();
        var created = await SubmitAsync(speaker.Id, "Visitor", "Topic", visitor);
        var admin = await CreateAdministratorAndSignInAsync();

        var respond = await PutAuthAsync(
            $"/api/v1/admin/speaker-meeting-requests/{created.Id}/respond",
            new RespondToSpeakerMeetingRequestRequest
            {
                Status = MeetingRequestStatus.Rejected,
                ResponseNote = new string('x', 2001),
            }, admin);
        Assert.Equal(HttpStatusCode.BadRequest, respond.StatusCode);
        var body = (await respond.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SpeakerMeetingRequestInvalid, body.Error!.Code);
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
            .OrderByDescending(e => e.Timestamp)
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
                SlotStart = slot.Start,
                SlotEnd = slot.End,
            }, admin);
        Assert.Equal(HttpStatusCode.OK, respond.StatusCode);
        var detail = (await respond.Content
            .ReadFromJsonAsync<ApiResult<AdminSpeakerMeetingRequestDetail>>())!.Data!;
        Assert.Equal(MeetingRequestStatus.AwaitingSpeaker, detail.Status);
        Assert.Equal(hallId, detail.HallId);
        Assert.Equal(slot.Start, detail.SlotStart);
        Assert.Equal(slot.End, detail.SlotEnd);
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
                HallId = hallId, SlotStart = slot.Start, SlotEnd = slot.End,
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
                HallId = hallId, SlotStart = slot.Start, SlotEnd = slot.End,
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
        // speaker's calendar via the same SlotStart/End columns), not only
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

    [Fact]
    public async Task Accepting_a_slot_that_overlaps_a_different_start_meeting_the_speaker_already_holds_is_409()
    {
        // R-1 FIX #22 regression — the speaker double-book re-check must reject an
        // overlap even when the two slots START at DIFFERENT times. The frozen
        // (SpeakerId, SlotStart) unique index can only catch an equal-start
        // collision, so for two staggered overlapping windows (e.g. 10:00-11:00 vs
        // 10:30-11:30) the half-open interval re-check in the accept path
        // (SpeakerHasOverlappingMeetingAsync) is the sole backstop. Both rows carry
        // distinct requesters, so it is the SPEAKER overlap guard — not the M-7
        // requester guard — that fires.
        var speaker = await SeedSpeakerAsync(allowsMeetings: true);
        var admin = await CreateAdministratorAndSignInAsync();

        var baseStart = new DateTimeOffset(2031, 8, 1, 10, 0, 0, TimeSpan.Zero);

        // R1 already holds the speaker's 10:00-11:00 slot as a live Accepted meeting.
        await SeedSpeakerRequestAsync(
            speaker.Id, MeetingRequestStatus.Accepted,
            baseStart, baseStart.AddHours(1));
        // R2 is a Pending request for a DIFFERENT start (10:30-11:30) that overlaps
        // R1 by 30 minutes — different SlotStart, so the DB index cannot catch it.
        var r2 = await SeedSpeakerRequestAsync(
            speaker.Id, MeetingRequestStatus.Pending,
            baseStart.AddMinutes(30), baseStart.AddMinutes(90));

        var respond = await PutAuthAsync(
            $"/api/v1/admin/speaker-meeting-requests/{r2}/respond",
            new RespondToSpeakerMeetingRequestRequest { Status = MeetingRequestStatus.Accepted },
            admin);
        Assert.Equal(HttpStatusCode.Conflict, respond.StatusCode);
        var body = (await respond.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SpeakerMeetingRequestInvalid, body.Error!.Code);
    }

    [Fact]
    public async Task Two_concurrent_accepts_of_overlapping_different_start_slots_for_one_speaker_yield_one_200_and_one_409()
    {
        // R-1 FIX #22 regression (CONCURRENT) — two Pending requests for the SAME
        // speaker whose slots OVERLAP but START at DIFFERENT times (10:00-11:00 vs
        // 10:30-11:30) are accepted at the same instant. The sequential re-check cannot
        // catch this (both rows are still Pending when each check runs) and the frozen
        // (SpeakerId, SlotStart) unique index cannot catch a different-start overlap,
        // so the Serializable accept transaction is the sole arbiter: exactly one accept
        // must commit (200) and the other must lose the race (409). Before the fix both
        // slipped through and double-booked the speaker (two 200s).
        var speaker = await SeedSpeakerAsync(allowsMeetings: true);
        var admin = await CreateAdministratorAndSignInAsync();

        var baseStart = new DateTimeOffset(2031, 9, 1, 10, 0, 0, TimeSpan.Zero);
        // Two DIFFERENT requesters (SeedSpeakerRequestAsync assigns a random requester),
        // so it is the SPEAKER overlap guard that fires — not the M-7 requester guard.
        var r1 = await SeedSpeakerRequestAsync(
            speaker.Id, MeetingRequestStatus.Pending, baseStart, baseStart.AddHours(1));
        var r2 = await SeedSpeakerRequestAsync(
            speaker.Id, MeetingRequestStatus.Pending,
            baseStart.AddMinutes(30), baseStart.AddMinutes(90));

        var accept = new RespondToSpeakerMeetingRequestRequest
        {
            Status = MeetingRequestStatus.Accepted,
        };
        var responses = await Task.WhenAll(
            PutAuthAsync($"/api/v1/admin/speaker-meeting-requests/{r1}/respond", accept, admin),
            PutAuthAsync($"/api/v1/admin/speaker-meeting-requests/{r2}/respond", accept, admin));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));

        // The loser's 409 carries the slot-conflict code (from the re-check on the
        // serialization retry, or the unique-index backstop).
        var loser = responses.Single(r => r.StatusCode == HttpStatusCode.Conflict);
        var body = (await loser.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SpeakerMeetingRequestInvalid, body.Error!.Code);

        // Exactly one row is live (Accepted); the loser stayed Pending — no double-book.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var statuses = await db.SpeakerMeetingRequests
            .Where(r => r.Id == r1 || r.Id == r2)
            .Select(r => r.Status)
            .ToListAsync();
        Assert.Equal(1, statuses.Count(s => s == MeetingRequestStatus.Accepted));
        Assert.Equal(1, statuses.Count(s => s == MeetingRequestStatus.Pending));
    }

    // -- R-1b: resend the speaker confirmation links --------------------------

    [Fact]
    public async Task Resend_confirmation_for_an_AwaitingSpeaker_request_invalidates_old_tokens_mints_a_fresh_pair_and_keeps_AwaitingSpeaker()
    {
        // Accept-with-hall mints the first token pair + moves the request to
        // AwaitingSpeaker. Resend must invalidate that pair and mint a fresh one,
        // leaving exactly two still-usable tokens and the request AwaitingSpeaker.
        var speaker = await SeedSpeakerAsync(allowsMeetings: true);
        var visitor = await SignInApprovedVisitorAsync();
        var created = await SubmitAsync(speaker.Id, "Visitor", "Topic", visitor);
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

        var now = DateTimeOffset.UtcNow;
        var resend = await PostAuthAsync(
            $"/api/v1/admin/speaker-meeting-requests/{created.Id}/resend-confirmation",
            new { }, admin);
        Assert.Equal(HttpStatusCode.OK, resend.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var tokens = await db.MeetingActionTokens
            .Where(t => t.SpeakerMeetingRequestId == created.Id)
            .ToListAsync();
        // The original pair is invalidated (UsedAt stamped); a fresh unused pair minted.
        Assert.Equal(4, tokens.Count);
        Assert.Equal(2, tokens.Count(t => t.UsedAt == null && t.Expires > now));
        Assert.Equal(2, tokens.Count(t => t.UsedAt != null));

        var req = await db.SpeakerMeetingRequests.SingleAsync(r => r.Id == created.Id);
        Assert.Equal(MeetingRequestStatus.AwaitingSpeaker, req.Status);
    }

    [Fact]
    public async Task Resend_confirmation_on_a_non_AwaitingSpeaker_request_is_409()
    {
        // A plain Pending request has no confirmation flow to re-send.
        var speaker = await SeedSpeakerAsync(allowsMeetings: true);
        var visitor = await SignInApprovedVisitorAsync();
        var created = await SubmitAsync(speaker.Id, "Visitor", "Topic", visitor);
        var admin = await CreateAdministratorAndSignInAsync();

        var resend = await PostAuthAsync(
            $"/api/v1/admin/speaker-meeting-requests/{created.Id}/resend-confirmation",
            new { }, admin);
        Assert.Equal(HttpStatusCode.Conflict, resend.StatusCode);
        var body = (await resend.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SpeakerMeetingRequestStatusInvalid, body.Error!.Code);
    }

    [Fact]
    public async Task Resend_confirmation_requires_the_manage_permission()
    {
        // A signed-in non-admin visitor must be 403'd on the admin resend action.
        var visitor = await SignInApprovedVisitorAsync();
        var resend = await PostAuthAsync(
            $"/api/v1/admin/speaker-meeting-requests/{Guid.NewGuid()}/resend-confirmation",
            new { }, visitor);
        Assert.Equal(HttpStatusCode.Forbidden, resend.StatusCode);
    }

    // -- M-7: the requester cannot hold two overlapping meetings --------------

    [Fact]
    public async Task Accepting_a_second_meeting_that_overlaps_the_requesters_existing_accepted_meeting_with_another_speaker_is_409()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var speaker1 = await SeedSpeakerAsync(allowsMeetings: true);
        var speaker2 = await SeedSpeakerAsync(allowsMeetings: true);
        var requesterId = Guid.NewGuid();
        var slotStart = new DateTimeOffset(2031, 6, 1, 10, 0, 0, TimeSpan.Zero);
        var slotEnd = slotStart.AddMinutes(30);

        // The requester already holds an Accepted meeting with speaker1 at 10:00.
        await SeedSpeakerRequestForUserAsync(
            speaker1.Id, requesterId, MeetingRequestStatus.Accepted, slotStart, slotEnd);
        // A second, Pending request with speaker2 overlapping the same window.
        var second = await SeedSpeakerRequestForUserAsync(
            speaker2.Id, requesterId, MeetingRequestStatus.Pending,
            slotStart.AddMinutes(15), slotEnd.AddMinutes(15));

        var respond = await PutAuthAsync(
            $"/api/v1/admin/speaker-meeting-requests/{second}/respond",
            new RespondToSpeakerMeetingRequestRequest { Status = MeetingRequestStatus.Accepted },
            admin);
        Assert.Equal(HttpStatusCode.Conflict, respond.StatusCode);
        var body = (await respond.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SpeakerMeetingRequestInvalid, body.Error!.Code);
    }

    [Fact]
    public async Task Accepting_with_hall_when_the_requester_already_holds_an_overlapping_meeting_is_409()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var hallId = await SeedMeetingHallAsync();
        var slot = (await CreateHallWindowAndGetSlotsAsync(hallId, admin))[0];

        var speaker1 = await SeedSpeakerAsync(allowsMeetings: true);
        var speaker2 = await SeedSpeakerAsync(allowsMeetings: true);
        var requesterId = Guid.NewGuid();

        // The requester already holds an Accepted meeting with speaker1 on the same slot.
        await SeedSpeakerRequestForUserAsync(
            speaker1.Id, requesterId, MeetingRequestStatus.Accepted, slot.Start, slot.End);
        // A second Pending request with speaker2 — accepted WITH the same hall slot.
        var second = await SeedSpeakerRequestForUserAsync(
            speaker2.Id, requesterId, MeetingRequestStatus.Pending, null, null);

        var respond = await PutAuthAsync(
            $"/api/v1/admin/speaker-meeting-requests/{second}/respond",
            new RespondToSpeakerMeetingRequestRequest
            {
                Status = MeetingRequestStatus.Accepted,
                HallId = hallId, SlotStart = slot.Start, SlotEnd = slot.End,
            }, admin);
        Assert.Equal(HttpStatusCode.Conflict, respond.StatusCode);
        var body = (await respond.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SpeakerMeetingRequestInvalid, body.Error!.Code);
    }

    [Fact]
    public async Task Two_non_overlapping_meetings_for_the_same_requester_are_both_accepted()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var speaker1 = await SeedSpeakerAsync(allowsMeetings: true);
        var speaker2 = await SeedSpeakerAsync(allowsMeetings: true);
        var requesterId = Guid.NewGuid();
        var slotStart = new DateTimeOffset(2031, 7, 1, 10, 0, 0, TimeSpan.Zero);

        await SeedSpeakerRequestForUserAsync(
            speaker1.Id, requesterId, MeetingRequestStatus.Accepted,
            slotStart, slotStart.AddMinutes(30));
        // A non-overlapping second slot (11:00) — the requester overlap guard allows it.
        var second = await SeedSpeakerRequestForUserAsync(
            speaker2.Id, requesterId, MeetingRequestStatus.Pending,
            slotStart.AddHours(1), slotStart.AddHours(1).AddMinutes(30));

        var respond = await PutAuthAsync(
            $"/api/v1/admin/speaker-meeting-requests/{second}/respond",
            new RespondToSpeakerMeetingRequestRequest { Status = MeetingRequestStatus.Accepted },
            admin);
        Assert.Equal(HttpStatusCode.OK, respond.StatusCode);
        var detail = (await respond.Content
            .ReadFromJsonAsync<ApiResult<AdminSpeakerMeetingRequestDetail>>())!.Data!;
        Assert.Equal(MeetingRequestStatus.Accepted, detail.Status);
    }

    [Fact]
    public async Task Checking_in_a_confirmed_meeting_marks_it_Done()
    {
        // Bi-Meeting rework — an operator checks a confirmed (Accepted) meeting in at
        // the hall; it flips to Done and stamps CheckedInAt / CheckedInByUserId.
        var speaker = await SeedSpeakerAsync(allowsMeetings: true);
        var reqId = await SeedSpeakerRequestAsync(
            speaker.Id, MeetingRequestStatus.Accepted,
            DateTimeOffset.UtcNow.AddHours(1), DateTimeOffset.UtcNow.AddHours(1).AddMinutes(30));
        var (admin, adminId) = await CreateAdministratorAndSignInWithIdAsync();

        var checkIn = await PostAuthAsync(
            $"/api/v1/admin/speaker-meeting-requests/{reqId}/check-in", new { }, admin);
        Assert.Equal(HttpStatusCode.OK, checkIn.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var row = await db.SpeakerMeetingRequests.SingleAsync(r => r.Id == reqId);
        Assert.Equal(MeetingRequestStatus.Done, row.Status);
        Assert.NotNull(row.CheckedInAt);
        Assert.Equal(adminId, row.CheckedInByUserId);
    }

    [Fact]
    public async Task Checking_in_a_non_confirmed_meeting_is_409()
    {
        // Only a confirmed (Accepted) meeting can be checked in — a Pending row is 409.
        var speaker = await SeedSpeakerAsync(allowsMeetings: true);
        var reqId = await SeedSpeakerRequestAsync(speaker.Id, MeetingRequestStatus.Pending);
        var admin = await CreateAdministratorAndSignInAsync();

        var checkIn = await PostAuthAsync(
            $"/api/v1/admin/speaker-meeting-requests/{reqId}/check-in", new { }, admin);
        Assert.Equal(HttpStatusCode.Conflict, checkIn.StatusCode);
        var body = (await checkIn.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.AppRequestAlreadyResponded, body.Error!.Code);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var row = await db.SpeakerMeetingRequests.SingleAsync(r => r.Id == reqId);
        Assert.Equal(MeetingRequestStatus.Pending, row.Status);
    }

    // -- Helpers --------------------------------------------------------------

    // M-7 — seed a SpeakerMeetingRequest for a SPECIFIC requester (so two rows can
    // share one requester and exercise the requester-overlap guard).
    private async Task<Guid> SeedSpeakerRequestForUserAsync(
        Guid speakerId, Guid requesterUserId, MeetingRequestStatus status,
        DateTimeOffset? slotStart, DateTimeOffset? slotEnd)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var req = new SpeakerMeetingRequest
        {
            Id = Guid.NewGuid(),
            SpeakerId = speakerId,
            RequestedByUserId = requesterUserId,
            RequesterName = "Seed", Subject = "Seed",
            SlotStart = slotStart, SlotEnd = slotEnd,
            Status = status,
            CreatedAt = DateTimeOffset.UtcNow,
            RespondedAt = status == MeetingRequestStatus.Pending ? null : DateTimeOffset.UtcNow,
        };
        db.SpeakerMeetingRequests.Add(req);
        await db.SaveChangesAsync();
        return req.Id;
    }

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
            SlotStart = slotStart, SlotEnd = slotEnd,
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
                Start = HallWindowStart, End = HallWindowStart.AddMinutes(60), SlotMinutes = 30,
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

    // Bi-Meeting rework (was D-729 VIP-only) — grant the signed-up requester the
    // per-user UserProfile.AllowsSpeakerMeeting flag (admin-assigned in prod) so the
    // speaker-meeting eligibility gate lets the flow tests through. Still assigns a
    // VIP tier too (harmless; some assertions read the tier). The dedicated
    // eligibility-gate coverage lives in SpeakerMeetingVipSlotTests.
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
                AllowsSpeakerMeeting = true,
                Name = "SMR Visitor", NameArabic = "زائر",
                CreatedAt = DateTimeOffset.UtcNow,
            });
        }
        else
        {
            profile.ProfileTypeId = vipType.Id;
            profile.AllowsSpeakerMeeting = true;
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
