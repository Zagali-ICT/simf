using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Profiles;
using SIMF.Domain.PublicRelations;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>
/// Invitation entity configuration. Real DB FK to
/// <see cref="UserProfile"/> on the recipient side, because UserProfile lives
/// on this same DbContext. SentByUserId
/// stays a logical FK because SimfUser lives on the Identity DB; the
/// service layer enforces existence at write time.
/// </summary>
internal sealed class InvitationConfiguration : IEntityTypeConfiguration<Invitation>
{
    public void Configure(EntityTypeBuilder<Invitation> builder)
    {
        // RespondedAt is the response stamp, so it is set exactly when the
        // invitation has been responded to: null while Pending, non-null once
        // settled. AdminInvitationService is the only writer and already keeps
        // the pair in step (create fixes Pending with no stamp; the update
        // stamps every transition off Pending and refuses to move back), so
        // this puts that rule on the table as well — the same state-pin shape
        // as CK_GateAssignments_RevocationPin.
        //
        // State is persisted as its int value, and Pending is 0.
        builder.ToTable("Invitations", table => table.HasCheckConstraint(
            "CK_Invitations_ResponsePin",
            "([State] = 0 AND [RespondedAt] IS NULL) OR "
            + "([State] <> 0 AND [RespondedAt] IS NOT NULL)"));
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Notes).HasMaxLength(1000);

        // Real DB FK to UserProfile — same context. Restrict matches the
        // soft-delete policy: profiles never get hard-deleted out from
        // under an invitation row.
        builder.HasOne<UserProfile>()
            .WithMany()
            .HasForeignKey(i => i.SentToUserProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        // Query indexes — the PR grid filters on recipient or state, the
        // VIP-view filter joins on SentToUserProfileId, and the SOC view
        // sorts by created time. Soft-delete column is also indexed.
        builder.HasIndex(i => i.SentToUserProfileId);
        builder.HasIndex(i => i.SentByUserId);
        builder.HasIndex(i => new { i.IsActive, i.State, i.CreatedAt });
    }
}
