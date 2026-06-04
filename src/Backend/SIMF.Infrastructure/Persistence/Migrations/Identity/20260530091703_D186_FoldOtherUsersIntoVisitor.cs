using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMF.Infrastructure.Persistence.Migrations.Identity
{
    /// <inheritdoc />
    public partial class D186_FoldOtherUsersIntoVisitor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // D-186 — collapse the legacy UserType='Other' rows into
            // 'Visitor'. The partner-vs-audience split now lives on
            // the linked ProfileType.IsVisitor (App DB; the sibling
            // migration handles that). After this runs, every non-
            // admin account is UserType='Visitor'. UserType is stored
            // as a string (HasConversion<string>), so the literal
            // matters.
            migrationBuilder.Sql(
                "UPDATE [AspNetUsers] SET [UserType] = 'Visitor' WHERE [UserType] = 'Other';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op on roll-back: re-deriving which Visitor accounts
            // used to be Other would require joining back to the App
            // DB's ProfileType.IsVisitor=false set, which is a
            // cross-context query the migration runner cannot express.
            // A roll-back leaves every account as Visitor; the
            // companion App-DB migration's Down restores the 'Other'
            // value on ProfileTypes — but the Identity-side accounts
            // stay collapsed under Visitor. This is acceptable because
            // the freeze contract guarantees a roll-back is for the
            // pre-deploy emergency window only, not for long-term use.
        }
    }
}
