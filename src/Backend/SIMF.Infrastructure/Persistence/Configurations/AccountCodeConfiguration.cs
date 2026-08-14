// Tests: SIMF.Api.Tests/AuthFlow.cs (the Code column holds a hash, recovered
//        by brute force) and SIMF.Api.Tests/RetentionPurgeServiceTests.cs.
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.IdentityAccess;

namespace SIMF.Infrastructure.Persistence.Configurations;

internal sealed class AccountCodeConfiguration : IEntityTypeConfiguration<AccountCode>
{
    public void Configure(EntityTypeBuilder<AccountCode> builder)
    {
        builder.HasKey(code => code.Id);

        builder.Property(code => code.Code).HasMaxLength(16).IsRequired();

        builder.Property(code => code.Purpose)
            .HasConversion<string>()
            .HasMaxLength(32);

        // Long enough for the addresses the account table itself accepts.
        builder.Property(code => code.PendingEmail).HasMaxLength(256);

        builder.HasIndex(code => new { code.UserId, code.Purpose });

        // The lookup for the pre-account flow, which knows the attendee and not
        // an account. Filtered, because only that one purpose sets the column and
        // the index would otherwise be mostly nulls.
        builder.HasIndex(code => new { code.UserProfileId, code.Purpose })
            .HasFilter("[UserProfileId] IS NOT NULL");

        // Optional now: a code issued before its owner has an account has no
        // account to point at. Cascade still deletes an account's codes with it;
        // a profile-keyed code has no such parent here, and is consumed or
        // expires rather than being cleaned up by a foreign key.
        builder.HasOne<SimfUser>()
            .WithMany()
            .HasForeignKey(code => code.UserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
