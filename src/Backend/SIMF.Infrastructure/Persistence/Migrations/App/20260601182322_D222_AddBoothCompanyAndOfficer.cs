using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMF.Infrastructure.Persistence.Migrations.App
{
    /// <inheritdoc />
    public partial class D222_AddBoothCompanyAndOfficer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "Booths",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OfficerEmail",
                table: "Booths",
                type: "nvarchar(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OfficerName",
                table: "Booths",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OfficerPhone",
                table: "Booths",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Booths_CompanyId",
                table: "Booths",
                column: "CompanyId");

            migrationBuilder.AddForeignKey(
                name: "FK_Booths_Companies_CompanyId",
                table: "Booths",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Booths_Companies_CompanyId",
                table: "Booths");

            migrationBuilder.DropIndex(
                name: "IX_Booths_CompanyId",
                table: "Booths");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "Booths");

            migrationBuilder.DropColumn(
                name: "OfficerEmail",
                table: "Booths");

            migrationBuilder.DropColumn(
                name: "OfficerName",
                table: "Booths");

            migrationBuilder.DropColumn(
                name: "OfficerPhone",
                table: "Booths");
        }
    }
}
