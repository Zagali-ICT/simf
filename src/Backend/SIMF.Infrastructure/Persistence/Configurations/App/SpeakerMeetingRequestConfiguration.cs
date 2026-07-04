using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.BusinessMeetings;
using SIMF.Domain.Programme;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>D-269 (Mockup page 20) — SpeakerMeetingRequest EF config. Real FK
/// to Speaker with cascade so a deleted speaker removes its pending requests;
/// RequestedByUserId is a logical FK to SimfUser on the Identity DB (no cross-DB
/// relation, D-157). Mirrors <see cref="MeetingRequestConfiguration"/>.</summary>
internal sealed class SpeakerMeetingRequestConfiguration
    : IEntityTypeConfiguration<SpeakerMeetingRequest>
{
    public void Configure(EntityTypeBuilder<SpeakerMeetingRequest> builder)
    {
        // D-611 (Wave B) — a set slot pair must be ordered; NULLs (topic-only
        // request) are allowed to pass.
        builder.ToTable("SpeakerMeetingRequests", table => table.HasCheckConstraint(
            "CK_SpeakerMeetingRequests_Slot",
            "[SlotStartUtc] IS NULL OR [SlotEndUtc] IS NULL OR [SlotEndUtc] > [SlotStartUtc]"));
        builder.HasKey(r => r.Id);

        builder.Property(r => r.RequesterName).HasMaxLength(128).IsRequired();
        builder.Property(r => r.Subject).HasMaxLength(1000).IsRequired();
        builder.Property(r => r.ResponseNote).HasMaxLength(2000);

        // D-611 (Wave B) — Restrict (was Cascade): a hard speaker delete must not
        // silently drop its requests (speakers are soft-deleted, so this never
        // fires in practice — it's a safety backstop).
        builder.HasOne(r => r.Speaker)
            .WithMany()
            .HasForeignKey(r => r.SpeakerId)
            .OnDelete(DeleteBehavior.Restrict);

        // D-611 (Wave B) — the picked availability window, persisted as a real FK
        // (SetNull; removing a window nulls the link rather than blocking).
        builder.HasOne<SpeakerAvailabilityWindow>()
            .WithMany()
            .HasForeignKey(r => r.AvailabilityWindowId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(r => new { r.SpeakerId, r.Status, r.CreatedAt });
        builder.HasIndex(r => r.RequestedByUserId);

        // D-611 (Wave B) — at most one ACCEPTED request per (speaker, slot):
        // Status is stored as int (Accepted=1); the NOT NULL guard excludes
        // legacy topic-only requests whose slot is null (NULLs collide in a
        // SQL Server unique index).
        builder.HasIndex(r => new { r.SpeakerId, r.SlotStartUtc })
            .IsUnique()
            .HasFilter("[Status] = 1 AND [SlotStartUtc] IS NOT NULL");
    }
}
