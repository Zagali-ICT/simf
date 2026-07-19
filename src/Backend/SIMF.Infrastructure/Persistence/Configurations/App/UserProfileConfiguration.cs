using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Organisations;
using SIMF.Domain.Profiles;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>
/// EF configuration for the user-profile row (decisions D-046, P8 —
/// D-048; D-167 moved this onto <c>SimfAppDbContext</c>). Length caps
/// line up with the FluentValidation rules in
/// <c>UpsertUserProfileRequestValidator</c>. The <c>ProfileTypeId</c> FK
/// references the <c>ProfileTypes</c> lookup with
/// <c>OnDelete(Restrict)</c> so a profile-type cannot be deleted while
/// any user is assigned to it.
/// </summary>
internal sealed class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> builder)
    {
        builder.ToTable("UserProfiles");
        builder.HasKey(profile => profile.Id);

        // D-167: UserId is a logical FK to SimfUser.Id (Identity DB) —
        // enforced at the service layer, not by SQL. Unique so the
        // second upsert by the same user updates instead of inserting
        // a sibling row.
        builder.HasIndex(profile => profile.UserId).IsUnique();

        // Owner 2026-07-06 — reasonable name caps (was 256); tightened to 50
        // (D-683, owner 2026-07-07), aligned client + server + EF.
        builder.Property(profile => profile.NameArabic).HasMaxLength(50).IsRequired();
        builder.Property(profile => profile.Name).HasMaxLength(50).IsRequired();
        // D-163 — PDF §2.6 optional job title (max 100, owner 2026-07-06).
        builder.Property(profile => profile.JobTitle).HasMaxLength(100);
        // D-151 / D-167: NationalityId is validated at the service layer
        // (UserProfileService.ResolveIdAsync rejects unknown ids). We do
        // NOT add a real DB FK here even though Country now lives in the
        // same DB, because the existing data model allows 0 = "no
        // nationality chosen" on profile stubs (admin-create-user, walk-in
        // pre-fill, test seed fixtures) — a real FK would break that
        // pre-existing behaviour. Indexed so admin filtering by
        // nationality stays cheap.
        builder.Property(profile => profile.NationalityId).IsRequired();
        builder.HasIndex(profile => profile.NationalityId);
        builder.Property(profile => profile.PlaceOfBirth).HasMaxLength(128);
        builder.Property(profile => profile.NationalId).HasMaxLength(20);
        builder.Property(profile => profile.IqamaNumber).HasMaxLength(20);
        builder.Property(profile => profile.PassportNumber).HasMaxLength(32);

        // H-1 — blind-index columns for the duplicate-identity guard. The
        // plaintext id columns above are randomly-nonce encrypted (SimfAppDbContext
        // OnModelCreating) so they CANNOT be unique-indexed; these deterministic
        // keyed-HMAC digests can. Filtered UNIQUE so two profiles cannot share a
        // National ID / Iqama / Passport, while the many null rows (no id of that
        // kind, or a row not written through the guarded path) never collide. Same
        // filtered-unique style as QrId / ReferenceNumber above.
        builder.Property(profile => profile.NationalIdHash).HasMaxLength(64);
        builder.Property(profile => profile.IqamaNumberHash).HasMaxLength(64);
        builder.Property(profile => profile.PassportNumberHash).HasMaxLength(64);
        builder.HasIndex(profile => profile.NationalIdHash)
            .IsUnique()
            .HasFilter("[NationalIdHash] IS NOT NULL");
        builder.HasIndex(profile => profile.IqamaNumberHash)
            .IsUnique()
            .HasFilter("[IqamaNumberHash] IS NOT NULL");
        builder.HasIndex(profile => profile.PassportNumberHash)
            .IsUnique()
            .HasFilter("[PassportNumberHash] IS NOT NULL");
        // D-736 — "Show in Meet People Like You" visibility toggle.
        builder.Property(profile => profile.ShowInMeetLikeYou)
            .HasDefaultValue(true);

        builder.Property(profile => profile.SaudiMobile).HasMaxLength(20);
        builder.Property(profile => profile.InternationalMobile).HasMaxLength(24);
        // C6 — D-371: stored normalized (3 letters + 1–4 digits, no separators).
        builder.Property(profile => profile.PlateNumber).HasMaxLength(7);
        // D-373 — SIMF-YYYY-NNNNNNNN is 18 chars; 20 leaves headroom for a
        // longer sequence. Unique among the rows that have one.
        builder.Property(profile => profile.ReferenceNumber).HasMaxLength(20);
        builder.HasIndex(profile => profile.ReferenceNumber)
            .IsUnique()
            .HasFilter("[ReferenceNumber] IS NOT NULL");
        builder.Property(profile => profile.IdImageRelativePath).HasMaxLength(256);
        // V-1 — VVIP/VIP extras (موج welcome-message integration). Nullable,
        // only set for VVIP/VIP. Lengths match the FluentValidation + UI.
        builder.Property(profile => profile.MawjId).HasMaxLength(64);
        builder.Property(profile => profile.Honorific).HasMaxLength(64);
        builder.Property(profile => profile.PreferredLanguage).HasMaxLength(16);
        builder.Property(profile => profile.VipPhotoRelativePath).HasMaxLength(260);

        // D-106: QrId on UserProfile. 12-char Crockford base32, unique
        // (only minted for Approved rows so most rows are null —
        // filtered unique index).
        builder.Property(profile => profile.QrId).HasMaxLength(16);
        builder.HasIndex(profile => profile.QrId)
            .IsUnique()
            .HasFilter("[QrId] IS NOT NULL");

        // D-106: bilingual rejection-reason text.
        builder.Property(profile => profile.RejectionReason).HasMaxLength(500);
        builder.Property(profile => profile.RejectionReasonArabic).HasMaxLength(500);

        // P8 — ProfileType lookup; Restrict so a profile-type cannot
        // be deleted while any user is assigned to it.
        builder.HasIndex(profile => profile.ProfileTypeId);
        builder.HasOne(profile => profile.ProfileType)
            .WithMany()
            .HasForeignKey(profile => profile.ProfileTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        // B3 — D-221: الجهة. Real DB FK to the Organisation lookup; Restrict
        // so an organisation cannot be removed while any profile points at it.
        // Nullable, so profile stubs simply leave it null. Gender is stored as
        // its int value by EF's default enum mapping (no explicit conversion).
        builder.HasIndex(profile => profile.OrganisationId);
        builder.HasOne(profile => profile.Organisation)
            .WithMany()
            .HasForeignKey(profile => profile.OrganisationId)
            .OnDelete(DeleteBehavior.Restrict);

        // D-611 (Wave B) — الإقليم / region. Real DB FK to the Region lookup,
        // same shape as the Organisation FK (nullable + Restrict); persists the
        // region pick that previously had nowhere to live.
        builder.HasIndex(profile => profile.RegionId);
        builder.HasOne(profile => profile.Region)
            .WithMany()
            .HasForeignKey(profile => profile.RegionId)
            .OnDelete(DeleteBehavior.Restrict);

        // P9 — M-to-M with Interests (D-050). Composite-PK join table
        // UserProfileInterests, both FKs Cascade so deleting either side
        // cleans up the join row.
        builder.HasMany(profile => profile.Interests)
            .WithMany()
            .UsingEntity<Dictionary<string, object>>(
                "UserProfileInterests",
                right => right.HasOne<UserInterest>()
                    .WithMany()
                    .HasForeignKey("InterestId")
                    .OnDelete(DeleteBehavior.Cascade),
                left => left.HasOne<UserProfile>()
                    .WithMany()
                    .HasForeignKey("UserProfileId")
                    .OnDelete(DeleteBehavior.Cascade),
                join =>
                {
                    join.HasKey("UserProfileId", "InterestId");
                    join.HasIndex("InterestId");
                });
    }
}
