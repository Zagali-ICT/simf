// Tests: SIMF.Api.Tests/MovementTrackingTests.cs
using Microsoft.EntityFrameworkCore;
using SIMF.Application.Programme.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Programme;
using SIMF.Domain.Programme;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Programme;

/// <summary>
/// Implements <see cref="IMovementTrackingService"/>. See that
/// interface for the shape and for why the feature is inert until halls are given
/// geofence boundaries.
///
/// <para>Everything here lives on <c>SIMF_App</c> (pings, halls, sessions), so
/// there is no cross-database read; the attendee id is a bare <c>Guid</c>.</para>
/// </summary>
internal sealed class MovementTrackingService(
    SimfAppDbContext dbContext,
    TimeProvider timeProvider) : IMovementTrackingService
{
    /// <summary>Samples accepted in one upload. A device batching a long offline
    /// stretch is normal; an unbounded batch is not.</summary>
    internal const int MaxSamplesPerUpload = 200;

    /// <summary>A gap longer than this between two consecutive pings ends the
    /// current leg rather than counting the silence as dwell — the device was off,
    /// out of signal, or the attendee left the venue.</summary>
    internal static readonly TimeSpan MaxLegGap = TimeSpan.FromMinutes(10);

    public async Task<RecordDevicePositionsResponse> RecordPositionsAsync(
        Guid userId,
        RecordDevicePositionsRequest request,
        CancellationToken cancellationToken = default)
    {
        var samples = request.Samples ?? [];
        if (samples.Count == 0)
        {
            return new RecordDevicePositionsResponse(0, 0);
        }
        if (samples.Count > MaxSamplesPerUpload)
        {
            throw new ApiException(
                ErrorCodes.ValidationFailed, 400,
                $"An upload carries at most {MaxSamplesPerUpload} position samples.",
                $"يقبل الرفع الواحد {MaxSamplesPerUpload} عيّنة موقع كحدّ أقصى.");
        }
        foreach (var sample in samples)
        {
            if (sample.Lat is < -90 or > 90 || sample.Lon is < -180 or > 180)
            {
                throw new ApiException(
                    ErrorCodes.ValidationFailed, 400,
                    "A position sample carries an out-of-range coordinate.",
                    "إحدى عيّنات الموقع تحمل إحداثية خارج النطاق.");
            }
        }

        // Every hall that actually HAS a boundary. While none do — the shipped
        // state — this list is empty, every ping lands unmatched, and the reads
        // report nothing. That is the intended inert behaviour, not a failure.
        var boundaries = await dbContext.Halls
            .AsNoTracking()
            .Where(h => h.IsActive
                && h.GeofenceCenterLat != null
                && h.GeofenceCenterLon != null
                && h.GeofenceRadiusMeters != null)
            .Select(h => new
            {
                h.Id,
                Lat = h.GeofenceCenterLat!.Value,
                Lon = h.GeofenceCenterLon!.Value,
                Radius = h.GeofenceRadiusMeters!.Value,
            })
            .ToListAsync(cancellationToken);

        // The sessions that could be running during this batch, so a matched ping
        // can also name the session the attendee was sitting in. One query for the
        // whole batch — no per-sample lookup. Skipped entirely when no hall has a
        // boundary, because then nothing can match a hall in the first place.
        var earliest = samples.Min(sample => sample.CapturedAt);
        var latest = samples.Max(sample => sample.CapturedAt);
        var running = new List<RunningSession>();
        if (boundaries.Count > 0)
        {
            running = await dbContext.Sessions
                .AsNoTracking()
                .Where(s => s.IsActive && s.Start <= latest && s.End >= earliest)
                .Select(s => new RunningSession(s.Id, s.HallId, s.Start, s.End))
                .ToListAsync(cancellationToken);
        }

        var now = timeProvider.SimfNow();
        var matched = 0;
        foreach (var sample in samples)
        {
            Guid? hallId = null;
            var closest = double.MaxValue;
            foreach (var hall in boundaries)
            {
                var distance = HaversineMeters(sample.Lat, sample.Lon, hall.Lat, hall.Lon);
                // Nearest containing boundary wins, so overlapping geofences
                // resolve deterministically instead of by row order.
                if (distance <= hall.Radius && distance < closest)
                {
                    closest = distance;
                    hallId = hall.Id;
                }
            }

            Guid? sessionId = null;
            if (hallId is { } resolvedHallId)
            {
                matched++;
                sessionId = running
                    .Where(s => s.HallId == resolvedHallId
                        && s.Start <= sample.CapturedAt
                        && s.End >= sample.CapturedAt)
                    .Select(s => (Guid?)s.SessionId)
                    .FirstOrDefault();
            }

            dbContext.DevicePositionPings.Add(new DevicePositionPing
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                HallId = hallId,
                SessionId = sessionId,
                CapturedAt = sample.CapturedAt,
                Latitude = sample.Lat,
                Longitude = sample.Lon,
                AccuracyMeters = sample.AccuracyMeters,
                CreatedAt = now,
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return new RecordDevicePositionsResponse(samples.Count, matched);
    }

    public async Task<IReadOnlyList<HallDwellSummary>> DwellByHallAsync(
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default)
    {
        var pings = await dbContext.DevicePositionPings
            .AsNoTracking()
            .Where(p => p.HallId != null && p.CapturedAt >= from && p.CapturedAt <= to)
            .OrderBy(p => p.UserId).ThenBy(p => p.CapturedAt)
            .Select(p => new { p.UserId, HallId = p.HallId!.Value, p.CapturedAt })
            .ToListAsync(cancellationToken);
        if (pings.Count == 0)
        {
            return [];
        }

        // Sum each attendee's per-hall dwell, then roll up per hall. Dwell is the
        // time BETWEEN consecutive pings in the same hall, so a single isolated
        // ping contributes zero — it evidences presence, not duration.
        var dwellByHall = new Dictionary<Guid, double>();
        var attendeesByHall = new Dictionary<Guid, HashSet<Guid>>();

        for (var i = 0; i < pings.Count; i++)
        {
            var current = pings[i];
            if (!attendeesByHall.TryGetValue(current.HallId, out var attendees))
            {
                attendees = [];
                attendeesByHall[current.HallId] = attendees;
            }
            attendees.Add(current.UserId);

            if (i + 1 >= pings.Count) { continue; }
            var next = pings[i + 1];
            if (next.UserId != current.UserId || next.HallId != current.HallId) { continue; }

            var gap = next.CapturedAt - current.CapturedAt;
            if (gap <= TimeSpan.Zero || gap > MaxLegGap) { continue; }
            dwellByHall[current.HallId] =
                dwellByHall.GetValueOrDefault(current.HallId) + gap.TotalMinutes;
        }

        var hallNames = await ResolveHallNamesAsync(attendeesByHall.Keys, cancellationToken);

        return attendeesByHall
            .Select(entry =>
            {
                var total = dwellByHall.GetValueOrDefault(entry.Key);
                var attendeeCount = entry.Value.Count;
                hallNames.TryGetValue(entry.Key, out var names);
                return new HallDwellSummary(
                    entry.Key,
                    names.Name,
                    names.NameArabic,
                    attendeeCount,
                    Math.Round(total, 2),
                    attendeeCount == 0 ? 0 : Math.Round(total / attendeeCount, 2));
            })
            .OrderByDescending(summary => summary.TotalDwellMinutes)
            .ThenBy(summary => summary.HallName)
            .ToList();
    }

    public async Task<AttendeeRoute> RouteForAttendeeAsync(
        Guid userId,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default)
    {
        var pings = await dbContext.DevicePositionPings
            .AsNoTracking()
            .Where(p => p.UserId == userId && p.CapturedAt >= from && p.CapturedAt <= to)
            .OrderBy(p => p.CapturedAt)
            .Select(p => new { p.HallId, p.CapturedAt })
            .ToListAsync(cancellationToken);
        if (pings.Count == 0)
        {
            return new AttendeeRoute(userId, []);
        }

        // Collapse the track into consecutive stays: a leg runs while the resolved
        // hall is unchanged AND the pings keep arriving. A gap longer than
        // MaxLegGap ends the leg — the silence is not dwell.
        var legs = new List<(Guid? HallId, DateTime Enter, DateTime Leave)>();
        var legHallId = pings[0].HallId;
        var legEnter = pings[0].CapturedAt;
        var legLeave = pings[0].CapturedAt;

        for (var i = 1; i < pings.Count; i++)
        {
            var ping = pings[i];
            var sameHall = ping.HallId == legHallId;
            var continuous = ping.CapturedAt - legLeave <= MaxLegGap;
            if (sameHall && continuous)
            {
                legLeave = ping.CapturedAt;
                continue;
            }
            legs.Add((legHallId, legEnter, legLeave));
            legHallId = ping.HallId;
            legEnter = ping.CapturedAt;
            legLeave = ping.CapturedAt;
        }
        legs.Add((legHallId, legEnter, legLeave));

        var hallNames = await ResolveHallNamesAsync(
            legs.Where(leg => leg.HallId.HasValue).Select(leg => leg.HallId!.Value),
            cancellationToken);

        var projected = legs
            .Select(leg =>
            {
                string? name = null;
                string? nameArabic = null;
                if (leg.HallId is { } hallId && hallNames.TryGetValue(hallId, out var names))
                {
                    name = names.Name;
                    nameArabic = names.NameArabic;
                }
                return new RouteLeg(
                    leg.HallId, name, nameArabic, leg.Enter, leg.Leave,
                    Math.Round((leg.Leave - leg.Enter).TotalMinutes, 2));
            })
            .ToList();

        return new AttendeeRoute(userId, projected);
    }

    /// <summary>Bilingual names for the halls a projection touched. The pings carry
    /// no FK by design, so an id that no longer resolves is simply absent and the
    /// caller renders the leg without a name.</summary>
    private async Task<Dictionary<Guid, (string Name, string NameArabic)>> ResolveHallNamesAsync(
        IEnumerable<Guid> hallIds, CancellationToken cancellationToken)
    {
        var ids = hallIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }
        var rows = await dbContext.Halls
            .AsNoTracking()
            .Where(h => ids.Contains(h.Id))
            .Select(h => new { h.Id, h.Name, h.NameArabic })
            .ToListAsync(cancellationToken);
        return rows.ToDictionary(row => row.Id, row => (row.Name, row.NameArabic));
    }

    /// <summary>One session that could have been running while a batch was
    /// captured. A named type rather than an anonymous one so the "no boundaries →
    /// empty list" branch above has something to declare.</summary>
    private sealed record RunningSession(
        Guid SessionId, Guid HallId, DateTime Start, DateTime End);

    /// <summary>Great-circle distance in metres between two WGS-84 points. Same
    /// formula the geofence arrival check uses (<c>HallAttendanceService</c>), so a
    /// ping and an arrival agree on what "inside the boundary" means.</summary>
    private static double HaversineMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadiusMeters = 6_371_000.0;
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
            + Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2))
              * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return earthRadiusMeters * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;
}
