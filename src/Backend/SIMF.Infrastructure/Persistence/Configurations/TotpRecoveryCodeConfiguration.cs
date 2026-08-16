using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.IdentityAccess;

namespace SIMF.Infrastructure.Persistence.Configurations;

internal sealed class TotpRecoveryCodeConfiguration : IEntityTypeConfiguration<TotpRecoveryCode>
{
    public void Configure(EntityTypeBuilder<TotpRecoveryCode> builder)
    {
        builder.HasKey(code => code.Id);

        // RecoveryCode.Hash defers to OpaqueToken.Hash: a SHA-256 digest as
        // LOWERCASE HEX, so every value is exactly 64 characters and the column
        // is sized to that, not to headroom. (The comment here used to claim
        // base64 / 44 chars, which no writer has ever produced.)
        builder.Property(code => code.CodeHash).HasMaxLength(64).IsRequired();

        // One row per (user, code-hash): the composite unique is
        // the DB backstop for "consume a recovery code once" and supersedes the
        // standalone HasIndex(UserId) (its left-prefix). The standalone
        // HasIndex(CodeHash) stays for a by-hash redemption lookup.
        builder.HasIndex(code => new { code.UserId, code.CodeHash }).IsUnique();
        builder.HasIndex(code => code.CodeHash);

        builder.HasOne<SimfUser>()
            .WithMany()
            .HasForeignKey(code => code.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
