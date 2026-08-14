// B1 (owner "change seat") — POST /app/sessions/{id}/seats/move.
// The move is ATOMIC: the destination is acquired and the source released in one
// unit of work, so the holder is never left seatless. These tests pin
//  * the happy path frees the old seat and holds the new one in ONE step;
//  * a move onto an occupied seat leaves the mover on their ORIGINAL seat;
//  * the seat-TIER rule (D-771) is re-run on the destination;
//  * the timing rule — a self-service move is refused once the session has started;
//  * the no-seat / same-seat guards.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Sessions;
using SIMF.Domain.Profiles;
using SIMF.Domain.Programme;
using SIMF.Domain.SeatReservations;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class SeatChangeTests : IClassFixture<SimfApiFactory>
{
    // Row A = VVIP (protocol), row B = VIP, row C = Normal — the tier fixture.
    private static readonly string[] TieredRows = ["A", "B", "C"];
    private static readonly SeatTier[] Tiers =
        [SeatTier.Vvip, SeatTier.Vip, SeatTier.Normal];

    // A plain (all-Normal) hall for the non-tier scenarios.
    private static readonly string[] PlainRows = ["A", "B"];
    private static readonly SeatTier[] PlainTiers =
        [SeatTier.Normal, SeatTier.Normal];

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public SeatChangeTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Moving_frees_the_old_seat_and_holds_the_new_one_in_one_step()
    {
        var session = await SeedSessionAsync(PlainRows, PlainTiers);
        var (token, userId) = await SignInVisitorAsync(vip: false);
        await ReserveAsync(session.Id, "A", 1, token);

        var move = await PostAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/seats/move",
            new MoveSeatRequest { RowLabel = "B", SeatNumber = 3 }, token);

        Assert.Equal(HttpStatusCode.OK, move.StatusCode);
        var mine = (await move.Content
            .ReadFromJsonAsync<ApiResult<MySeatReservation>>())!.Data!;
        Assert.Equal("B", mine.RowLabel);
        Assert.Equal(3, mine.SeatNumber);
        Assert.Equal(BookingStatus.Approved, mine.Status);

        // The grid agrees: the new seat is held by the caller and the old one is
        // free again (nobody occupies A1).
        var map = await ReadSeatMapAsync(session.Id, token);
        Assert.Equal("B", map.MyCell!.RowLabel);
        Assert.Equal(3, map.MyCell.SeatNumber);
        Assert.DoesNotContain(
            map.ReservedCells, c => c.RowLabel == "A" && c.SeatNumber == 1);

        // Exactly ONE active row for the mover, and the source row is closed off
        // properly (released + cancelled), not left as a stale "approved but gone".
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        // The hold is keyed by the mover's attendee profile, not their account.
        var profileId = await TestAttendeeProfiles.EnsureForAccountAsync(db, userId);
        var rows = await db.SeatReservations.AsNoTracking()
            .Where(r => r.SessionId == session.Id && r.ReservedForProfileId == profileId)
            .ToListAsync();
        var active = Assert.Single(rows, r => r.ReleasedAt == null);
        Assert.Equal("B", active.RowLabel);
        Assert.Equal(3, active.SeatNumber);
        var source = Assert.Single(rows, r => r.ReleasedAt != null);
        Assert.Equal("A", source.RowLabel);
        Assert.Equal(BookingStatus.Cancelled, source.Status);
    }

    [Fact]
    public async Task A_move_to_an_occupied_seat_leaves_the_original_seat_held()
    {
        // The whole point of the atomic move: losing the race for the destination
        // must NOT cost the mover the seat they already had.
        var session = await SeedSessionAsync(PlainRows, PlainTiers);
        var (mover, moverId) = await SignInVisitorAsync(vip: false);
        var (rival, _) = await SignInVisitorAsync(vip: false);
        await ReserveAsync(session.Id, "A", 1, mover);
        await ReserveAsync(session.Id, "A", 2, rival);

        var move = await PostAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/seats/move",
            new MoveSeatRequest { RowLabel = "A", SeatNumber = 2 }, mover);

        Assert.Equal(HttpStatusCode.Conflict, move.StatusCode);
        var body = (await move.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SeatAlreadyReserved, body.Error!.Code);

        var map = await ReadSeatMapAsync(session.Id, mover);
        Assert.Equal("A", map.MyCell!.RowLabel);
        Assert.Equal(1, map.MyCell.SeatNumber);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var moverProfileId = await TestAttendeeProfiles.EnsureForAccountAsync(db, moverId);
        var active = await db.SeatReservations.AsNoTracking()
            .SingleAsync(r => r.SessionId == session.Id
                && r.ReservedForProfileId == moverProfileId
                && r.ReleasedAt == null);
        Assert.Equal("A", active.RowLabel);
        Assert.Equal(1, active.SeatNumber);
        Assert.Equal(BookingStatus.Approved, active.Status);
    }

    [Fact]
    public async Task Tier_eligibility_is_enforced_on_a_move()
    {
        // A non-VIP visitor sitting on a Normal seat may not move into the VIP row
        // and may never move into the VVIP protocol row — and each refusal leaves
        // them on the seat they already hold.
        var session = await SeedSessionAsync(TieredRows, Tiers);
        var (token, _) = await SignInVisitorAsync(vip: false);
        await ReserveAsync(session.Id, "C", 1, token);

        var toVip = await PostAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/seats/move",
            new MoveSeatRequest { RowLabel = "B", SeatNumber = 1 }, token);
        Assert.Equal(HttpStatusCode.Conflict, toVip.StatusCode);
        Assert.Equal(
            ErrorCodes.SeatTierNotEligible,
            (await toVip.Content.ReadFromJsonAsync<ApiResult<object>>())!.Error!.Code);

        var toVvip = await PostAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/seats/move",
            new MoveSeatRequest { RowLabel = "A", SeatNumber = 1 }, token);
        Assert.Equal(HttpStatusCode.Conflict, toVvip.StatusCode);
        Assert.Equal(
            ErrorCodes.SeatTierReserved,
            (await toVvip.Content.ReadFromJsonAsync<ApiResult<object>>())!.Error!.Code);

        var map = await ReadSeatMapAsync(session.Id, token);
        Assert.Equal("C", map.MyCell!.RowLabel);
        Assert.Equal(1, map.MyCell.SeatNumber);
    }

    [Fact]
    public async Task A_vip_visitor_may_move_into_a_vip_seat()
    {
        var session = await SeedSessionAsync(TieredRows, Tiers);
        var (token, _) = await SignInVisitorAsync(vip: true);
        await ReserveAsync(session.Id, "C", 2, token);

        var move = await PostAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/seats/move",
            new MoveSeatRequest { RowLabel = "B", SeatNumber = 2 }, token);

        Assert.Equal(HttpStatusCode.OK, move.StatusCode);
        var mine = (await move.Content
            .ReadFromJsonAsync<ApiResult<MySeatReservation>>())!.Data!;
        Assert.Equal("B", mine.RowLabel);
        Assert.Equal(2, mine.SeatNumber);
    }

    [Fact]
    public async Task A_move_after_the_session_has_started_is_refused()
    {
        // Deliberate rule: the change window closes at Start, the same boundary the
        // cancel already enforces. A walk-in may still BOOK a live session, so the
        // reservation below succeeds and only the move is refused.
        var session = await SeedSessionAsync(PlainRows, PlainTiers, alreadyStarted: true);
        var (token, _) = await SignInVisitorAsync(vip: false);
        await ReserveAsync(session.Id, "A", 1, token);

        var move = await PostAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/seats/move",
            new MoveSeatRequest { RowLabel = "A", SeatNumber = 2 }, token);

        Assert.Equal(HttpStatusCode.Conflict, move.StatusCode);
        var body = (await move.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.BookingSessionStarted, body.Error!.Code);

        var map = await ReadSeatMapAsync(session.Id, token);
        Assert.Equal(1, map.MyCell!.SeatNumber);
    }

    [Fact]
    public async Task Moving_onto_the_seat_you_already_hold_is_refused()
    {
        var session = await SeedSessionAsync(PlainRows, PlainTiers);
        var (token, _) = await SignInVisitorAsync(vip: false);
        await ReserveAsync(session.Id, "A", 1, token);

        var move = await PostAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/seats/move",
            new MoveSeatRequest { RowLabel = "A", SeatNumber = 1 }, token);

        Assert.Equal(HttpStatusCode.Conflict, move.StatusCode);
        var body = (await move.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SeatMoveSameSeat, body.Error!.Code);
    }

    [Fact]
    public async Task Moving_without_a_held_seat_is_a_404()
    {
        var session = await SeedSessionAsync(PlainRows, PlainTiers);
        var (token, _) = await SignInVisitorAsync(vip: false);

        var move = await PostAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/seats/move",
            new MoveSeatRequest { RowLabel = "A", SeatNumber = 1 }, token);

        Assert.Equal(HttpStatusCode.NotFound, move.StatusCode);
        var body = (await move.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SeatReservationNotFound, body.Error!.Code);
    }

    [Fact]
    public async Task Moving_outside_the_layout_is_rejected_and_keeps_the_seat()
    {
        var session = await SeedSessionAsync(PlainRows, PlainTiers);
        var (token, _) = await SignInVisitorAsync(vip: false);
        await ReserveAsync(session.Id, "A", 1, token);

        var move = await PostAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/seats/move",
            new MoveSeatRequest { RowLabel = "Z", SeatNumber = 1 }, token);

        Assert.Equal(HttpStatusCode.BadRequest, move.StatusCode);
        var body = (await move.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SeatOutOfBounds, body.Error!.Code);

        var map = await ReadSeatMapAsync(session.Id, token);
        Assert.Equal("A", map.MyCell!.RowLabel);
    }

    [Fact]
    public async Task Move_requires_an_authenticated_account()
    {
        var session = await SeedSessionAsync(PlainRows, PlainTiers);

        var move = await _client.PostAsJsonAsync(
            $"/api/v1/app/sessions/{session.Id}/seats/move",
            new MoveSeatRequest { RowLabel = "A", SeatNumber = 1 });

        Assert.Equal(HttpStatusCode.Unauthorized, move.StatusCode);
    }

    // -- helpers --

    private async Task<Session> SeedSessionAsync(
        string[] rows, SeatTier[] tiers, bool alreadyStarted = false)
    {
        const int seatsPerRow = 5;
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var hall = new Hall
        {
            Id = Guid.NewGuid(),
            Code = "H-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Move hall",
            NameArabic = "قاعة",
            Capacity = rows.Length * seatsPerRow,
            SeatSelectionMode = SeatSelectionMode.AssignedSeat,
            IsActive = true,
            CreatedAt = SimfClock.Now,
        };
        db.Halls.Add(hall);
        db.HallSeatLayouts.Add(new HallSeatLayout
        {
            Id = Guid.NewGuid(),
            HallId = hall.Id,
            RowLabels = string.Join(',', rows),
            SeatsPerRow = seatsPerRow,
            SeatTiers = string.Join(',', tiers.Select(t => (int)t)),
            CreatedAt = SimfClock.Now,
        });
        // "Already started" is seeded as a LIVE session (started, not yet ended) so
        // the booking itself still succeeds and only the move hits the start gate.
        var now = _factory.Time.SimfNow();
        var session = new Session
        {
            Id = Guid.NewGuid(),
            Code = "SES-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Title = "Movable",
            TitleArabic = "قابلة للتغيير",
            HallId = hall.Id,
            Start = alreadyStarted ? now.AddMinutes(-10) : now.AddHours(1),
            End = alreadyStarted ? now.AddMinutes(50) : now.AddHours(2),
            IsActive = true,
            CreatedAt = SimfClock.Now,
        };
        db.Sessions.Add(session);
        await db.SaveChangesAsync();
        return session;
    }

    private async Task ReserveAsync(Guid sessionId, string row, int seat, string token)
    {
        var response = await PostAuthAsync(
            $"/api/v1/app/sessions/{sessionId}/seats/reserve",
            new ReserveSeatRequest { RowLabel = row, SeatNumber = seat }, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<SessionSeatMap> ReadSeatMapAsync(Guid sessionId, string token)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Get, $"/api/v1/app/sessions/{sessionId}/seats");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content
            .ReadFromJsonAsync<ApiResult<SessionSeatMap>>())!.Data!;
    }

    /// <summary>An approved visitor with a profile type. The VIP tier reuses the
    /// existing notion (<c>IsVipTier</c>), same as the D-771 tests.</summary>
    private async Task<(string Token, Guid UserId)> SignInVisitorAsync(bool vip)
    {
        var email = $"move-visitor-{Guid.NewGuid():N}@simf.test";
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

        var userId = await FindUserIdAsync(email);
        await SetProfileTypeAsync(userId, allowsVip: vip);

        var sign = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest { Email = email, Password = AuthFlow.Password });
        var body = (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        return (body.Data!.Tokens!.AccessToken, userId);
    }

    private async Task<Guid> FindUserIdAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        return await db.Users.AsNoTracking()
            .Where(u => u.Email == email)
            .Select(u => u.Id)
            .SingleAsync();
    }

    private async Task SetProfileTypeAsync(Guid userId, bool allowsVip)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var type = new UserProfileType
        {
            Id = Guid.NewGuid(),
            Name = $"Move-{Guid.NewGuid():N}"[..16],
            NameArabic = "فئة",
            PageColor = "#000000",
            IsForVisitor = true,
            MobileAppRole = MobileAppRole.None,
            IsVipTier = allowsVip,
            IsActive = true,
            CreatedAt = SimfClock.Now,
        };
        db.ProfileTypes.Add(type);

        var profile = await db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
        if (profile is null)
        {
            profile = new UserProfile
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                CreatedAt = SimfClock.Now,
            };
            db.UserProfiles.Add(profile);
        }
        profile.ProfileTypeId = type.Id;
        await db.SaveChangesAsync();
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
}
