using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Contacts;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

// Tests: SIMF.Api.Tests/VisitorContactSharingTests.cs

/// <summary>VisitorShareToken EF config.
/// <see cref="VisitorShareToken.TokenHash"/> is uniquely indexed — it is the
/// deterministic lookup key a scanned code resolves through, because
/// <see cref="VisitorShareToken.Token"/> holds random-nonce ciphertext that no
/// equality query or unique index can work against. Sized 256 for the
/// <c>enc:1:</c> blob, matching the encrypted profile columns; 64 for the hex
/// digest. <see cref="VisitorShareToken.UserId"/> is indexed for the owner's
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
        // Both columns stay ASCII once the code is encrypted, so varchar rather
        // than nvarchar (mirrors ScanIdempotency / MeetingActionToken): the token
        // holds the base64 enc:1: envelope and the digest is hex. NOT
        // IsFixedLength — the ciphertext is not a constant length, and char(n)
        // would pad what is read back before it is decrypted.
        builder.Property(token => token.Token).HasMaxLength(256).IsUnicode(false).IsRequired();
        builder.Property(token => token.TokenHash).HasMaxLength(64).IsUnicode(false).IsRequired();
        builder.HasIndex(token => token.TokenHash).IsUnique();
        // One ACTIVE share token per user (the "mint if absent"
        // invariant); a revoked token (IsActive=0) is excluded so a fresh mint
        // after revocation still succeeds.
        builder.HasIndex(token => token.UserId)
            .IsUnique()
            .HasFilter("[IsActive] = 1");
    }
}
