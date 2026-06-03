using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.IdentityAccess;

namespace SIMF.Infrastructure.Persistence.Configurations;

internal sealed class TotpRecoveryCodeConfiguration : IEntityTypeConfiguration<TotpRecoveryCode>
{
    public void Configure(EntityTypeBuilder<TotpRecoveryCode> builder)
    {
        builder.HasKey(code => code.Id);

        // SHA-256 hash, base64 (44 chars). Fixed length so the index is tight.
        builder.Property(code => code.CodeHash).HasMaxLength(64).IsRequired();

        builder.HasIndex(code => code.CreateBy);
        builder.HasIndex(code => code.CodeHash);

        builder.HasOne<SimfUser>()
            .WithMany()
            .HasForeignKey(code => code.CreateBy)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
