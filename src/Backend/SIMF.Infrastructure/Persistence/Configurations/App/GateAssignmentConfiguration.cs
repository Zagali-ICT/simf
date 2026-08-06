using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.AccessControl;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>Operator-to-gate assignment. UserId / AssignedByUserId /
/// RevokedByUserId are logical FKs to SimfUser (DAT-001 §5.3.1).</summary>
internal sealed class GateAssignmentConfiguration
    : IEntityTypeConfiguration<GateAssignment>
{
    public void Configure(EntityTypeBuilder<GateAssignment> builder)
    {
        builder.ToTable("GateAssignments");
        builder.HasKey(assignment => assignment.Id);

        builder.HasIndex(assignment => new { assignment.UserId, assignment.IsActive });
        builder.HasIndex(assignment => new { assignment.GateId, assignment.IsActive });

        // At most one ACTIVE assignment of an operator to a gate.
        builder.HasIndex(assignment => new { assignment.GateId, assignment.UserId })
            .IsUnique()
            .HasFilter("[IsActive] = 1");
    }
}
