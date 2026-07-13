using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMF.Infrastructure.Persistence.Migrations.App
{
    /// <inheritdoc />
    public partial class AddRegistrationReferenceSequence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "RegistrationReferenceSequence");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropSequence(
                name: "RegistrationReferenceSequence");
        }
    }
}
