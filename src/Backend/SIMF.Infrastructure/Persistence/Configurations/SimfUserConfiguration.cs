using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.IdentityAccess;

using SIMF.Common.Enums;

namespace SIMF.Infrastructure.Persistence.Configurations;

internal sealed class SimfUserConfiguration : IEntityTypeConfiguration<SimfUser>
{
    public void Configure(EntityTypeBuilder<SimfUser> builder)
    {
        builder.Property(user => user.DisplayName)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(user => user.AccountState)
            .HasConversion<string>()
            .HasMaxLength(32);

        // P7 — UserType (decision D-048). Stored as a string for
        // readability in SQL diagnostics. P8 moved ProfileTypeId off
        // SimfUser onto UserProfile (see UserProfileConfiguration).
        builder.Property(user => user.UserType)
            .HasConversion<string>()
            .HasMaxLength(16);
        // The "users of type X, newest first" admin listing
        // filters by UserType and orders by CreatedAt; the composite supersedes
        // the former standalone HasIndex(UserType) (a redundant left-prefix).
        builder.HasIndex(user => new { user.UserType, user.CreatedAt });

        builder.Property(user => user.AvatarRelativePath)
            .HasMaxLength(256);

        // State-change metadata. The composite index supports
        // "recently rejected" / "recently approved" admin queries without
        // scanning AspNetUsers. D-106 moved QrId + RejectionReason* to
        // UserProfile (profile-scope).
        builder.HasIndex(user => new { user.AccountState, user.StateChangedAt });

        // Enforce e-mail uniqueness at the DB. Identity creates
        // a non-unique "EmailIndex" on NormalizedEmail by default; override it in
        // place (same database name) as UNIQUE + filtered so multiple NULL/e-mail-
        // less rows are still allowed. Redundant-but-defensive alongside the
        // already-unique UserNameIndex (UserName == Email for every account).
        builder.HasIndex(user => user.NormalizedEmail)
            .IsUnique()
            .HasDatabaseName("EmailIndex")
            .HasFilter("[NormalizedEmail] IS NOT NULL");
    }
}
