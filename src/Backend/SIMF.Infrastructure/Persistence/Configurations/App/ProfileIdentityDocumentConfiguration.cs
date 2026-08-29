using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Profiles;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>
/// EF configuration for the per-attendee identity documents, on
/// <c>SimfAppDbContext</c>. Discovered by
/// <c>ApplyConfigurationsFromAssembly</c> through the navigation on
/// <see cref="UserProfile.IdentityDocuments"/> — there is deliberately no
/// <c>DbSet</c>, because every read reaches the rows through their profile and a
/// context-level set would only invite an unscoped query over encrypted PII.
///
/// <para>The one index that matters here is <c>(ProfileId, Kind)</c>: one profile
/// holds at most one national ID, one Iqama and one passport, so the read path
/// never has to choose between two passports.</para>
///
/// <para>There is NO cross-profile constraint. A unique index over
/// <see cref="ProfileIdentityDocument.NumberHash"/> used to make the same number
/// on two different profiles impossible; it was dropped on owner instruction
/// because it blocked a visitor whose number already sat on an earlier profile
/// from registering at all. The digest column survives it unread — nothing
/// queries or indexes it — because the plaintext is encrypted under a random
/// nonce and the digest is the only seam a future lookup could use.</para>
/// </summary>
internal sealed class ProfileIdentityDocumentConfiguration
    : IEntityTypeConfiguration<ProfileIdentityDocument>
{
    public void Configure(EntityTypeBuilder<ProfileIdentityDocument> builder)
    {
        builder.ToTable("ProfileIdentityDocuments");
        builder.HasKey(document => document.Id);

        // Stored as the enum NAME, not its ordinal, so reordering
        // IdentityDocumentKind can never re-interpret a stored passport as a
        // national id. Same choice, for the same reason, as UserProfile's
        // AdmissionState.
        builder.Property(document => document.Kind)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        // Sized to hold the encrypted blob, matching the width the PII converter
        // loop in SimfAppDbContext gives the parent row's remaining plaintext
        // columns. Written here rather than left to convention because this has to
        // survive encryption — a column sized for a 20-character Iqama would
        // truncate the ciphertext and lose the number outright.
        builder.Property(document => document.Number)
            .HasMaxLength(256)
            .IsRequired();

        // The keyed HMAC-SHA256 digest, 64 hex characters. Deliberately OUTSIDE
        // the PII converter: encrypting a digest under a random nonce would make
        // it a different value on every write and destroy the determinism the
        // unique index below depends on. Same reasoning that kept the three *Hash
        // columns it replaced outside that converter.
        builder.Property(document => document.NumberHash)
            .HasMaxLength(64)
            .IsRequired();

        // One document of each kind per attendee. This is the constraint that used
        // to be expressed by there being exactly one NationalId / IqamaNumber /
        // PassportNumber column; without it the child table would happily hold two
        // passports for one person and the read path would have to pick one.
        builder.HasIndex(document => new { document.ProfileId, document.Kind })
            .IsUnique();

        // Cascade, unlike every other foreign key on the profile row. A document
        // has no meaning apart from its attendee, and it is PII: a row outliving
        // the profile it describes is retained personal data with nothing left to
        // justify it. It used to matter more — an orphan kept occupying the
        // cross-profile unique index and barred that person from ever registering
        // the number again — but that index is gone and the reason above stands
        // without it.
        builder.HasOne(document => document.Profile)
            .WithMany(profile => profile.IdentityDocuments)
            .HasForeignKey(document => document.ProfileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
