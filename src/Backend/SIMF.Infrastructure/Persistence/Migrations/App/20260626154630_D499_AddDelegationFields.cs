using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMF.Infrastructure.Persistence.Migrations.App
{
    /// <inheritdoc />
    public partial class D499_AddDelegationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "DelegationArrivalDate",
                table: "Countries",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DelegationDepartureDate",
                table: "Countries",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "HeadOfDelegationUserProfileId",
                table: "Countries",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 32,
                columns: new[] { "DelegationArrivalDate", "DelegationDepartureDate", "HeadOfDelegationUserProfileId" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 36,
                columns: new[] { "DelegationArrivalDate", "DelegationDepartureDate", "HeadOfDelegationUserProfileId" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 40,
                columns: new[] { "DelegationArrivalDate", "DelegationDepartureDate", "HeadOfDelegationUserProfileId" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 48,
                columns: new[] { "DelegationArrivalDate", "DelegationDepartureDate", "HeadOfDelegationUserProfileId" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 50,
                columns: new[] { "DelegationArrivalDate", "DelegationDepartureDate", "HeadOfDelegationUserProfileId" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 56,
                columns: new[] { "DelegationArrivalDate", "DelegationDepartureDate", "HeadOfDelegationUserProfileId" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 76,
                columns: new[] { "DelegationArrivalDate", "DelegationDepartureDate", "HeadOfDelegationUserProfileId" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 124,
                columns: new[] { "DelegationArrivalDate", "DelegationDepartureDate", "HeadOfDelegationUserProfileId" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 156,
                columns: new[] { "DelegationArrivalDate", "DelegationDepartureDate", "HeadOfDelegationUserProfileId" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 208,
                columns: new[] { "DelegationArrivalDate", "DelegationDepartureDate", "HeadOfDelegationUserProfileId" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 231,
                columns: new[] { "DelegationArrivalDate", "DelegationDepartureDate", "HeadOfDelegationUserProfileId" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 246,
                columns: new[] { "DelegationArrivalDate", "DelegationDepartureDate", "HeadOfDelegationUserProfileId" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 250,
                columns: new[] { "DelegationArrivalDate", "DelegationDepartureDate", "HeadOfDelegationUserProfileId" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 262,
                columns: new[] { "DelegationArrivalDate", "DelegationDepartureDate", "HeadOfDelegationUserProfileId" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 275,
                columns: new[] { "DelegationArrivalDate", "DelegationDepartureDate", "HeadOfDelegationUserProfileId" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 276,
                columns: new[] { "DelegationArrivalDate", "DelegationDepartureDate", "HeadOfDelegationUserProfileId" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 300,
                columns: new[] { "DelegationArrivalDate", "DelegationDepartureDate", "HeadOfDelegationUserProfileId" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 356,
                columns: new[] { "DelegationArrivalDate", "DelegationDepartureDate", "HeadOfDelegationUserProfileId" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 360,
                columns: new[] { "DelegationArrivalDate", "DelegationDepartureDate", "HeadOfDelegationUserProfileId" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 364,
                columns: new[] { "DelegationArrivalDate", "DelegationDepartureDate", "HeadOfDelegationUserProfileId" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 368,
                columns: new[] { "DelegationArrivalDate", "DelegationDepartureDate", "HeadOfDelegationUserProfileId" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 372,
                columns: new[] { "DelegationArrivalDate", "DelegationDepartureDate", "HeadOfDelegationUserProfileId" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 380,
                columns: new[] { "DelegationArrivalDate", "DelegationDepartureDate", "HeadOfDelegationUserProfileId" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 392,
                columns: new[] { "DelegationArrivalDate", "DelegationDepartureDate", "HeadOfDelegationUserProfileId" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 400,
                columns: new[] { "DelegationArrivalDate", "DelegationDepartureDate", "HeadOfDelegationUserProfileId" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 404,
                columns: new[] { "DelegationArrivalDate", "DelegationDepartureDate", "HeadOfDelegationUserProfileId" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 410,
                columns: new[] { "DelegationArrivalDate", "DelegationDepartureDate", "HeadOfDelegationUserProfileId" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 414,
                columns: new[] { "DelegationArrivalDate", "DelegationDepartureDate", "HeadOfDelegationUserProfileId" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 422,
                columns: new[] { "DelegationArrivalDate", "DelegationDepartureDate", "HeadOfDelegationUserProfileId" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 458,
                columns: new[] { "DelegationArrivalDate", "DelegationDepartureDate", "HeadOfDelegationUserProfileId" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 484,
                columns: new[] { "DelegationArrivalDate", "DelegationDepartureDate", "HeadOfDelegationUserProfileId" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 504,
                columns: new[] { "DelegationArrivalDate", "DelegationDepartureDate", "HeadOfDelegationUserProfileId" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 512,
                columns: new[] { "DelegationArrivalDate", "DelegationDepartureDate", "HeadOfDelegationUserProfileId" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 528,
                columns: new[] { "DelegationArrivalDate", "DelegationDepartureDate", "HeadOfDelegationUserProfileId" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 554,
                columns: new[] { "DelegationArrivalDate", "DelegationDepartureDate", "HeadOfDelegationUserProfileId" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 566,
                columns: new[] { "DelegationArrivalDate", "DelegationDepartureDate", "HeadOfDelegationUserProfileId" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 578,
                columns: new[] { "DelegationArrivalDate", "DelegationDepartureDate", "HeadOfDelegationUserProfileId" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 586,
                columns: new[] { "DelegationArrivalDate", "DelegationDepartureDate", "HeadOfDelegationUserProfileId" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 608,
                columns: new[] { "DelegationArrivalDate", "DelegationDepartureDate", "HeadOfDelegationUserProfileId" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 620,
                columns: new[] { "DelegationArrivalDate", "DelegationDepartureDate", "HeadOfDelegationUserProfileId" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 634,
                columns: new[] { "DelegationArrivalDate", "DelegationDepartureDate", "HeadOfDelegationUserProfileId" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 643,
                columns: new[] { "DelegationArrivalDate", "DelegationDepartureDate", "HeadOfDelegationUserProfileId" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 682,
                columns: new[] { "DelegationArrivalDate", "DelegationDepartureDate", "HeadOfDelegationUserProfileId" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 702,
                columns: new[] { "DelegationArrivalDate", "DelegationDepartureDate", "HeadOfDelegationUserProfileId" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 704,
                columns: new[] { "DelegationArrivalDate", "DelegationDepartureDate", "HeadOfDelegationUserProfileId" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 710,
                columns: new[] { "DelegationArrivalDate", "DelegationDepartureDate", "HeadOfDelegationUserProfileId" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 724,
                columns: new[] { "DelegationArrivalDate", "DelegationDepartureDate", "HeadOfDelegationUserProfileId" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 729,
                columns: new[] { "DelegationArrivalDate", "DelegationDepartureDate", "HeadOfDelegationUserProfileId" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 752,
                columns: new[] { "DelegationArrivalDate", "DelegationDepartureDate", "HeadOfDelegationUserProfileId" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 756,
                columns: new[] { "DelegationArrivalDate", "DelegationDepartureDate", "HeadOfDelegationUserProfileId" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 764,
                columns: new[] { "DelegationArrivalDate", "DelegationDepartureDate", "HeadOfDelegationUserProfileId" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 784,
                columns: new[] { "DelegationArrivalDate", "DelegationDepartureDate", "HeadOfDelegationUserProfileId" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 792,
                columns: new[] { "DelegationArrivalDate", "DelegationDepartureDate", "HeadOfDelegationUserProfileId" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 818,
                columns: new[] { "DelegationArrivalDate", "DelegationDepartureDate", "HeadOfDelegationUserProfileId" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 826,
                columns: new[] { "DelegationArrivalDate", "DelegationDepartureDate", "HeadOfDelegationUserProfileId" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 840,
                columns: new[] { "DelegationArrivalDate", "DelegationDepartureDate", "HeadOfDelegationUserProfileId" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 887,
                columns: new[] { "DelegationArrivalDate", "DelegationDepartureDate", "HeadOfDelegationUserProfileId" },
                values: new object[] { null, null, null });

            migrationBuilder.CreateIndex(
                name: "IX_Countries_HeadOfDelegationUserProfileId",
                table: "Countries",
                column: "HeadOfDelegationUserProfileId");

            migrationBuilder.AddForeignKey(
                name: "FK_Countries_UserProfiles_HeadOfDelegationUserProfileId",
                table: "Countries",
                column: "HeadOfDelegationUserProfileId",
                principalTable: "UserProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Countries_UserProfiles_HeadOfDelegationUserProfileId",
                table: "Countries");

            migrationBuilder.DropIndex(
                name: "IX_Countries_HeadOfDelegationUserProfileId",
                table: "Countries");

            migrationBuilder.DropColumn(
                name: "DelegationArrivalDate",
                table: "Countries");

            migrationBuilder.DropColumn(
                name: "DelegationDepartureDate",
                table: "Countries");

            migrationBuilder.DropColumn(
                name: "HeadOfDelegationUserProfileId",
                table: "Countries");
        }
    }
}
