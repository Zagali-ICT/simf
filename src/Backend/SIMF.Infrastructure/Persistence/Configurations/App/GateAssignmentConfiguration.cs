using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.AccessControl;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>Operator-to-gate assignment. UserId, the inherited CreatedBy (the
/// admin who made the assignment) and RevokedByUserId are logical FKs to
/// SimfUser (DAT-001 §5.3.1) -- bare Guids, because the user rows live in the
/// Identity database.</summary>
internal sealed class GateAssignmentConfiguration
    : IEntityTypeConfiguration<GateAssignment>
{
    public void Configure(EntityTypeBuilder<GateAssignment> builder)
    {
        // Revocation is all-or-nothing: an active assignment carries no
        // revocation stamp, and a revoked one carries both halves of it. The
        // single write site sets IsActive, RevokedAt and RevokedByUserId
        // together, so pin that here rather than trusting it to stay true.
        builder.ToTable("GateAssignments", table => table.HasCheckConstraint(
            "CK_GateAssignments_RevocationPin",
            "([IsActive] = 1 AND [RevokedAt] IS NULL AND [RevokedByUserId] IS NULL) OR " +
            "([IsActive] = 0 AND [RevokedAt] IS NOT NULL AND [RevokedByUserId] IS NOT NULL)"));
        builder.HasKey(assignment => assignment.Id);

        builder.HasIndex(assignment => new { assignment.UserId, assignment.IsActive });
        builder.HasIndex(assignment => new { assignment.GateId, assignment.IsActive });

        // At most one ACTIVE assignment of an operator to a gate.
        builder.HasIndex(assignment => new { assignment.GateId, assignment.UserId })
            .IsUnique()
            .HasFilter("[IsActive] = 1");
    }
}
