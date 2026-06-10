using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Assets;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>D-357 — unified media <c>Asset</c> configuration. One ACTIVE asset per
/// (<c>Category</c>, <c>OwnerId</c>) is enforced by a filtered unique index; a
/// soft-deleted row frees the slot so a re-upload after removal succeeds.
/// <c>OwnerId</c> is polymorphic so it carries no FK (D-157 logical reference).</summary>
internal sealed class AssetConfiguration : IEntityTypeConfiguration<Asset>
{
    public void Configure(EntityTypeBuilder<Asset> builder)
    {
        builder.ToTable("Assets");
        builder.HasKey(asset => asset.Id);

        builder.Property(asset => asset.Category).IsRequired();
        builder.Property(asset => asset.OwnerId).IsRequired();
        builder.Property(asset => asset.Kind).IsRequired();
        builder.Property(asset => asset.SourceType).IsRequired();

        builder.Property(asset => asset.StoragePath).HasMaxLength(256);
        builder.Property(asset => asset.ExternalUrl).HasMaxLength(1024);
        builder.Property(asset => asset.ContentType).HasMaxLength(128);
        builder.Property(asset => asset.OriginalFileName).HasMaxLength(256);

        // One active asset per owner per category; a soft-deleted (IsActive=false)
        // row frees the slot, so re-adding after a remove succeeds.
        builder.HasIndex(asset => new { asset.Category, asset.OwnerId })
            .IsUnique()
            .HasFilter("[IsActive] = 1");

        // Drives the Media Library list (filter by category + active).
        builder.HasIndex(asset => new { asset.Category, asset.IsActive });
    }
}
