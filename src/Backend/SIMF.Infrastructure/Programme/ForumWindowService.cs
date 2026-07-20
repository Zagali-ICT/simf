// Tests: SIMF.Api.Tests/BusinessMeetingsTests.cs, SIMF.Api.Tests/SpeakerAvailabilityTests.cs
using Microsoft.EntityFrameworkCore;
using SIMF.Application.Programme.Abstractions;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Programme;

/// <summary>D-753 — the forum-day window, computed as MIN/MAX over the ACTIVE
/// <c>ProgrammeDay.Date</c> rows on <see cref="SimfAppDbContext"/>. It is the
/// authoritative source for "which days is the event running", replacing the stale
/// <c>OrganizationProfile.EventStartDate/EventEndDate</c> placeholder for meeting
/// scheduling bounds. Returns <c>null</c> when no programme days are seeded, so the
/// callers skip the bound rather than blocking all scheduling.</summary>
internal sealed class ForumWindowService(SimfAppDbContext appDbContext)
    : IForumWindowService
{
    public async Task<(DateOnly MinDate, DateOnly MaxDate)?> GetForumDaysAsync(
        CancellationToken cancellationToken = default)
    {
        // One round-trip: aggregate MIN/MAX over the active programme days. With no
        // active rows the GroupBy yields no group and FirstOrDefault returns null, so
        // the callers apply no forum-day bound (scheduling stays open when the event
        // calendar is not seeded yet).
        var bounds = await appDbContext.ProgrammeDays.AsNoTracking()
            .Where(d => d.IsActive)
            .GroupBy(_ => 1)
            .Select(g => new { Min = g.Min(d => d.Date), Max = g.Max(d => d.Date) })
            .FirstOrDefaultAsync(cancellationToken);

        return bounds is null ? null : (bounds.Min, bounds.Max);
    }
}
