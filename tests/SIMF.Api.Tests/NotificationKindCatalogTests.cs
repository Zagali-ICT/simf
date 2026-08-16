using SIMF.Application.Notifications;
using SIMF.Common.Enums;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>D-677 — the kind → (group, clickUrl) catalog the dispatcher stamps
/// every row from. Pure unit tests (no DB).</summary>
[Trait(TestAreas.TraitName, TestAreas.Ops)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Fast)]
public sealed class NotificationKindCatalogTests
{
    [Theory]
    [InlineData(NotificationKind.AccountApproved, "Account")]
    [InlineData(NotificationKind.CredentialSignInOtpSent, "Account")]
    [InlineData(NotificationKind.VipBroadcast, "Vip")]
    [InlineData(NotificationKind.BookingConfirmed, "Bookings")]
    [InlineData(NotificationKind.BookingRejected, "Bookings")]
    [InlineData(NotificationKind.SessionReminder, "Sessions")]
    [InlineData(NotificationKind.MeetingScheduled, "Meetings")]
    [InlineData(NotificationKind.MeetingCancelled, "Meetings")]
    [InlineData(NotificationKind.SessionRatingRequest, "Ratings")]
    [InlineData(NotificationKind.DayRatingRequest, "Ratings")]
    [InlineData(NotificationKind.EventRatingRequest, "Ratings")]
    [InlineData(NotificationKind.AppRatingRequest, "Ratings")]
    [InlineData(NotificationKind.ExhibitionRatingRequest, "Ratings")]
    public void GroupFor_maps_each_kind_to_its_section(NotificationKind kind, string group)
    {
        Assert.Equal(group, NotificationKindCatalog.GroupFor(kind));
    }

    [Fact]
    public void GroupFor_covers_every_defined_kind()
    {
        // A new kind added without a catalog arm would fall to the "all" bucket
        // only — assert every value is classified so that omission is caught.
        foreach (var kind in Enum.GetValues<NotificationKind>())
        {
            Assert.False(
                string.IsNullOrEmpty(NotificationKindCatalog.GroupFor(kind)),
                $"NotificationKind.{kind} has no group in NotificationKindCatalog.");
        }
    }

    [Fact]
    public void ClickUrlFor_BookingConfirmed_opens_the_badge()
    {
        Assert.Equal("/badge", NotificationKindCatalog.ClickUrlFor(NotificationKind.BookingConfirmed, null));
    }

    [Fact]
    public void ClickUrlFor_per_target_rating_kinds_carry_the_target_id()
    {
        var id = Guid.NewGuid();
        Assert.Equal(
            $"/rate?code=Session&targetId={id}",
            NotificationKindCatalog.ClickUrlFor(NotificationKind.SessionRatingRequest, id));
        Assert.Equal(
            $"/rate?code=Day&targetId={id}",
            NotificationKindCatalog.ClickUrlFor(NotificationKind.DayRatingRequest, id));
    }

    [Fact]
    public void ClickUrlFor_a_per_target_kind_without_an_id_is_null()
    {
        Assert.Null(NotificationKindCatalog.ClickUrlFor(NotificationKind.SessionRatingRequest, null));
    }

    [Theory]
    [InlineData(NotificationKind.EventRatingRequest, "/rate?code=Event")]
    [InlineData(NotificationKind.AppRatingRequest, "/rate?code=App")]
    [InlineData(NotificationKind.ExhibitionRatingRequest, "/rate?code=Exhibition")]
    public void ClickUrlFor_global_rating_kinds_carry_no_target(NotificationKind kind, string url)
    {
        Assert.Equal(url, NotificationKindCatalog.ClickUrlFor(kind, null));
    }

    [Fact]
    public void ClickUrlFor_an_informational_kind_is_null()
    {
        Assert.Null(NotificationKindCatalog.ClickUrlFor(NotificationKind.AccountApproved, Guid.NewGuid()));
    }

    // QA A27 — MeetingScheduled / MeetingCancelled / MeetingRequestConfirmed /
    // MeetingReminder had no arm at all, so every speaker + delegation meeting
    // notification tile was inert. They all open the bilateral-meetings page.
    [Theory]
    [InlineData(NotificationKind.MeetingScheduled)]
    [InlineData(NotificationKind.MeetingCancelled)]
    [InlineData(NotificationKind.MeetingRequestConfirmed)]
    [InlineData(NotificationKind.MeetingReminder)]
    public void ClickUrlFor_every_meeting_lifecycle_kind_opens_the_meetings_page(
        NotificationKind kind)
    {
        Assert.Equal("/meetings", NotificationKindCatalog.ClickUrlFor(kind, Guid.NewGuid()));
        // Not per-target — the app has no meeting-detail route, so a missing id must
        // still yield a usable link rather than null.
        Assert.Equal("/meetings", NotificationKindCatalog.ClickUrlFor(kind, null));
    }

    // QA A27 — the Meetings-group kinds must ALL be navigable; a new one added
    // without an arm would silently ship another inert tile.
    [Fact]
    public void ClickUrlFor_covers_every_kind_in_the_Meetings_group()
    {
        foreach (var kind in Enum.GetValues<NotificationKind>())
        {
            if (NotificationKindCatalog.GroupFor(kind)
                != NotificationKindCatalog.Groups.Meetings)
            {
                continue;
            }
            Assert.False(
                string.IsNullOrEmpty(
                    NotificationKindCatalog.ClickUrlFor(kind, Guid.NewGuid())),
                $"NotificationKind.{kind} is in the Meetings group but has no clickUrl.");
        }
    }
}
