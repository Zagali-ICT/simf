using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMF.Infrastructure.Persistence.Migrations.App
{
    /// <inheritdoc />
    public partial class D744_AddGateHallId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "HallId",
                table: "Gates",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Gates_HallId",
                table: "Gates",
                column: "HallId");

            migrationBuilder.AddForeignKey(
                name: "FK_Gates_Halls_HallId",
                table: "Gates",
                column: "HallId",
                principalTable: "Halls",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Gates_Halls_HallId",
                table: "Gates");

            migrationBuilder.DropIndex(
                name: "IX_Gates_HallId",
                table: "Gates");

            migrationBuilder.DropColumn(
                name: "HallId",
                table: "Gates");
        }
    }
}
