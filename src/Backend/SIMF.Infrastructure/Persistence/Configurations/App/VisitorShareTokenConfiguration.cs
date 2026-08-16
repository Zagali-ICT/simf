using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Contacts;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>VisitorShareToken EF config. The
/// <see cref="VisitorShareToken.Token"/> is uniquely indexed (the lookup key on
/// resolve); <see cref="VisitorShareToken.UserId"/> is indexed for the owner's
/// "mint if absent" query. <c>UserId</c> is a bare-Guid logical FK to
/// <c>SimfUser.Id</c> on the Identity DB — no DB FK. Auto-discovered by
/// <c>ApplyConfigurationsFromAssembly</c> (App namespace).</summary>
internal sealed class VisitorShareTokenConfiguration : IEntityTypeConfiguration<VisitorShareToken>
{
    public void Configure(EntityTypeBuilder<VisitorShareToken> builder)
    {
        // Revocation is pinned to the soft-delete flag: VisitorShareService
        // .RotateTokenAsync is the only writer that retires a token, and it sets
        // IsActive=false and RevokedAt together. Without this, a future writer
        // could deactivate a token and leave RevokedAt null, which reads as
        // "still live" to anyone auditing when a code stopped resolving.
        builder.ToTable("VisitorShareTokens", table => table.HasCheckConstraint(
            "CK_VisitorShareTokens_RevocationPin",
            "([IsActive] = 1 AND [RevokedAt] IS NULL) OR ([IsActive] = 0 AND [RevokedAt] IS NOT NULL)"));
        builder.HasKey(token => token.Id);
        // Crockford base32 — ASCII by construction, so varchar not nvarchar
        // (mirrors ScanIdempotency / MeetingActionToken). NOT IsFixedLength: the
        // minted code is 12 chars against a 32-char ceiling, and char(32) would
        // pad the QR payload with trailing spaces on read.
        builder.Property(token => token.Token).HasMaxLength(32).IsUnicode(false).IsRequired();
        builder.HasIndex(token => token.Token).IsUnique();
        // One ACTIVE share token per user (the "mint if absent"
        // invariant); a revoked token (IsActive=0) is excluded so a fresh mint
        // after revocation still succeeds.
        builder.HasIndex(token => token.UserId)
            .IsUnique()
            .HasFilter("[IsActive] = 1");
    }
}
