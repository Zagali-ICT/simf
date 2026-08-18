// Tests: SIMF.Api.Tests/MovementTrackingTests.cs
using System.Data;
using System.Data.Common;
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

    /// <summary>The widest horizontal accuracy worth storing. A consumer GPS fix is
    /// good to single-digit metres and even a coarse cell-tower fix to a few
    /// thousand, so a radius past this locates nothing that a venue-scale geofence
    /// could use — it is a unit mix-up or a hand-rolled caller, not a reading.</summary>
    internal const double MaxAccuracyMeters = 10_000;

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
            // The accuracy radius was stored verbatim while the coordinates beside
            // it were range-checked. It is optional on the wire, so null stays
            // valid; anything present has to be a real distance. The non-finite
            // guard is belt-and-braces — the JSON reader rejects NaN before it gets
            // here — but SQL Server's float cannot hold one, so a caller that ever
            // did reach SaveChanges with it would fail the whole batch.
            if (sample.AccuracyMeters is { } accuracy
                && (double.IsNaN(accuracy)
                    || double.IsInfinity(accuracy)
                    || accuracy is < 0 or > MaxAccuracyMeters))
            {
                throw new ApiException(
                    ErrorCodes.ValidationFailed, 400,
                    "A position sample's accuracy must be between 0 and "
                        + $"{MaxAccuracyMeters:0} metres.",
                    "يجب أن تكون دقة عيّنة الموقع بين 0 و "
                        + $"{MaxAccuracyMeters:0} متر.");
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
        var rows = await ReadHallDwellAsync(from, to, cancellationToken);
        if (rows.Count == 0)
        {
            return [];
        }

        var hallNames = await ResolveHallNamesAsync(
            rows.Select(row => row.HallId), cancellationToken);

        return rows
            .Select(row =>
            {
                var total = TimeSpan.FromMicroseconds(row.DwellMicroseconds).TotalMinutes;
                hallNames.TryGetValue(row.HallId, out var names);
                return new HallDwellSummary(
                    row.HallId,
                    names.Name,
                    names.NameArabic,
                    row.DistinctAttendees,
                    Math.Round(total, 2),
                    row.DistinctAttendees == 0
                        ? 0
                        : Math.Round(total / row.DistinctAttendees, 2));
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

    /// <summary>Per-hall headcount and dwell, rolled up BY THE DATABASE.
    ///
    /// <para>This read used to materialise every matched ping in the window — a
    /// week of the whole venue's device cadence — and pair them in memory. The
    /// rollup is now one GROUP BY over a windowed pairing, so a single row per
    /// hall crosses the wire. It is raw SQL because LINQ cannot express
    /// <c>LAG</c>, and it is parameterised (never concatenated) because two of the
    /// three inputs arrive from a query string.</para>
    ///
    /// <para><b>PARTITION BY UserId alone.</b> Adding HallId to the partition
    /// looks equivalent and is not: an attendee who walks hall A, hall B, then
    /// hall A again would have their two A visits made adjacent inside the A
    /// partition, and the time spent in B counted as dwell in A. A pair is kept
    /// only when the previous ping in the attendee's OWN track was in the same
    /// hall, which is exactly what the in-memory version tested.</para>
    ///
    /// <para>The window filter sits inside the CTE so it runs BEFORE the pairing,
    /// which is where the in-memory version applied it too: a ping taken outside
    /// every boundary does not break a leg, it is simply not there.</para></summary>
    private async Task<List<HallDwellRow>> ReadHallDwellAsync(
        DateTime from, DateTime to, CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = HallDwellSql;
        var transaction = dbContext.Database.CurrentTransaction;
        if (transaction is not null)
        {
            command.Transaction =
                Microsoft.EntityFrameworkCore.Storage.DbContextTransactionExtensions
                    .GetDbTransaction(transaction);
        }
        AddParameter(command, "@from", DbType.DateTime2, from);
        AddParameter(command, "@to", DbType.DateTime2, to);
        AddParameter(command, "@maxLegGapMicroseconds", DbType.Int64,
            MaxLegGap.Ticks / TimeSpan.TicksPerMicrosecond);

        var rows = new List<HallDwellRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new HallDwellRow(
                reader.GetGuid(0), reader.GetInt32(1), reader.GetInt64(2)));
        }
        return rows;
    }

    private static void AddParameter(
        DbCommand command, string name, DbType type, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    /// <summary>One hall's row from <see cref="HallDwellSql"/>. Dwell comes back in
    /// microseconds — an exact integer the database can SUM — and is turned into
    /// minutes once, in one place, on the way out.</summary>
    private sealed record HallDwellRow(
        Guid HallId, int DistinctAttendees, long DwellMicroseconds);

    /// <summary>The dwell rollup. Held beside the method that runs it rather than
    /// inline, so the shape of the query is readable on its own.
    ///
    /// <para>A ping's dwell is the time to the NEXT ping of the same attendee in
    /// the same hall, so an isolated ping contributes no dwell yet still counts its
    /// attendee as present — which is why the headcount is taken over every
    /// qualifying row and the sum only over the paired ones. A gap of zero (two
    /// samples at one instant) and a gap past <see cref="MaxLegGap"/> (the device
    /// went dark) both contribute nothing.</para>
    ///
    /// <para><c>DATEDIFF_BIG</c>, not <c>DATEDIFF</c>: the difference is evaluated
    /// for every adjacent pair before the CASE discards the long ones, and a
    /// multi-day gap expressed in microseconds overflows a 32-bit result.</para>
    /// </summary>
    private const string HallDwellSql = """
        WITH [paired] AS (
            SELECT
                [p].[UserId] AS [UserId],
                [p].[HallId] AS [HallId],
                LAG([p].[HallId]) OVER (
                    PARTITION BY [p].[UserId] ORDER BY [p].[CapturedAt])
                    AS [PreviousHallId],
                DATEDIFF_BIG(
                    microsecond,
                    LAG([p].[CapturedAt]) OVER (
                        PARTITION BY [p].[UserId] ORDER BY [p].[CapturedAt]),
                    [p].[CapturedAt]) AS [GapMicroseconds]
            FROM [dbo].[DevicePositionPings] AS [p]
            WHERE [p].[HallId] IS NOT NULL
              AND [p].[CapturedAt] >= @from
              AND [p].[CapturedAt] <= @to
        )
        SELECT
            [x].[HallId] AS [HallId],
            COUNT(DISTINCT [x].[UserId]) AS [DistinctAttendees],
            SUM(CASE
                    WHEN [x].[PreviousHallId] = [x].[HallId]
                     AND [x].[GapMicroseconds] > 0
                     AND [x].[GapMicroseconds] <= @maxLegGapMicroseconds
                    THEN [x].[GapMicroseconds]
                    ELSE CAST(0 AS bigint)
                END) AS [DwellMicroseconds]
        FROM [paired] AS [x]
        GROUP BY [x].[HallId];
        """;

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
