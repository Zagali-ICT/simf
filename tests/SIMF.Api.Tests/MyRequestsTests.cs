// Tests: D-500 (Wave 5, الطلبات 1408:9726) — the unified my-requests feed
// (GET /app/my-requests) + the self-cancel (POST /app/my-requests/cancel):
// MyRequestsService + MyRequestsEndpoints. Covers the surfaced seat-booking rows.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Requests;
using SIMF.Domain.BusinessMeetings;
using SIMF.Domain.Programme;
using SIMF.Domain.SeatReservations;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>D-500 — the unified requests feed lists the signed-in user's requests
/// across every kind (here: document + badge + a surfaced seat booking); the user
/// can self-cancel a still-pending one of their own.</summary>
public sealed class MyRequestsTests : IClassFixture<SimfApiFactory>
{
    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public MyRequestsTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task My_requests_requires_authentication()
    {
        var response = await _client.GetAsync("/api/v1/app/my-requests");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Lists_the_users_submitted_requests_as_cancellable_pending()
    {
        var (token, _) = await SignInApprovedVisitorAsync();
        await SubmitDocumentAsync(token, documentType: 0);
        await SubmitBadgeAsync(token, "Director of Operations");

        var items = await GetMyRequestsAsync(token);

        var doc = Assert.Single(items, i => i.Kind == AppRequestKind.ParticipationDocument);
        Assert.Equal(MeetingRequestStatus.Pending, doc.Status);
        Assert.True(doc.CanCancel);

        var badge = Assert.Single(items, i => i.Kind == AppRequestKind.BadgeUpdate);
        Assert.Equal("Director of Operations", badge.Title);
        Assert.True(badge.CanCancel);
    }

    [Fact]
    public async Task Surfaces_a_seat_booking_as_session_attendance()
    {
        var (token, userId) = await SignInApprovedVisitorAsync();
        await SeedBookingAsync(userId, BookingStatus.Approved);

        var items = await GetMyRequestsAsync(token);

        var booking = Assert.Single(items, i => i.Kind == AppRequestKind.SessionAttendance);
        // Approved booking maps to the unified Accepted status; bookings cancel
        // from the join-session flow, not here.
        Assert.Equal(MeetingRequestStatus.Accepted, booking.Status);
        Assert.False(booking.CanCancel);
    }

    [Fact]
    public async Task Speaker_meeting_carries_the_speaker_rank_as_subtitle()
    {
        var (token, userId) = await SignInApprovedVisitorAsync();
        await SeedSpeakerMeetingAsync(userId, rank: "باحث بيئي");

        var items = await GetMyRequestsAsync(token);

        // D-590 — the المقابلات card's secondary line comes from the speaker's
        // Rank, resolved via the join (no new column). The other kinds leave it
        // null so the app falls back to the meeting-type headline.
        var meeting = Assert.Single(items, i => i.Kind == AppRequestKind.SpeakerMeeting);
        Assert.Equal("باحث بيئي", meeting.Subtitle);
    }

    [Fact]
    public async Task A_speaker_with_no_rank_leaves_the_subtitle_null()
    {
        var (token, userId) = await SignInApprovedVisitorAsync();
        await SeedSpeakerMeetingAsync(userId, rank: null);

        var items = await GetMyRequestsAsync(token);

        var meeting = Assert.Single(items, i => i.Kind == AppRequestKind.SpeakerMeeting);
        Assert.Null(meeting.Subtitle);
    }

    [Fact]
    public async Task Cancel_sets_a_pending_request_to_cancelled()
    {
        var (token, _) = await SignInApprovedVisitorAsync();
        await SubmitDocumentAsync(token, documentType: 0);
        var item = Assert.Single(await GetMyRequestsAsync(token));

        var cancel = await CancelAsync(token, AppRequestKind.ParticipationDocument, item.Id);
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);

        var after = Assert.Single(await GetMyRequestsAsync(token));
        Assert.Equal(MeetingRequestStatus.Cancelled, after.Status);
        Assert.False(after.CanCancel);
    }

    [Fact]
    public async Task Cancelling_a_non_pending_request_is_a_conflict()
    {
        var (token, _) = await SignInApprovedVisitorAsync();
        await SubmitDocumentAsync(token, documentType: 0);
        var item = Assert.Single(await GetMyRequestsAsync(token));
        await CancelAsync(token, AppRequestKind.ParticipationDocument, item.Id);

        var again = await CancelAsync(token, AppRequestKind.ParticipationDocument, item.Id);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task Cancelling_another_users_request_is_not_found()
    {
        var (tokenA, _) = await SignInApprovedVisitorAsync();
        await SubmitDocumentAsync(tokenA, documentType: 0);
        var item = Assert.Single(await GetMyRequestsAsync(tokenA));

        var (tokenB, _) = await SignInApprovedVisitorAsync();
        var cancel = await CancelAsync(tokenB, AppRequestKind.ParticipationDocument, item.Id);
        Assert.Equal(HttpStatusCode.NotFound, cancel.StatusCode);
    }

    [Fact]
    public async Task Cancelling_a_non_cancellable_kind_is_a_conflict()
    {
        var (token, _) = await SignInApprovedVisitorAsync();
        var cancel = await CancelAsync(token, AppRequestKind.DelegationMeeting, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Conflict, cancel.StatusCode);
    }

    // -- helpers --------------------------------------------------------------

    private async Task<List<AppRequestItem>> GetMyRequestsAsync(string token)
    {
        var response = await SendAuthAsync(HttpMethod.Get, "/api/v1/app/my-requests", token, null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<List<AppRequestItem>>>())!;
        return body.Data!;
    }

    private Task<HttpResponseMessage> SubmitDocumentAsync(string token, int documentType, string? note = null) =>
        SendAuthAsync(HttpMethod.Post, "/api/v1/app/document-requests", token,
            new { documentType, note });

    private Task<HttpResponseMessage> SubmitBadgeAsync(string token, string requestedJobTitle) =>
        SendAuthAsync(HttpMethod.Post, "/api/v1/app/badge-requests", token,
            new { requestedJobTitle });

    private Task<HttpResponseMessage> CancelAsync(string token, AppRequestKind kind, Guid id) =>
        SendAuthAsync(HttpMethod.Post, "/api/v1/app/my-requests/cancel", token,
            new { kind = (int)kind, id });

    private async Task SeedSpeakerMeetingAsync(Guid userId, string? rank)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var speaker = new Speaker
        {
            Id = Guid.NewGuid(),
            Code = "SPK" + suffix,
            Name = "Dr Ibrahim Al-Hamed",
            NameArabic = "د. ابراهيم الحامد",
            Rank = rank,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Speakers.Add(speaker);
        db.SpeakerMeetingRequests.Add(new SpeakerMeetingRequest
        {
            Id = Guid.NewGuid(),
            SpeakerId = speaker.Id,
            RequestedByUserId = userId,
            RequesterName = "Test Requester",
            Subject = "Cooperation on marine research",
            Status = MeetingRequestStatus.Accepted,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    private async Task SeedBookingAsync(Guid userId, BookingStatus status)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var hall = new Hall
        {
            Id = Guid.NewGuid(),
            Code = "H" + suffix,
            Name = "Main Hall",
            NameArabic = "القاعة الرئيسية",
            Capacity = 100,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Halls.Add(hall);
        var session = new Session
        {
            Id = Guid.NewGuid(),
            Code = "S" + suffix,
            Title = "Vision 2030 session",
            TitleArabic = "جلسة رؤية 2030",
            HallId = hall.Id,
            StartUtc = DateTimeOffset.UtcNow.AddDays(1),
            EndUtc = DateTimeOffset.UtcNow.AddDays(1).AddHours(1),
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Sessions.Add(session);
        db.SeatReservations.Add(new SeatReservation
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            Kind = SeatReservationKind.OpenSeating,
            ReservedForUserId = userId,
            CreatedByUserId = userId,
            Status = status,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    private async Task<(string Token, Guid UserId)> SignInApprovedVisitorAsync()
    {
        var email = $"req-visitor-{Guid.NewGuid():N}@simf.test";
        await _client.PostAsJsonAsync("/api/v1/app/auth/sign-up",
            new SignUpRequest { Email = email, Password = AuthFlow.Password, ConfirmPassword = AuthFlow.Password });
        await _client.PostAsJsonAsync("/api/v1/app/auth/verify-email",
            new VerifyEmailRequest
            {
                Email = email,
                Code = AuthFlow.GetActiveCode(_factory, email, AccountCodePurpose.EmailVerification),
            });
        AuthFlow.SetAccountState(_factory, email, AccountState.Approved);
        AuthFlow.DisableTwoFactor(_factory, email);
        var sign = await _client.PostAsJsonAsync("/api/v1/app/auth/sign-in",
            new SignInRequest { Email = email, Password = AuthFlow.Password });
        var token = (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!
            .Data!.Tokens!.AccessToken;

        Guid userId;
        using (var scope = _factory.Services.CreateScope())
        {
            var idDb = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
            userId = await idDb.Users.Where(u => u.Email == email).Select(u => u.Id).SingleAsync();
        }
        return (token, userId);
    }

    private Task<HttpResponseMessage> SendAuthAsync(
        HttpMethod method, string url, string token, object? body)
    {
        var request = new HttpRequestMessage(method, url);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, body.GetType());
        }
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
