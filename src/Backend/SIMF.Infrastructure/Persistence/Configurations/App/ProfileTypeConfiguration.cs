using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Common.Enums;
using SIMF.Domain.Profiles;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>EF mapping for <see cref="ProfileType"/> (P7 — D-048).</summary>
internal sealed class ProfileTypeConfiguration : IEntityTypeConfiguration<ProfileType>
{
    public void Configure(EntityTypeBuilder<ProfileType> builder)
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

        builder.Property(profileType => profileType.UserType)
            .HasConversion<string>()
            .HasMaxLength(16);

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
        builder.Property(profileType => profileType.IsVisitor)
            .HasDefaultValue(true)
            .IsRequired();

        // The picker filters by (UserType, IsActive) — keep one
        // composite index so the dropdown is one lookup. D-186 adds
        // IsVisitor to a second composite index so the CP Others-
        // approval-queue filter (IsVisitor=false on the Visitor pool)
        // hits an index too.
        builder.HasIndex(profileType => new { profileType.UserType, profileType.IsActive });
        builder.HasIndex(profileType => new { profileType.IsVisitor, profileType.IsActive });
    }
}
