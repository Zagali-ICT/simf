using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Common.Enums;
using SIMF.Domain.Profiles;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>EF mapping for <see cref="UserProfileType"/> (P7 — D-048).</summary>
internal sealed class ProfileTypeConfiguration : IEntityTypeConfiguration<UserProfileType>
{
    public void Configure(EntityTypeBuilder<UserProfileType> builder)
    {
        builder.ToTable("ProfileTypes");

        builder.HasKey(profileType => profileType.Id);

        builder.Property(profileType => profileType.Name)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(profileType => profileType.NameArabic)
            .HasMaxLength(128)
            .IsRequired();

        // A hex string like "#FFD700" or a CSS variable name —
        // 32 chars is comfortable for both.
        builder.Property(profileType => profileType.PageColor)
            .HasMaxLength(32)
            .IsRequired();

        // D-161 — MobileAppRole persisted as the stringly enum value
        // (None / Visitor / Staff / Moderator) so a DBA reading the
        // table can interpret the column without an out-of-band
        // mapping table. Default None keeps backfill safe.
        builder.Property(profileType => profileType.MobileAppRole)
            .HasConversion<string>()
            .HasMaxLength(16)
            .HasDefaultValue(MobileAppRole.None)
            .IsRequired();

        // D-186 — audience/partner split inside the Visitor scope.
        // Defaults to true so freshly created profile types are
        // assumed audience-side until an admin toggles them.
        builder.Property(profileType => profileType.IsForVisitor)
            .HasDefaultValue(true)
            .IsRequired();

        // D-611 (Wave B) — VIP meeting-slot eligibility (replaces the Name
        // substring hack). Default false; the seeder flips VVIP/VIP to true.
        builder.Property(profileType => profileType.AllowsVipMeetingSlots)
            .HasDefaultValue(false)
            .IsRequired();

        // D-725 (owner item 1) — app sign-up picker visibility. Default true
        // so existing + freshly created rows stay registerable; the D-725
        // migration's data step flips Staff / Moderator to false, and the CP
        // form lets an admin toggle any row.
        builder.Property(profileType => profileType.IsAppRegisterable)
            .HasDefaultValue(true)
            .IsRequired();

        // D-760 (owner request) — "Meet People" networking visibility. Default
        // true so existing rows backfill to visible and stay in the partner
        // directory + recommender; the CP "Others" form lets an admin hide a
        // whole partner type.
        builder.Property(profileType => profileType.ShowInPartnerDirectory)
            .HasDefaultValue(true)
            .IsRequired();

        // D-186 — after the UserType collapse every profile type is
        // Visitor-scope; the CP picker + approval queues filter by
        // (IsForVisitor, IsActive), so one composite index serves both.
        builder.HasIndex(profileType => new { profileType.IsForVisitor, profileType.IsActive });

        // D-611 (Wave B) — unique profile-type name among the ACTIVE rows (the
        // seeder is idempotent by Name; the filter lets an admin reuse a
        // soft-deleted name).
        builder.HasIndex(profileType => profileType.Name)
            .IsUnique()
            .HasFilter("[IsActive] = 1");
    }
}
