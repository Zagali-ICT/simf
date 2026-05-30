using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMF.Infrastructure.Persistence.Migrations.App
{
    /// <inheritdoc />
    public partial class D186_AddProfileTypeIsVisitor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsVisitor",
                table: "ProfileTypes",
                type: "bit",
                nullable: false,
                defaultValue: true);

            // D-186 — backfill existing rows that were created under
            // the now-removed UserType.Other scope. The UserType column
            // is stored as a string (HasConversion<string>), so the
            // legacy value is literal 'Other'. After the update every
            // ProfileType is UserType='Visitor'; the partner-vs-audience
            // distinction lives entirely on IsVisitor.
            migrationBuilder.Sql(
                "UPDATE [ProfileTypes] SET [IsVisitor] = 0 WHERE [UserType] = 'Other';");
            migrationBuilder.Sql(
                "UPDATE [ProfileTypes] SET [UserType] = 'Visitor' WHERE [UserType] = 'Other';");

            migrationBuilder.CreateIndex(
                name: "IX_ProfileTypes_IsVisitor_IsActive",
                table: "ProfileTypes",
                columns: new[] { "IsVisitor", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProfileTypes_IsVisitor_IsActive",
                table: "ProfileTypes");

            // D-186 — best-effort restore: rows that were partner-side
            // (IsVisitor=false) revert to the legacy UserType='Other'
            // string so a roll-back leaves the old code path findable.
            migrationBuilder.Sql(
                "UPDATE [ProfileTypes] SET [UserType] = 'Other' WHERE [IsVisitor] = 0;");

            migrationBuilder.DropColumn(
                name: "IsVisitor",
                table: "ProfileTypes");
        }
    }
}
