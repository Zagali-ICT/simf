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

        // UserId is a logical FK to SimfUser.Id (Identity DB) —
        // enforced at the service layer, not by SQL. Unique so the
        // second upsert by the same user updates instead of inserting
        // a sibling row.
        builder.HasIndex(profile => profile.UserId).IsUnique();

        // Owner 2026-07-06 — reasonable name caps (was 256); tightened to 50
        // (D-683, owner 2026-07-07), aligned client + server + EF.
        builder.Property(profile => profile.NameArabic).HasMaxLength(50).IsRequired();
        builder.Property(profile => profile.Name).HasMaxLength(50).IsRequired();
        // PDF §2.6 optional job title (max 100, owner 2026-07-06).
        builder.Property(profile => profile.JobTitle).HasMaxLength(100);
        // 2026-07-19 (owner) — Arabic twin, same length as JobTitle.
        builder.Property(profile => profile.JobTitleArabic).HasMaxLength(100);
        // NationalityId is validated at the service layer
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
        // "Show in Meet People Like You" visibility toggle.
        builder.Property(profile => profile.ShowInMeetLikeYou)
            .HasDefaultValue(true);

        builder.Property(profile => profile.SaudiMobile).HasMaxLength(20);
        builder.Property(profile => profile.InternationalMobile).HasMaxLength(24);
        // Stored normalized (3 letters + 1–4 digits, no separators).
        builder.Property(profile => profile.PlateNumber).HasMaxLength(7);
        // SIMF-YYYY-NNNNNNNN is 18 chars; 20 leaves headroom for a
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
        // 2026-07-19 (owner) — Arabic twin, same length as Honorific.
        builder.Property(profile => profile.HonorificArabic).HasMaxLength(64);
        builder.Property(profile => profile.PreferredLanguage).HasMaxLength(16);
        builder.Property(profile => profile.VipPhotoRelativePath).HasMaxLength(260);

        // QrId on UserProfile. 12-char Crockford base32, unique
        // (only minted for Approved rows so most rows are null —
        // filtered unique index).
        builder.Property(profile => profile.QrId).HasMaxLength(16);
        builder.HasIndex(profile => profile.QrId)
            .IsUnique()
            .HasFilter("[QrId] IS NOT NULL");

        // Bilingual rejection-reason text.
        builder.Property(profile => profile.RejectionReason).HasMaxLength(500);
        builder.Property(profile => profile.RejectionReasonArabic).HasMaxLength(500);

        // `accessibility-server-sync` — the five per-account accessibility
        // choices behind GET / PUT /app/account/preferences. Additive columns.
        // TextSize is a STRING holding the app's stable enum name, never an int
        // index, so reordering the client enum can never re-interpret a stored
        // row; 16 chars covers the longest name ("extraLarge") with headroom.
        // The store defaults mirror the wire defaults (captions ON, the rest
        // off), so every pre-existing row reads back as "never chosen".
        // HasSentinel pins what EF treats as "not set" on INSERT: without it,
        // a first save that turns captions OFF would be omitted from the INSERT
        // and silently come back ON from the DEFAULT.
        builder.Property(profile => profile.AccessibilityTextSize)
            .HasMaxLength(16)
            .IsRequired()
            .HasDefaultValue(UserProfile.DefaultAccessibilityTextSize)
            .HasSentinel(UserProfile.DefaultAccessibilityTextSize);
        builder.Property(profile => profile.AccessibilityHighContrast)
            .HasDefaultValue(false);
        builder.Property(profile => profile.AccessibilityReduceMotion)
            .HasDefaultValue(false);
        builder.Property(profile => profile.AccessibilityScreenReaderAssist)
            .HasDefaultValue(false);
        builder.Property(profile => profile.AccessibilityCaptions)
            .HasDefaultValue(true)
            .HasSentinel(true);
        // Nullable ON PURPOSE and with NO default — null is the load-bearing value.
        // It is the only thing separating "the user chose the defaults" from "nobody
        // has chosen anything", and the app relies on that difference to avoid
        // overwriting accessibility settings a user had already made on the device.
        builder.Property(profile => profile.AccessibilityConfiguredAt);

        // P8 — ProfileType lookup; Restrict so a profile-type cannot
        // be deleted while any user is assigned to it.
        builder.HasIndex(profile => profile.ProfileTypeId);
        builder.HasOne(profile => profile.ProfileType)
            .WithMany()
            .HasForeignKey(profile => profile.ProfileTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        // الجهة. Real DB FK to the Organisation lookup; Restrict
        // so an organisation cannot be removed while any profile points at it.
        // Nullable, so profile stubs simply leave it null. Gender is stored as
        // its int value by EF's default enum mapping (no explicit conversion).
        builder.HasIndex(profile => profile.OrganisationId);
        builder.HasOne(profile => profile.Organisation)
            .WithMany()
            .HasForeignKey(profile => profile.OrganisationId)
            .OnDelete(DeleteBehavior.Restrict);

        // الإقليم / region. Real DB FK to the Region lookup,
        // same shape as the Organisation FK (nullable + Restrict); persists the
        // region pick that previously had nowhere to live.
        builder.HasIndex(profile => profile.RegionId);
        builder.HasOne(profile => profile.Region)
            .WithMany()
            .HasForeignKey(profile => profile.RegionId)
            .OnDelete(DeleteBehavior.Restrict);

        // The bulk-badge batch this placeholder profile was
        // minted by. Intra-App-DB FK (nullable + Restrict, same shape as the
        // Organisation / Region FKs) so a batch cannot be hard-deleted while any
        // badge references it — batches are soft-deleted (revoke → IsActive=false).
        builder.HasIndex(profile => profile.BadgeBatchId);
        builder.HasOne(profile => profile.BadgeBatch)
            .WithMany()
            .HasForeignKey(profile => profile.BadgeBatchId)
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
