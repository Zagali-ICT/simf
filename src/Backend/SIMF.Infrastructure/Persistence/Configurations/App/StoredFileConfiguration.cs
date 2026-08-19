using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Files;
using SIMF.Common.Files;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>The single unified <c>StoredFile</c> table configuration.
/// The enums persist as <c>int</c> (the EF default for enum-backed properties);
/// <see cref="StoredFile.OwnerEntityId"/> / <c>CreatedBy</c> are polymorphic bare
/// Guids and carry NO FK. Bytes live out-of-row; only the metadata
/// is stored here.
///
/// <para>That owner pair still carries no key, but this table is now the
/// <b>principal</b> of several: owning rows point at it by a typed
/// <c>Guid? XFileId</c> with a real foreign key. The two links are
/// not redundant — the owner pair is queried by <c>Service</c> as well as by
/// owner, which a bare key cannot express, and the owner-or-admin download check
/// reads it.</para></summary>
internal sealed class StoredFileConfiguration : IEntityTypeConfiguration<StoredFile>
{
    public void Configure(EntityTypeBuilder<StoredFile> builder)
    {
        // A stored file is never zero bytes. SpeakerPresentations carried this
        // guard as CK_SpeakerPresentations_SizeBytes and lost it with the
        // duplicated column it constrained; it could not follow the data here
        // as written, because it demanded a positive count while this column is
        // NULL for every ExternalLink row, and is nulled again when an upload
        // is converted into one. Tolerating NULL puts the guard back for every
        // file service at once rather than for presentations alone - the
        // creation paths already refuse an empty upload with a 400, so what
        // this closes is a seed or a repair script writing straight to the table.
        builder.ToTable("StoredFiles", table => table.HasCheckConstraint(
            "CK_StoredFiles_SizeBytes",
            "[SizeBytes] IS NULL OR [SizeBytes] > 0"));
        builder.HasKey(file => file.Id);

        builder.Property(file => file.Service).IsRequired();
        builder.Property(file => file.SensitivityTier).IsRequired();
        builder.Property(file => file.FileType).IsRequired();
        builder.Property(file => file.SourceType).IsRequired();
        builder.Property(file => file.IsEncrypted).IsRequired();
        builder.Property(file => file.CipherFormatVersion).IsRequired();
        builder.Property(file => file.IsDeletable).IsRequired();
        builder.Property(file => file.OwnerEntityType).IsRequired();

        builder.Property(file => file.StorageKey).HasMaxLength(400);
        builder.Property(file => file.ExternalUrl).HasMaxLength(1024);
        builder.Property(file => file.OriginalFileName).HasMaxLength(260);
        builder.Property(file => file.ContentType).HasMaxLength(128);
        // char(64), not nvarchar(64): a SHA-256 rendered as hex is always exactly 64
        // ASCII characters, so the variable-length Unicode column cost four bytes per
        // stored byte for no gain. Every writer either stores that 64-char hex or
        // null, so the fixed length never pads a short value into a failed compare.
        builder.Property(file => file.Sha256)
            .HasMaxLength(64)
            .IsUnicode(false)
            .IsFixedLength();

        // The polymorphic owner back-link the owning row resolves on read, and the
        // scope of the owner-or-admin download check. Filtered to live rows.
        builder.HasIndex(file => new { file.OwnerEntityType, file.OwnerEntityId })
            .HasFilter("[IsActive] = 1");

        // Drives the Media Library / per-service management list.
        builder.HasIndex(file => new { file.Service, file.IsActive });

        // One active file per (service, owner) - for the services where that is
        // actually the rule. Until this index existed the invariant was a
        // convention AssetService enforced after the fact: it uploaded the
        // replacement, then retired the previous row, so two admins replacing the
        // same logo at the same moment could both pass and both end active, and the
        // readers then disagreed with the retire about which one was current. No
        // amount of C# closes that; the database has to hold it.
        //
        // The service list is generated from FileServicePolicies.SingleActivePerOwner
        // rather than written out here, so the constraint cannot drift from the code
        // that maintains it. It is a genuine subset: a gallery, a set of identity
        // documents and a speaker's presentations are all many-per-owner, and a
        // blanket index would reject the second one.
        //
        // This also serves the reads AssetService.ResolveAsync and
        // UserProfileRepository.GetOwnerScopedFileAsync perform - equality on
        // Service + OwnerEntityId against live rows - so no separate covering index
        // is added for them. With uniqueness at most one row matches, and the
        // ordering those queries carry never has to sort anything.
        builder.HasIndex(file => new { file.Service, file.OwnerEntityId })
            .IsUnique()
            .HasFilter(
                "[IsActive] = 1 AND [OwnerEntityId] IS NOT NULL AND [Service] IN ("
                + FileServicePolicies.SingleActivePerOwnerSqlList + ")");

        // "files I uploaded" + audit lookups.
        builder.HasIndex(file => file.CreatedBy);

        // Counts the store by KEK version, which is what makes a key rotation
        // inventoried rather than guessed: "how many files are still on key 1" is
        // a GROUP BY here instead of a walk over every blob header on disk. Kept
        // to encrypted rows, because a plaintext row has no KEK and would only
        // dilute the count. The filter deliberately does NOT exclude nulls - an
        // encrypted row with no recorded version is exactly what a re-wrap pass
        // has to find. Provisioned ahead of that worker, on the same reasoning as
        // the RetainUntil index below.
        builder.HasIndex(file => file.KekVersion)
            .HasFilter("[IsEncrypted] = 1");

        // Enumerates live, time-limited rows for a retention review. No sweep
        // reads it yet — the retention date is recorded, never acted on
        // automatically — so this index is provisioned ahead of that worker
        // rather than serving a query today.
        builder.HasIndex(file => file.RetainUntil)
            .HasFilter("[IsActive] = 1 AND [RetainUntil] IS NOT NULL");
    }
}
