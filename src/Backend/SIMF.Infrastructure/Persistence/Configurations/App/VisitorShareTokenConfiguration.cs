using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Contacts;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>SIMF-FDS-014 §5.4 (D-284) — VisitorShareToken EF config. The
/// <see cref="VisitorShareToken.Token"/> is uniquely indexed (the lookup key on
/// resolve); <see cref="VisitorShareToken.UserId"/> is indexed for the owner's
/// "mint if absent" query. <c>UserId</c> is a bare-Guid logical FK to
/// <c>SimfUser.Id</c> on the Identity DB — no DB FK (D-157). Auto-discovered by
/// <c>ApplyConfigurationsFromAssembly</c> (App namespace).</summary>
internal sealed class VisitorShareTokenConfiguration : IEntityTypeConfiguration<VisitorShareToken>
{
    public void Configure(EntityTypeBuilder<VisitorShareToken> builder)
    {
        builder.ToTable("VisitorShareTokens");
        builder.HasKey(token => token.Id);
        builder.Property(token => token.Token).HasMaxLength(32).IsRequired();
        builder.HasIndex(token => token.Token).IsUnique();
        // One ACTIVE share token per user (the "mint if absent"
        // invariant); a revoked token (IsActive=0) is excluded so a fresh mint
        // after revocation still succeeds.
        builder.HasIndex(token => token.UserId)
            .IsUnique()
            .HasFilter("[IsActive] = 1");
    }
}
