using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Notifications;

namespace SIMF.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF configuration for the in-app notification row (P12 — D-053).
/// The two indexes match the two reads the page does: "the latest 5 for
/// the bell" (UserId + CreatedAt DESC) and "unread count" (UserId +
/// ReadAt).
/// </summary>
internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.HasKey(notification => notification.Id);

        // D-108: NotificationKind enum persisted as its name string.
        // Existing rows are converted by the data migration in the
        // matching EF migration (UPDATE FROM the old dot-form values).
        builder.Property(notification => notification.Kind)
            .HasConversion<string>()
            .HasMaxLength(64)
            .IsRequired();

        // D-108: IsRead is a derived computed accessor — keep it off the table.
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
    }
}
