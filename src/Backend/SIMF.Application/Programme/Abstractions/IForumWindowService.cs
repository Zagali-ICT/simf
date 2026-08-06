namespace SIMF.Application.Programme.Abstractions;

/// <summary>The forum's day boundary, derived from the authored
/// <c>ProgrammeDay</c> rows (MIN/MAX over their event-local <c>Date</c>). Meeting
/// scheduling (admin-arranged business meetings + speaker availability windows) is
/// bounded to these days so an admin cannot pick a date outside the event. This
/// deliberately keys off <c>ProgrammeDay</c> rather than the admin-editable
/// <c>OrganizationProfile.EventStartDate/EventEndDate</c>, which currently holds a
/// stale placeholder range.</summary>
public interface IForumWindowService
{
    /// <summary>The inclusive first / last authored forum day (MIN/MAX over ACTIVE
    /// <c>ProgrammeDay.Date</c>), or <c>null</c> when no programme days exist yet.
    /// Callers treat <c>null</c> as "no bound" (content is simply not seeded); they
    /// never hard-block scheduling on the absence of programme days.</summary>
    Task<(DateOnly MinDate, DateOnly MaxDate)?> GetForumDaysAsync(
        CancellationToken cancellationToken = default);
}
