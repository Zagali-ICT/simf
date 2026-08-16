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
        // Exactly one owner: a code belongs either to an account (UserId) or to
        // an attendee who holds none yet (UserProfileId), never to both and never
        // to neither. Every writer already honours it, and the redemption paths
        // key off whichever column is set, so a row with both would be redeemable
        // down two different flows and a row with neither is unreachable and
        // undeletable by the account cascade. Anchored on IS NULL / IS NOT NULL
        // rather than a bare comparison: SQL Server passes a CHECK that evaluates
        // to UNKNOWN, so an unanchored predicate would let the null cases through.
        builder.ToTable("AccountCodes", table => table.HasCheckConstraint(
            "CK_AccountCodes_OneOwner",
            "([UserId] IS NOT NULL AND [UserProfileId] IS NULL) OR "
            + "([UserId] IS NULL AND [UserProfileId] IS NOT NULL)"));

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
