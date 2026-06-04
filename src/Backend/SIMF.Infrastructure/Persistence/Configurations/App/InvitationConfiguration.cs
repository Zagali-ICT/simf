using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Profiles;
using SIMF.Domain.PublicRelations;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>
/// D-168 (gap doc G5) — Invitation entity configuration. Real DB FK to
/// <see cref="UserProfile"/> on the recipient side (D-167 moved
/// UserProfile to this DbContext so a real FK is possible). SentByUserId
/// stays a logical FK because SimfUser lives on the Identity DB; the
/// service layer enforces existence at write time.
/// </summary>
internal sealed class InvitationConfiguration : IEntityTypeConfiguration<Invitation>
{
    public void Configure(EntityTypeBuilder<Invitation> builder)
    {
        builder.ToTable("Invitations");
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
