using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Files;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>The single unified <c>StoredFile</c> table configuration.
/// The enums persist as <c>int</c> (the EF default for enum-backed properties);
/// <see cref="StoredFile.OwnerEntityId"/> / <c>CreatedBy</c> are polymorphic bare
/// Guids and carry NO FK. Bytes live out-of-row; only the metadata
/// is stored here.
///
/// <para>That owner pair still carries no key, but this table is now the
/// <b>principal</b> of several: owning rows point at it by a typed
/// <c>Guid? XFileId</c> with a real foreign key (D-885, D-888). The two links are
/// not redundant — the owner pair is queried by <c>Service</c> as well as by
/// owner, which a bare key cannot express, and the owner-or-admin download check
/// reads it.</para></summary>
internal sealed class StoredFileConfiguration : IEntityTypeConfiguration<StoredFile>
{
    public void Configure(EntityTypeBuilder<StoredFile> builder)
    {
        builder.ToTable("StoredFiles");
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
        builder.Property(file => file.Sha256).HasMaxLength(64);

        // The polymorphic owner back-link the owning row resolves on read, and the
        // scope of the owner-or-admin download check. Filtered to live rows.
        builder.HasIndex(file => new { file.OwnerEntityType, file.OwnerEntityId })
            .HasFilter("[IsActive] = 1");

        // Drives the Media Library / per-service management list.
        builder.HasIndex(file => new { file.Service, file.IsActive });

        // "files I uploaded" + audit lookups.
        builder.HasIndex(file => file.CreatedBy);

        // The retention secure-erase sweep enumerates live, time-limited rows.
        builder.HasIndex(file => file.RetainUntil)
            .HasFilter("[IsActive] = 1 AND [RetainUntil] IS NOT NULL");
    }
}
