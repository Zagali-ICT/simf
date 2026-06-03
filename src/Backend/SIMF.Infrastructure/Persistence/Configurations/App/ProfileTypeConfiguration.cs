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

        // D-186 — after the UserType collapse every profile type is
        // Visitor-scope; the CP picker + approval queues filter by
        // (IsForVisitor, IsActive), so one composite index serves both.
        builder.HasIndex(profileType => new { profileType.IsForVisitor, profileType.IsActive });
    }
}
