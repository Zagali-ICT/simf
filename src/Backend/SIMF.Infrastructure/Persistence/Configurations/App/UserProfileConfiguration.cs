using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Organisations;
using SIMF.Domain.Files;
using SIMF.Domain.Profiles;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>
/// EF configuration for the user-profile row, which lives on
/// <c>SimfAppDbContext</c>. Length caps
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
        //
        // FILTERED, and that is load-bearing rather than tidiness. An attendee
        // need not have an account at all, so this column is nullable and most
        // rows at a walk-in desk will be null. SQL Server treats NULLs as EQUAL
        // in a unique index, so an unfiltered one would admit exactly ONE such
        // row system-wide and reject the second with a duplicate-key error —
        // green in every test that creates one profile, failing at the venue on
        // the second registration. Same filtered-unique shape as QrId and
        // ReferenceNumber below, for the same reason.
        builder.HasIndex(profile => profile.UserId)
            .IsUnique()
            .HasFilter("[UserId] IS NOT NULL");

        // Admission state, stored as the enum NAME rather than its ordinal, so
        // reordering the enum can never re-interpret stored rows. Matches how the
        // Identity side persists the same enum.
        builder.Property(profile => profile.AdmissionState)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        // The admission queue reads "everyone awaiting a decision" on every load
        // of the pending pages, and the gate reads state per scan.
        builder.HasIndex(profile => profile.AdmissionState);

        // Reasonable name caps (they were 256), tightened to 50 and
        // aligned across client + server + EF.
        builder.Property(profile => profile.NameArabic).HasMaxLength(50).IsRequired();
        builder.Property(profile => profile.Name).HasMaxLength(50).IsRequired();
        // Optional job title (max 100).
        builder.Property(profile => profile.JobTitle).HasMaxLength(100);
        // Arabic twin, same length as JobTitle.
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

        // Blind-index columns for the duplicate-identity guard. The
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
        // The ID document and the VIP photo are real foreign keys into the one
        // file store. Configured without a navigation property on purpose: no
        // code path walks from a profile to its file (bytes are always fetched
        // through IFileService by id), and a navigation on an encrypted,
        // sensitive document is an invitation to Include() it into ordinary
        // profile reads. This is the deliberate difference from the
        // Organisation and Region keys below, which do carry navigations
        // because callers genuinely traverse them.
        //
        // Restrict, not Cascade: deleting a file must never silently delete the
        // registrant. In practice the constraint should never fire, because
        // StoredFileService deactivates rows rather than removing them, which
        // is exactly why the key is worth having: it turns "should never" into
        // "cannot".
        builder.HasIndex(profile => profile.IdImageFileId);
        builder.HasOne<StoredFile>()
            .WithMany()
            .HasForeignKey(profile => profile.IdImageFileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(profile => profile.VipPhotoFileId);
        builder.HasOne<StoredFile>()
            .WithMany()
            .HasForeignKey(profile => profile.VipPhotoFileId)
            .OnDelete(DeleteBehavior.Restrict);
        // VVIP/VIP extras (موج welcome-message integration). Nullable,
        // only set for VVIP/VIP. Lengths match the FluentValidation + UI.
        builder.Property(profile => profile.MawjId).HasMaxLength(64);
        builder.Property(profile => profile.Honorific).HasMaxLength(64);
        // Arabic twin, same length as Honorific.
        builder.Property(profile => profile.HonorificArabic).HasMaxLength(64);
        builder.Property(profile => profile.PreferredLanguage).HasMaxLength(16);

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

        // ProfileType lookup; Restrict so a profile-type cannot
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

        // M-to-M with Interests. Composite-PK join table
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
