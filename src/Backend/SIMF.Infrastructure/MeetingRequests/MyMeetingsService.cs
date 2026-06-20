// Tests: SIMF.Api.Tests/MyMeetingsTests.cs
using Microsoft.EntityFrameworkCore;
using SIMF.Application.MeetingRequests.Abstractions;
using SIMF.Contracts.Programme;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.MeetingRequests;

/// <summary>D-479 (#11 follow-up) — unifies the signed-in user's speaker meeting
/// requests (D-269/D-475) and delegation meeting requests (D-478) into one
/// read-only feed for the mobile "My meetings" screen, newest first. Keyed on
/// <c>RequestedByUserId</c> (the meetings the user requested).</summary>
internal sealed class MyMeetingsService(SimfAppDbContext appDbContext) : IMyMeetingsService
{
    public async Task<IReadOnlyList<MyMeetingItem>> GetMyMeetingsAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        var speaker = await appDbContext.SpeakerMeetingRequests.AsNoTracking()
            .Where(r => r.RequestedByUserId == userId)
            .Join(appDbContext.Speakers, r => r.SpeakerId, s => s.Id,
                (r, s) => new MyMeetingItem(
                    MyMeetingKind.SpeakerMeeting, r.Id, s.Name, s.NameArabic, r.Subject,
                    r.Status, r.SlotStartUtc, r.CreatedAt))
            .ToListAsync(cancellationToken);

        var delegation = await appDbContext.DelegationMeetingRequests.AsNoTracking()
            .Where(r => r.RequestedByUserId == userId)
            .Select(r => new MyMeetingItem(
                MyMeetingKind.DelegationMeeting, r.Id,
                r.TargetCountry!.Name, r.TargetCountry!.NameArabic, r.Subject,
                r.Status, r.SlotStartUtc, r.CreatedAt))
            .ToListAsync(cancellationToken);

        return speaker
            .Concat(delegation)
            .OrderByDescending(m => m.CreatedAt)
            .ToList();
    }
}
