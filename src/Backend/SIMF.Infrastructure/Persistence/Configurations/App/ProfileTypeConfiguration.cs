using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Common.Enums;
using SIMF.Domain.Profiles;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>EF mapping for <see cref="UserProfileType"/>.</summary>
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

        // MobileAppRole persisted as the stringly enum value
        // (None / Visitor / Staff / Moderator) so a DBA reading the
        // table can interpret the column without an out-of-band
        // mapping table. Default None keeps backfill safe.
        builder.Property(profileType => profileType.MobileAppRole)
            .HasConversion<string>()
            .HasMaxLength(16)
            .HasDefaultValue(MobileAppRole.None)
            .IsRequired();

        // Audience/partner split inside the Visitor scope.
        // Defaults to true so freshly created profile types are
        // assumed audience-side until an admin toggles them.
        builder.Property(profileType => profileType.IsForVisitor)
            .HasDefaultValue(true)
            .IsRequired();

        // VIP meeting-slot eligibility (replaces the Name
        // substring hack). Default false; the seeder flips VVIP/VIP to true.
        builder.Property(profileType => profileType.IsVipTier)
            .HasDefaultValue(false)
            .IsRequired();

        // App sign-up picker visibility. Default true so existing +
        // freshly created rows stay registerable; the migration's data step
        // flips Staff / Moderator to false, and the CP form lets an admin
        // toggle any row.
        builder.Property(profileType => profileType.IsAppRegisterable)
            .HasDefaultValue(true)
            .IsRequired();

        // "Meet People" networking visibility. Default
        // true so existing rows backfill to visible and stay in the partner
        // directory + recommender; the CP "Others" form lets an admin hide a
        // whole partner type.
        builder.Property(profileType => profileType.ShowInPartnerDirectory)
            .HasDefaultValue(true)
            .IsRequired();

        // The small stable number the offline event badge carries in
        // place of the Guid id. Default 0 = unassigned, which no badge can
        // carry. Unique among ACTIVE rows only, mirroring the Name index below,
        // so a soft-deleted type does not permanently reserve its code — but
        // note the code of a type whose badges are still in circulation must
        // NOT be reused, which is an operational rule, not a DB one.
        builder.Property(profileType => profileType.Code)
            .HasDefaultValue((short)0)
            .IsRequired();

        builder.HasIndex(profileType => profileType.Code)
            .IsUnique()
            .HasFilter("[IsActive] = 1 AND [Code] <> 0");

        // After the UserType collapse every profile type is
        // Visitor-scope; the CP picker + approval queues filter by
        // (IsForVisitor, IsActive), so one composite index serves both.
        builder.HasIndex(profileType => new { profileType.IsForVisitor, profileType.IsActive });

        // Unique profile-type name among the ACTIVE rows (the
        // seeder is idempotent by Name; the filter lets an admin reuse a
        // soft-deleted name).
        builder.HasIndex(profileType => profileType.Name)
            .IsUnique()
            .HasFilter("[IsActive] = 1");
    }
}
