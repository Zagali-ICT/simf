// D-771 (owner 2026-07-26) — seat TIERS are real data, not a label:
//  * a VIP-tier visitor may reserve a VIP seat;
//  * every other visitor type is refused a VIP seat (SEAT_TIER_NOT_ELIGIBLE);
//  * nobody may self-reserve a VVIP seat (SEAT_TIER_RESERVED) — an administrator
//    assigns it with a manual guest hint, because a VVIP seat has no
//    registration;
//  * a VIP visitor may still take a Normal seat.
// Plus the staff seating desk: badge -> seat and seat -> occupant.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Sessions;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Profiles;
using SIMF.Domain.Programme;
using SIMF.Domain.SeatReservations;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class SeatTierEligibilityTests : IClassFixture<SimfApiFactory>
{
    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public SeatTierEligibilityTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    // Row A = VVIP (protocol), row B = VIP, row C = Normal.
    private static readonly string[] TierRows = ["A", "B", "C"];
    private static readonly SeatTier[] Tiers =
        [SeatTier.Vvip, SeatTier.Vip, SeatTier.Normal];

    [Fact]
    public async Task Vip_visitor_can_reserve_a_vip_seat()
    {
        var session = await SeedTieredSessionAsync();
        var (token, _) = await SignInVisitorAsync(vip: true);

        var pick = await PostAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/seats/reserve",
            new ReserveSeatRequest { RowLabel = "B", SeatNumber = 1 }, token);

        Assert.Equal(HttpStatusCode.OK, pick.StatusCode);
    }

    [Fact]
    public async Task Normal_visitor_cannot_reserve_a_vip_seat()
    {
        var session = await SeedTieredSessionAsync();
        var (token, _) = await SignInVisitorAsync(vip: false);

        var pick = await PostAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/seats/reserve",
            new ReserveSeatRequest { RowLabel = "B", SeatNumber = 1 }, token);

        Assert.Equal(HttpStatusCode.Conflict, pick.StatusCode);
        var body = (await pick.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SeatTierNotEligible, body.Error!.Code);
    }

    [Fact]
    public async Task Nobody_can_self_reserve_a_vvip_seat()
    {
        // Even a VIP-tier visitor is refused: a VVIP seat has NO registration —
        // the administrator assigns it with a manual guest hint.
        var session = await SeedTieredSessionAsync();
        var (vip, _) = await SignInVisitorAsync(vip: true);
        var (normal, _) = await SignInVisitorAsync(vip: false);

        foreach (var token in new[] { vip, normal })
        {
            var pick = await PostAuthAsync(
                $"/api/v1/app/sessions/{session.Id}/seats/reserve",
                new ReserveSeatRequest { RowLabel = "A", SeatNumber = 1 }, token);
            Assert.Equal(HttpStatusCode.Conflict, pick.StatusCode);
            var body = (await pick.Content.ReadFromJsonAsync<ApiResult<object>>())!;
            Assert.Equal(ErrorCodes.SeatTierReserved, body.Error!.Code);
        }
    }

    [Fact]
    public async Task Vip_visitor_can_reserve_a_normal_seat()
    {
        var session = await SeedTieredSessionAsync();
        var (token, _) = await SignInVisitorAsync(vip: true);

        var pick = await PostAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/seats/reserve",
            new ReserveSeatRequest { RowLabel = "C", SeatNumber = 1 }, token);

        Assert.Equal(HttpStatusCode.OK, pick.StatusCode);
    }

    [Fact]
    public async Task Auto_pick_skips_rows_the_visitor_may_not_sit_in()
    {
        // Row-major order would hand out A1 (VVIP) first; the tier rule must skip
        // the VVIP and VIP rows for a normal visitor and land on the Normal row.
        var session = await SeedTieredSessionAsync();
        var (token, _) = await SignInVisitorAsync(vip: false);

        var pick = await PostAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/seats/reserve-random",
            new { }, token);

        Assert.Equal(HttpStatusCode.OK, pick.StatusCode);
        var mine = (await pick.Content
            .ReadFromJsonAsync<ApiResult<MySeatReservation>>())!.Data!;
        Assert.Equal("C", mine.RowLabel);
    }

    [Fact]
    public async Task Legacy_layout_without_tiers_stays_bookable_by_everyone()
    {
        // A layout written before D-771 stores no SeatTiers; every row must read
        // as Normal so no shipped session loses a bookable seat.
        var session = await SeedTieredSessionAsync(tiers: null);
        var (token, _) = await SignInVisitorAsync(vip: false);

        var pick = await PostAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/seats/reserve",
            new ReserveSeatRequest { RowLabel = "A", SeatNumber = 1 }, token);

        Assert.Equal(HttpStatusCode.OK, pick.StatusCode);
    }

    [Fact]
    public async Task Seat_map_reports_the_row_tiers_and_the_callers_vip_flag()
    {
        var session = await SeedTieredSessionAsync();
        var (token, _) = await SignInVisitorAsync(vip: true);

        var map = await GetAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/seats", token);
        Assert.Equal(HttpStatusCode.OK, map.StatusCode);
        var data = (await map.Content
            .ReadFromJsonAsync<ApiResult<SessionSeatMap>>())!.Data!;

        Assert.Equal(Tiers, data.SeatTiers);
        Assert.True(data.CallerIsVip);
    }

    [Fact]
    public async Task Staff_desk_resolves_a_seat_to_its_vvip_guest_hint()
    {
        // The VVIP seat carries only the administrator's manual note — there is no
        // registration behind it, so the note IS the occupant record.
        var session = await SeedTieredSessionAsync();
        await BlockVvipSeatAsync(session.Id, "A", 1,
            "Reserved for the Minister", "هذا المقعد محجوز لمعالي الوزير");
        var staff = await SignInStaffAsync();

        var response = await GetAuthAsync(
            $"/api/v1/app/staff/sessions/{session.Id}/seating/seat"
            + "?rowLabel=A&seatNumber=1", staff);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var occupant = (await response.Content
            .ReadFromJsonAsync<ApiResult<StaffSeatOccupant>>())!.Data!;
        Assert.True(occupant.Found);
        Assert.Equal(SeatTier.Vvip, occupant.Tier);
        Assert.Equal("Reserved for the Minister", occupant.GuestHint);
        Assert.Equal("هذا المقعد محجوز لمعالي الوزير", occupant.GuestHintArabic);
        Assert.Null(occupant.UserId);
    }

    [Fact]
    public async Task Staff_desk_resolves_a_badge_to_the_guests_seat()
    {
        var session = await SeedTieredSessionAsync();
        var (visitorToken, visitorId) = await SignInVisitorAsync(vip: false);
        var pick = await PostAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/seats/reserve",
            new ReserveSeatRequest { RowLabel = "C", SeatNumber = 2 }, visitorToken);
        Assert.Equal(HttpStatusCode.OK, pick.StatusCode);
        var qrId = await SetBadgeQrAsync(visitorId);
        var staff = await SignInStaffAsync();

        var response = await PostAuthAsync(
            $"/api/v1/app/staff/sessions/{session.Id}/seating/by-badge",
            new StaffSeatBadgeLookupRequest { QrId = qrId }, staff);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var occupant = (await response.Content
            .ReadFromJsonAsync<ApiResult<StaffSeatOccupant>>())!.Data!;
        Assert.True(occupant.Found);
        Assert.Equal("C", occupant.RowLabel);
        Assert.Equal(2, occupant.SeatNumber);
        Assert.Equal(visitorId, occupant.UserId);
    }

    [Fact]
    public async Task Staff_desk_is_forbidden_to_an_ordinary_visitor()
    {
        // The desk is gated on the Seating.Assist operational permission, which a
        // plain Visitor account never carries.
        var session = await SeedTieredSessionAsync();
        var (visitor, _) = await SignInVisitorAsync(vip: false);

        var response = await GetAuthAsync(
            $"/api/v1/app/staff/sessions/{session.Id}/seating/seat"
            + "?rowLabel=C&seatNumber=1", visitor);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // -- helpers --

    private async Task<Session> SeedTieredSessionAsync(SeatTier[]? tiers)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var hall = new Hall
        {
            Id = Guid.NewGuid(),
            Code = "H-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Hall", NameArabic = "قاعة",
            Capacity = TierRows.Length * 4,
            SeatSelectionMode = SeatSelectionMode.AssignedSeat,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Halls.Add(hall);
        db.HallSeatLayouts.Add(new HallSeatLayout
        {
            Id = Guid.NewGuid(),
            HallId = hall.Id,
            RowLabels = string.Join(',', TierRows),
            SeatsPerRow = 4,
            // null = the pre-D-771 shape (no tiers stored → all Normal).
            SeatTiers = tiers is null
                ? null
                : string.Join(',', tiers.Select(t => (int)t)),
            CreatedAt = DateTimeOffset.UtcNow,
        });
        var session = new Session
        {
            Id = Guid.NewGuid(),
            Code = "SES-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Title = "Tiered", TitleArabic = "مصنّفة",
            HallId = hall.Id,
            Start = DateTimeOffset.UtcNow.AddHours(1),
            End = DateTimeOffset.UtcNow.AddHours(2),
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Sessions.Add(session);
        await db.SaveChangesAsync();
        return session;
    }

    // Overload used by the "legacy layout" fact: an explicit null means "store no
    // SeatTiers at all", which the default-parameter form cannot express.
    private Task<Session> SeedTieredSessionAsync() =>
        SeedTieredSessionAsync(Tiers);

    private async Task BlockVvipSeatAsync(
        Guid sessionId, string rowLabel, int seatNumber,
        string hint, string hintArabic)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        db.SeatReservations.Add(new SeatReservation
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            RowLabel = rowLabel,
            SeatNumber = seatNumber,
            Kind = SeatReservationKind.AdminReservedRow,
            ReservedForUserId = null,
            CreatedByUserId = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow,
            Status = BookingStatus.Approved,
            GuestHint = hint,
            GuestHintArabic = hintArabic,
        });
        await db.SaveChangesAsync();
    }

    /// <summary>Signs in an approved visitor and gives them a profile type. The
    /// VIP tier reuses the EXISTING notion — <c>AllowsVipMeetingSlots</c>, the flag
    /// the seeder sets on the VVIP + VIP rows — rather than a parallel one.</summary>
    private async Task<(string Token, Guid UserId)> SignInVisitorAsync(bool vip)
    {
        var email = $"tier-visitor-{Guid.NewGuid():N}@simf.test";
        await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-up",
            new SignUpRequest
            {
                Email = email, Password = AuthFlow.Password,
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
        await SetProfileTypeAsync(
            userId, allowsVip: vip, mobileAppRole: MobileAppRole.None,
            isForVisitor: true);

        var sign = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest { Email = email, Password = AuthFlow.Password });
        var body = (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        return (body.Data!.Tokens!.AccessToken, userId);
    }

    /// <summary>An approved partner account on a Staff-role profile type — the
    /// app resolves it to <c>MobileAppRole.Staff</c>, whose operational grants now
    /// include <c>Seating.Assist</c> (no admin RBAC role involved).</summary>
    private async Task<string> SignInStaffAsync()
    {
        var email = $"tier-staff-{Guid.NewGuid():N}@simf.test";
        await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-up",
            new SignUpRequest
            {
                Email = email, Password = AuthFlow.Password,
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
        await SetProfileTypeAsync(
            userId, allowsVip: false, mobileAppRole: MobileAppRole.Staff,
            isForVisitor: false);

        var sign = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest { Email = email, Password = AuthFlow.Password });
        var body = (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        return body.Data!.Tokens!.AccessToken;
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

    private async Task SetProfileTypeAsync(
        Guid userId, bool allowsVip, MobileAppRole mobileAppRole, bool isForVisitor)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var type = new UserProfileType
        {
            Id = Guid.NewGuid(),
            Name = $"Tier-{Guid.NewGuid():N}"[..16],
            NameArabic = "فئة",
            PageColor = "#000000",
            IsForVisitor = isForVisitor,
            MobileAppRole = mobileAppRole,
            AllowsVipMeetingSlots = allowsVip,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.ProfileTypes.Add(type);

        var profile = await db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
        if (profile is null)
        {
            profile = new UserProfile
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            db.UserProfiles.Add(profile);
        }
        profile.ProfileTypeId = type.Id;
        await db.SaveChangesAsync();
    }

    private async Task<string> SetBadgeQrAsync(Guid userId)
    {
        var qrId = Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var profile = await db.UserProfiles.SingleAsync(p => p.UserId == userId);
        profile.QrId = qrId;
        await db.SaveChangesAsync();
        return qrId;
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

    private Task<HttpResponseMessage> GetAuthAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
