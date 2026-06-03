using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.IdentityAccess;

namespace SIMF.Infrastructure.Persistence.Configurations;

internal sealed class SecondFactorTokenConfiguration : IEntityTypeConfiguration<SecondFactorToken>
{
    public void Configure(EntityTypeBuilder<SecondFactorToken> builder)
    {
        builder.HasKey(token => token.Id);

        builder.Property(token => token.TokenHash).HasMaxLength(128).IsRequired();
        builder.Property(token => token.Kind).HasConversion<string>().HasMaxLength(16);

        builder.HasIndex(token => token.TokenHash).IsUnique();
        builder.HasIndex(token => token.CreateBy);

        builder.HasOne<SimfUser>()
            .WithMany()
            .HasForeignKey(token => token.CreateBy)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
