using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.BusinessMeetings;
using SIMF.Domain.Programme;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>DelegationMeetingRequest EF config. Real
/// FKs to Country for both the requesting and target countries (Restrict — a
/// country in use can't be hard-deleted); RequestedByUserId is a logical FK to
/// SimfUser on the Identity DB (no cross-DB relation).</summary>
internal sealed class DelegationMeetingRequestConfiguration
    : IEntityTypeConfiguration<DelegationMeetingRequest>
{
    public void Configure(EntityTypeBuilder<DelegationMeetingRequest> builder)
    {
        // The proposed slot must end after it starts (both nullable), and the
        // delegation size is the 1..100 the submit path validates.
        builder.ToTable("DelegationMeetingRequests", table =>
        {
            table.HasCheckConstraint(
                "CK_DelegationMeetingRequests_Slot",
                "[SlotStart] IS NULL OR [SlotEnd] IS NULL OR [SlotEnd] > [SlotStart]");
            table.HasCheckConstraint(
                "CK_DelegationMeetingRequests_AttendeeCount",
                "[AttendeeCount] >= 1 AND [AttendeeCount] <= 100");
            // A delegation cannot request a meeting with itself. SubmitAsync rejects it
            // with a bilingual 400, but that is the only path that checks; this is the
            // backstop so no other writer can put the row in (mirrors
            // CK_Connections_NotSelf, which guards the same shape on the networking side).
            table.HasCheckConstraint(
                "CK_DelegationMeetingRequests_NotSelf",
                "[RequestingCountryId] <> [TargetCountryId]");
        });
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Subject).HasMaxLength(1000).IsRequired();
        builder.Property(r => r.ResponseNote).HasMaxLength(2000);

        builder.HasOne(r => r.RequestingCountry)
            .WithMany()
            .HasForeignKey(r => r.RequestingCountryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.TargetCountry)
            .WithMany()
            .HasForeignKey(r => r.TargetCountryId)
            .OnDelete(DeleteBehavior.Restrict);

        // The hall + optional table an Approve bound the meeting to (SetNull; a deleted
        // hall/table clears the binding). Mirrors SpeakerMeetingRequestConfiguration,
        // except that a delegation request holds no availability-window reference:
        // nothing ever picked one, so the column and its FK were removed.
        builder.HasOne(r => r.Hall)
            .WithMany()
            .HasForeignKey(r => r.HallId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(r => r.MeetingTable)
            .WithMany()
            .HasForeignKey(r => r.MeetingTableId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(r => new { r.TargetCountryId, r.Status, r.CreatedAt });
        builder.HasIndex(r => r.RequestedByUserId);

        // At most one OPEN request per (requester, target delegation). SubmitAsync reads
        // that rule and then moves the existing Pending row, which two concurrent submits
        // can both pass, leaving a duplicate pair that makes every later submit throw on
        // its SingleOrDefault. This is the database backstop behind that read-then-write.
        // The key is the requesting USER, not their delegation: two delegates of the same
        // country may each hold their own request against the same target. Filtered to
        // Pending (= 0) alone, so a rejected, cancelled or completed request never blocks
        // a fresh one.
        builder.HasIndex(r => new { r.RequestedByUserId, r.TargetCountryId })
            .IsUnique()
            .HasFilter("[Status] = 0");

        // Bi-Meeting rework — at most one live meeting per (hall, slot): a hall slot
        // cannot be double-booked. Same slot-holding live set as the speaker index
        // (`MeetingRequestStatuses.SlotHolding` = "not a released state"), the DB
        // backstop for the app-level free-slot re-check. NULLs collide in a SQL Server
        // unique index, so the NOT NULL guards exclude un-bound rows.
        builder.HasIndex(r => new { r.HallId, r.SlotStart })
            .IsUnique()
            .HasFilter("[HallId] IS NOT NULL AND [SlotStart] IS NOT NULL AND [Status] <> 0 AND [Status] <> 2 AND [Status] <> 3");
    }
}
