// Tests: src/Backend/SIMF.Domain/Common/BaseEntity.cs — the soft-delete stamp.
//
// DeletedAt was declared on BaseAuditEntity and described by the HLD, the LLD and
// the offline-sync design as the soft-delete tombstone, but Deactivate() only ever
// flipped IsActive. A repo-wide sweep found exactly one writer (StoredFileService,
// which stamps it by hand), so every other soft-deleted row in the App database
// carried a NULL in the column the sync design reads to build tombstones. These
// tests pin the stamp so the column cannot quietly go dead again.
using SIMF.Common;
using SIMF.Domain.Common;
using Xunit;

namespace SIMF.Domain.Tests;

public sealed class BaseAuditEntityTests
{
    private sealed class SoftDeletable : BaseAuditEntity;

    [Fact]
    public void Deactivate_stamps_the_soft_delete_time()
    {
        var entity = new SoftDeletable();
        Assert.Null(entity.DeletedAt);

        var before = SimfClock.Now;
        entity.Deactivate();
        var after = SimfClock.Now;

        Assert.False(entity.IsActive);
        Assert.NotNull(entity.DeletedAt);
        Assert.InRange(entity.DeletedAt!.Value, before, after);
    }

    [Fact]
    public void Deactivate_keeps_the_first_soft_delete_time()
    {
        // Deactivate() is called without an "is it still active?" guard in several
        // services, and a second call must not move the tombstone: the instant that
        // matters is when the row was deleted, not when someone asked again.
        var deletedAt = new DateTime(2026, 1, 1, 9, 0, 0);
        var entity = new SoftDeletable { IsActive = false, DeletedAt = deletedAt };

        entity.Deactivate();

        Assert.Equal(deletedAt, entity.DeletedAt);
    }

    [Fact]
    public void Deactivate_after_a_restore_stamps_the_new_deletion()
    {
        // A restore clears DeletedAt (AssetService does exactly that), so the next
        // deletion is stamped afresh rather than inheriting the old instant.
        var entity = new SoftDeletable();
        entity.Deactivate();
        entity.IsActive = true;
        entity.DeletedAt = null;

        entity.Deactivate();

        Assert.NotNull(entity.DeletedAt);
    }
}
