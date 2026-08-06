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
        // The proposed slot must end after it starts (both nullable).
        builder.ToTable("DelegationMeetingRequests", table => table.HasCheckConstraint(
            "CK_DelegationMeetingRequests_Slot",
            "[SlotStart] IS NULL OR [SlotEnd] IS NULL OR [SlotEnd] > [SlotStart]"));
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

        // Bi-Meeting rework — the picked delegation-availability window (SetNull), the
        // hall + optional table an Approve bound the meeting to (SetNull; a deleted
        // hall/table clears the binding). Mirrors SpeakerMeetingRequestConfiguration.
        builder.HasOne<DelegationAvailabilityWindow>()
            .WithMany()
            .HasForeignKey(r => r.AvailabilityWindowId)
            .OnDelete(DeleteBehavior.SetNull);
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
