using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Contacts;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>SavedContact EF config. Indexed by owner (the "My Contacts" list
/// query) and by (owner, subject) for the idempotent-save lookup. Both refs are
/// bare-Guid logical FKs to <c>SimfUser.Id</c> on the Identity DB — there is no
/// DB FK across the two databases. Auto-discovered by
/// <c>ApplyConfigurationsFromAssembly</c> (App namespace).</summary>
internal sealed class SavedContactConfiguration : IEntityTypeConfiguration<SavedContact>
{
    public void Configure(EntityTypeBuilder<SavedContact> builder)
    {
        builder.ToTable("SavedContacts");
        builder.HasKey(saved => saved.Id);
        builder.Property(saved => saved.Note).HasMaxLength(512);
        // One ACTIVE saved contact per (owner, subject): the
        // filtered unique replaces the plain composite and backs the idempotent
        // save. The standalone HasIndex(OwnerUserId) is dropped — a redundant
        // left-prefix of this composite.
        builder.HasIndex(saved => new { saved.OwnerUserId, saved.SubjectUserId })
            .IsUnique()
            .HasFilter("[IsActive] = 1");
    }
}
