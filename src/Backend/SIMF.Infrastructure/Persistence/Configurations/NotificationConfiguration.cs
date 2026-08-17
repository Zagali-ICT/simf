using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Notifications;

using SIMF.Common.Enums;

namespace SIMF.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF configuration for the in-app notification row.
/// The two indexes match the two reads the page does: "the latest 5 for
/// the bell" (UserId + CreatedAt DESC) and "unread count" (UserId +
/// ReadAt).
/// </summary>
internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        // GroupCode is a closed vocabulary held as text: every value comes from
        // NotificationKindCatalog.Groups, and the one site that overrides the catalog
        // default (the session-scoped broadcast) picks Groups.Sessions from the same
        // constants. Nothing but the CHECK stopped a typo'd group from reaching the
        // column, where it would silently drop the tile out of every app filter chip
        // instead of failing — the same reason Role and CallerKind carry one.
        //
        // NULL passes deliberately: a CHECK admits a row whose predicate is UNKNOWN,
        // and null is the legitimate "no group" case for a kind the catalog does not
        // map, which the app shows in the "all" bucket only. Do not anchor this on
        // IS NULL the way CK_AccountCodes_OneOwner has to.
        builder.ToTable("Notifications", table => table.HasCheckConstraint(
            "CK_Notifications_GroupCode",
            "[GroupCode] IN ('Account', 'Vip', 'Bookings', 'Sessions', 'Meetings', 'Ratings')"));

        builder.HasKey(notification => notification.Id);

        // NotificationKind persisted as its name string, so appending a value can
        // never renumber a stored row.
        builder.Property(notification => notification.Kind)
            .HasConversion<string>()
            .HasMaxLength(64)
            .IsRequired();

        // IsRead is a derived computed accessor — keep it off the table.
        builder.Ignore(notification => notification.IsRead);
        builder.Property(notification => notification.Title)
            .HasMaxLength(256)
            .IsRequired();
        builder.Property(notification => notification.TitleArabic)
            .HasMaxLength(256)
            .IsRequired();
        builder.Property(notification => notification.Body)
            .HasMaxLength(2000)
            .IsRequired();
        builder.Property(notification => notification.BodyArabic)
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(notification => notification.Severity)
            .HasConversion<string>()
            .HasMaxLength(16);

        builder.Property(notification => notification.RelatedEntityType)
            .HasMaxLength(64);

        // The backend-driven tile: app-internal deep-link + group code.
        builder.Property(notification => notification.ClickUrl)
            .HasMaxLength(512);
        builder.Property(notification => notification.GroupCode)
            .HasMaxLength(64);

        // The bell pulls the latest 5; the page pulls a paged grid in
        // CreatedAt-DESC order. Index supports both.
        builder.HasIndex(notification => new
            {
                notification.UserId,
                notification.CreatedAt,
            })
            .IsDescending(false, true);

        // Unread-count is the most frequent read (polled every 60 s by
        // the bell).
        builder.HasIndex(notification => new
        {
            notification.UserId,
            notification.ReadAt,
        });

        // Notification lives on the Identity DB alongside AspNetUsers, so UserId is a
        // real FK rather than the cross-database bare Guid the App entities use:
        // cascade, so a deleted user's notifications go with them. The
        // (UserId, CreatedAt) index above already covers the FK.
        builder.HasOne<SimfUser>()
            .WithMany()
            .HasForeignKey(notification => notification.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
