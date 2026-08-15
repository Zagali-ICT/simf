using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Notifications;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>
/// EF configuration for the admin notification broadcast job (Announcements
/// desk). Lives on SIMF_App. Enums persist as their name strings; the max
/// lengths mirror <c>AdminCreateBroadcastValidator</c>.
/// </summary>
internal sealed class NotificationBroadcastConfiguration
    : IEntityTypeConfiguration<NotificationBroadcast>
{
    public void Configure(EntityTypeBuilder<NotificationBroadcast> builder)
    {
        // The target is an arc, not two loose columns: a Session broadcast
        // carries a session id and no audience scope, an Audience broadcast the
        // reverse. NotificationBroadcastService already enforces it on the only
        // write path; this stops a row that satisfies neither branch — the shape
        // the fan-out worker cannot resolve recipients for — reaching the table
        // by any other route. The enums persist by name, so the literals are the
        // member names.
        builder.ToTable("NotificationBroadcasts", table => table.HasCheckConstraint(
            "CK_NotificationBroadcasts_TargetArc",
            "([TargetMode] = 'Session' AND [SessionId] IS NOT NULL AND [AudienceScope] IS NULL)"
                + " OR ([TargetMode] = 'Audience' AND [AudienceScope] IS NOT NULL AND [SessionId] IS NULL)"));

        builder.HasKey(broadcast => broadcast.Id);

        builder.Property(broadcast => broadcast.TargetMode)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();
        builder.Property(broadcast => broadcast.AudienceScope)
            .HasConversion<string>()
            .HasMaxLength(32);
        builder.Property(broadcast => broadcast.Severity)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();
        builder.Property(broadcast => broadcast.Status)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(broadcast => broadcast.Title).HasMaxLength(200).IsRequired();
        builder.Property(broadcast => broadcast.TitleArabic).HasMaxLength(200).IsRequired();
        builder.Property(broadcast => broadcast.Body).HasMaxLength(2000).IsRequired();
        builder.Property(broadcast => broadcast.BodyArabic).HasMaxLength(2000).IsRequired();
        builder.Property(broadcast => broadcast.Error).HasMaxLength(1024);

        // Serves the worker's claim query (WHERE Status = 'Pending' ORDER BY
        // CreatedAt). The history grid orders by CreatedAt without a Status
        // predicate, so it scans + sorts — acceptable for the small Announcements
        // table (a dedicated CreatedAt index is only warranted if it grows large).
        builder.HasIndex(broadcast => new { broadcast.Status, broadcast.CreatedAt });
    }
}
