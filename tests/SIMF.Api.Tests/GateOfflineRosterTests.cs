// The people a door is expecting, downloaded so a scanner can decide entry with
// no network.
//
// The badge already lets a device answer "is this genuine", "is this from the
// open year" and "is this tier allowed here". It could not answer "is this
// person admitted" or "do they hold a seat in THIS session", so a hall door
// abstained on both — and an abstention at a hall door is a queue.
//
// Two properties matter more than the happy path and are pinned first: the
// roster is scoped to the operator's OWN gates, because Gates.Operate is held by
// every Staff and Moderator account rather than only the provisioned tablets;
// and it carries the MINIMUM, because a gate needs a decision, not a personal
// record.
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Application.AccessControl.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Domain.AccessControl;
using SIMF.Domain.Programme;
using SIMF.Domain.SeatReservations;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

[Trait(TestAreas.TraitName, TestAreas.Gates)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class GateOfflineRosterTests : IClassFixture<SimfApiFactory>
{
    private readonly SimfApiFactory _factory;

    public GateOfflineRosterTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
    }

    [Fact]
    public async Task A_reserved_attendee_is_downloaded_for_the_hall_the_operator_works()
    {
        var operatorUserId = Guid.NewGuid();
        var (hallId, sessionId) = await SeedHallAndSessionAsync();
        await AssignHallDoorAsync(operatorUserId, hallId);
        var profileId = await SeedReservationAsync(sessionId, "A", 7, BookingStatus.Approved);

        var roster = await GetRosterAsync(operatorUserId);

        var entry = Assert.Single(roster.Attendees, a => a.UserProfileId == profileId);
        Assert.Equal(sessionId, entry.SessionId);
        Assert.Equal(hallId, entry.HallId);
        Assert.Equal("A", entry.RowLabel);
        Assert.Equal(7, entry.SeatNumber);
        // A decided boolean, not the raw state: the device should not be
        // reimplementing admission rules the server already owns.
        Assert.True(entry.IsAdmitted);
        Assert.Equal("Attendee", entry.Name);
        // Stamped and expiring. A stale roster admits someone approved this
        // morning and disabled since, which is the failure the abstention
        // existed to prevent.
        Assert.True(roster.ValidUntil > roster.IssuedAt);
    }

    [Fact]
    public async Task An_operator_does_not_receive_a_hall_they_do_not_work()
    {
        // The scoping property. Gates.Operate is held by every Staff and
        // Moderator account, and a roster is attendee names and movements — more
        // sensitive than the badge key, which is already scoped this way.
        var theirOperator = Guid.NewGuid();
        var otherOperator = Guid.NewGuid();
        var (hallId, sessionId) = await SeedHallAndSessionAsync();
        await AssignHallDoorAsync(theirOperator, hallId);
        var profileId = await SeedReservationAsync(sessionId, "B", 3, BookingStatus.Approved);

        var roster = await GetRosterAsync(otherOperator);

        Assert.DoesNotContain(roster.Attendees, a => a.UserProfileId == profileId);
    }

    [Fact]
    public async Task An_operator_with_no_hall_door_gets_an_empty_roster_not_an_error()
    {
        // A perimeter-only operator is an ordinary case, not a failure.
        var roster = await GetRosterAsync(Guid.NewGuid());
        Assert.Empty(roster.Attendees);
    }

    [Theory]
    [InlineData(BookingStatus.Pending)]
    [InlineData(BookingStatus.Rejected)]
    [InlineData(BookingStatus.Cancelled)]
    public async Task Only_a_confirmed_reservation_reads_as_an_expected_attendee(
        BookingStatus status)
    {
        // A request that was never approved must not read as an admitted seat at
        // the door — the reservation carries an approval workflow precisely so
        // the two are distinguishable.
        var operatorUserId = Guid.NewGuid();
        var (hallId, sessionId) = await SeedHallAndSessionAsync();
        await AssignHallDoorAsync(operatorUserId, hallId);
        var profileId = await SeedReservationAsync(sessionId, "C", 1, status);

        var roster = await GetRosterAsync(operatorUserId);

        Assert.DoesNotContain(roster.Attendees, a => a.UserProfileId == profileId);
    }

    [Fact]
    public async Task A_released_seat_is_somebody_elses_now_and_drops_out()
    {
        var operatorUserId = Guid.NewGuid();
        var (hallId, sessionId) = await SeedHallAndSessionAsync();
        await AssignHallDoorAsync(operatorUserId, hallId);
        var profileId = await SeedReservationAsync(
            sessionId, "D", 4, BookingStatus.Approved, released: true);

        var roster = await GetRosterAsync(operatorUserId);

        Assert.DoesNotContain(roster.Attendees, a => a.UserProfileId == profileId);
    }

    [Fact]
    public async Task The_since_cursor_returns_only_what_appeared_after_it()
    {
        // The delta. A full roster on every gate-console load would not survive a
        // venue's network.
        var operatorUserId = Guid.NewGuid();
        var (hallId, sessionId) = await SeedHallAndSessionAsync();
        await AssignHallDoorAsync(operatorUserId, hallId);

        var early = await SeedReservationAsync(
            sessionId, "E", 1, BookingStatus.Approved,
            createdAt: SimfClock.Now.AddHours(-2));
        var late = await SeedReservationAsync(
            sessionId, "E", 2, BookingStatus.Approved,
            createdAt: SimfClock.Now.AddMinutes(-5));

        var delta = await GetRosterAsync(operatorUserId, since: SimfClock.Now.AddHours(-1));

        Assert.Contains(delta.Attendees, a => a.UserProfileId == late);
        Assert.DoesNotContain(delta.Attendees, a => a.UserProfileId == early);
    }

    [Fact]
    public async Task A_general_admission_hold_still_says_the_person_is_expected()
    {
        // A hall admitted by booking rather than by seat uses the same shape with
        // the seat left null: the row still answers the question the door asks.
        var operatorUserId = Guid.NewGuid();
        var (hallId, sessionId) = await SeedHallAndSessionAsync();
        await AssignHallDoorAsync(operatorUserId, hallId);
        var profileId = await SeedReservationAsync(
            sessionId, rowLabel: null, seatNumber: null, BookingStatus.Approved);

        var roster = await GetRosterAsync(operatorUserId);

        var entry = Assert.Single(roster.Attendees, a => a.UserProfileId == profileId);
        Assert.Null(entry.RowLabel);
        Assert.Null(entry.SeatNumber);
    }

    [Fact]
    public void The_roster_carries_no_identity_document_mobile_email_or_organisation()
    {
        // Asserted on the CONTRACT rather than on one response, so it holds for
        // every row rather than for the row a test happened to seed. Those
        // columns are encrypted at rest precisely so they do not travel, and a
        // gate needs a decision and a name to show the operator - not a personal
        // record on a tablet that lives on a folding table in a public hall.
        var carried = typeof(SIMF.Contracts.Gates.GateOfflineRosterEntry)
            .GetProperties()
            .Select(property => property.Name)
            .ToList();

        foreach (var forbidden in new[]
        {
            "NationalId", "IqamaNumber", "PassportNumber", "IdDocument",
            "SaudiMobile", "InternationalMobile", "Mobile",
            "Email", "Organisation",
        })
        {
            Assert.DoesNotContain(
                carried,
                name => name.Contains(forbidden, StringComparison.OrdinalIgnoreCase));
        }
    }

    // -- Helpers --------------------------------------------------------------

    private async Task<Contracts.Gates.GateOfflineRoster> GetRosterAsync(
        Guid operatorUserId, DateTime? since = null)
    {
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IGateOperatorService>();
        return await service.GetOfflineRosterAsync(operatorUserId, since);
    }

    private async Task<(Guid HallId, Guid SessionId)> SeedHallAndSessionAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var now = SimfClock.Now;

        var hall = new Hall
        {
            Id = Guid.NewGuid(),
            // Uniquely indexed, so every seeded hall needs its own.
            Code = "H-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),
            Name = "Roster Hall " + Guid.NewGuid().ToString("N")[..6],
            NameArabic = "قاعة",
            IsActive = true,
            CreatedAt = now,
        };
        db.Halls.Add(hall);

        var session = new Session
        {
            Id = Guid.NewGuid(),
            Code = "SES-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Title = "Roster Session",
            TitleArabic = "جلسة",
            HallId = hall.Id,
            Start = now.AddMinutes(-10),
            End = now.AddMinutes(50),
            IsActive = true,
            CreatedAt = now,
        };
        db.Sessions.Add(session);
        await db.SaveChangesAsync();
        return (hall.Id, session.Id);
    }

    private async Task AssignHallDoorAsync(Guid operatorUserId, Guid hallId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var now = SimfClock.Now;

        var gate = new Gate
        {
            Id = Guid.NewGuid(),
            Code = "G-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Roster Door",
            NameArabic = "باب",
            HallId = hallId,
            DirectionMode = DirectionMode.Both,
            IsActive = true,
            CreatedAt = now,
        };
        db.Gates.Add(gate);
        db.GateAssignments.Add(new GateAssignment
        {
            Id = Guid.NewGuid(),
            GateId = gate.Id,
            UserId = operatorUserId,
            IsActive = true,
            CreatedAt = now,
        });
        await db.SaveChangesAsync();
    }

    private Task<Guid> SeedReservationAsync(
        Guid sessionId, string? rowLabel, int? seatNumber, BookingStatus status,
        bool released = false, DateTime? createdAt = null) =>
        SeedReservationCoreAsync(sessionId, rowLabel, seatNumber, status, released, createdAt);

    private async Task<Guid> SeedReservationCoreAsync(
        Guid sessionId, string? rowLabel, int? seatNumber, BookingStatus status,
        bool released, DateTime? createdAt)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var now = SimfClock.Now;

        // An accountless attendee on purpose: after the admission relocation this
        // is the ordinary case, and it is exactly the population a hall door most
        // needs to recognise.
        var profileId = await TestAttendeeProfiles.CreateAccountlessAsync(
            db, TestAttendeeProfiles.NewQrId());

        db.SeatReservations.Add(new SeatReservation
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            RowLabel = rowLabel,
            SeatNumber = seatNumber,
            Kind = rowLabel is null
                ? SeatReservationKind.OpenSeating
                : SeatReservationKind.UserBooking,
            ReservedForProfileId = profileId,
            Status = status,
            ReleasedAt = released ? now : null,
            CreatedByUserId = Guid.NewGuid(),
            CreatedAt = createdAt ?? now,
        });
        await db.SaveChangesAsync();
        return profileId;
    }
}
