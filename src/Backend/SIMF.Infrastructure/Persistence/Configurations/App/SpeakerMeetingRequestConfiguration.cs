using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.BusinessMeetings;
using SIMF.Domain.Programme;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>SpeakerMeetingRequest EF config. Real FK
/// to Speaker with Restrict so a hard speaker delete cannot silently drop its
/// requests; RequestedByUserId is a logical FK to SimfUser on the Identity DB —
/// there is no cross-DB relation.</summary>
internal sealed class SpeakerMeetingRequestConfiguration
    : IEntityTypeConfiguration<SpeakerMeetingRequest>
{
    public void Configure(EntityTypeBuilder<SpeakerMeetingRequest> builder)
    {
        // A set slot pair must be ordered; NULLs (topic-only
        // request) are allowed to pass.
        builder.ToTable("SpeakerMeetingRequests", table => table.HasCheckConstraint(
            "CK_SpeakerMeetingRequests_Slot",
            "[SlotStart] IS NULL OR [SlotEnd] IS NULL OR [SlotEnd] > [SlotStart]"));
        builder.HasKey(r => r.Id);

        builder.Property(r => r.RequesterName).HasMaxLength(128).IsRequired();
        builder.Property(r => r.Subject).HasMaxLength(1000).IsRequired();
        builder.Property(r => r.ResponseNote).HasMaxLength(2000);

        // Restrict (was Cascade): a hard speaker delete must not
        // silently drop its requests (speakers are soft-deleted, so this never
        // fires in practice — it's a safety backstop).
        builder.HasOne(r => r.Speaker)
            .WithMany()
            .HasForeignKey(r => r.SpeakerId)
            .OnDelete(DeleteBehavior.Restrict);

        // The picked availability window, persisted as a real FK
        // (SetNull; removing a window nulls the link rather than blocking).
        builder.HasOne<SpeakerAvailabilityWindow>()
            .WithMany()
            .HasForeignKey(r => r.AvailabilityWindowId)
            .OnDelete(DeleteBehavior.SetNull);

        // The hall + optional table an accept bound the
        // meeting to. SetNull: deleting the hall/table clears the binding rather
        // than blocking (mirrors the availability-window FK above).
        builder.HasOne<Hall>()
            .WithMany()
            .HasForeignKey(r => r.HallId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<MeetingTable>()
            .WithMany()
            .HasForeignKey(r => r.MeetingTableId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(r => new { r.SpeakerId, r.Status, r.CreatedAt });
        builder.HasIndex(r => r.RequestedByUserId);

        // At most one OPEN request per (requester, speaker). SubmitAsync reads that rule
        // and then moves the existing Pending row, which two concurrent submits can both
        // pass, leaving a duplicate pair that makes every later submit throw on its
        // SingleOrDefault. This is the database backstop behind that read-then-write.
        // Filtered to Pending (= 0) alone, so a rejected, cancelled or completed request
        // never blocks a fresh one. Twin of the delegation index.
        builder.HasIndex(r => new { r.RequestedByUserId, r.SpeakerId })
            .IsUnique()
            .HasFilter("[Status] = 0");

        // At most one LIVE request per (speaker, slot). This was
        // widened from Accepted-only to the slot-holding set
        // (`MeetingRequestStatuses.SlotHolding` = Accepted + AwaitingSpeaker + Done): a
        // hall-bound request in AwaitingSpeaker writes the hall slot into
        // SlotStart and so occupies the speaker's calendar — it must be the DB
        // backstop for the speaker double-booking re-check, symmetric with the hall
        // index below. Status is int; SQL Server filtered indexes forbid OR, so the
        // live set is "not a released state" (not Pending=0 / Rejected=2 /
        // Cancelled=3). The NOT NULL guard excludes legacy topic-only requests
        // (NULLs collide in a SQL Server unique index).
        builder.HasIndex(r => new { r.SpeakerId, r.SlotStart })
            .IsUnique()
            .HasFilter("[SlotStart] IS NOT NULL AND [Status] <> 0 AND [Status] <> 2 AND [Status] <> 3");

        // At most one live meeting per (hall, slot): a hall
        // slot cannot be double-booked across speakers. Same slot-holding live set
        // as the speaker index above (`MeetingRequestStatuses.SlotHolding`). The DB
        // backstop for the app-level free-slot re-check in
        // SpeakerMeetingRequestService.
        builder.HasIndex(r => new { r.HallId, r.SlotStart })
            .IsUnique()
            .HasFilter("[HallId] IS NOT NULL AND [SlotStart] IS NOT NULL AND [Status] <> 0 AND [Status] <> 2 AND [Status] <> 3");
    }
}
