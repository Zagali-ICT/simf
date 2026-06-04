using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMF.Infrastructure.Persistence.Migrations.App
{
    /// <inheritdoc />
    public partial class D274_AuditFoldAndExhibitorRename : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Booths_Companies_CompanyId",
                table: "Booths");

            migrationBuilder.DropForeignKey(
                name: "FK_BusinessMeetingParticipants_Companies_CompanyId",
                table: "BusinessMeetingParticipants");

            migrationBuilder.DropTable(
                name: "CompanyMemberships");

            migrationBuilder.DropTable(
                name: "Companies");

            migrationBuilder.DropIndex(
                name: "IX_Booths_CompanyId",
                table: "Booths");

            migrationBuilder.RenameColumn(
                name: "CompanyId",
                table: "BusinessMeetingParticipants",
                newName: "ExhibitorId");

            migrationBuilder.RenameIndex(
                name: "IX_BusinessMeetingParticipants_CompanyId",
                table: "BusinessMeetingParticipants",
                newName: "IX_BusinessMeetingParticipants_ExhibitorId");

            migrationBuilder.RenameColumn(
                name: "CompanyId",
                table: "Booths",
                newName: "UpdatedBy");

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                table: "Themes",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "Themes",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedBy",
                table: "Themes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                table: "Speakers",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "Speakers",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedBy",
                table: "Speakers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                table: "SpeakerPresentations",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "SpeakerPresentations",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "SpeakerPresentations",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedBy",
                table: "SpeakerPresentations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                table: "Sessions",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "Sessions",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedBy",
                table: "Sessions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                table: "SessionComments",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "SessionComments",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedBy",
                table: "SessionComments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                table: "SessionCategories",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "SessionCategories",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedBy",
                table: "SessionCategories",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                table: "News",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "News",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedBy",
                table: "News",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                table: "MediaPartners",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "MediaPartners",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedBy",
                table: "MediaPartners",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                table: "Invitations",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "Invitations",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedBy",
                table: "Invitations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                table: "Halls",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "Halls",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedBy",
                table: "Halls",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                table: "Delegations",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "Delegations",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedBy",
                table: "Delegations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                table: "Connections",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "Connections",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "Connections",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedBy",
                table: "Connections",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                table: "Booths",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "Booths",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ExhibitorId",
                table: "Booths",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                table: "Banners",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "Banners",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedBy",
                table: "Banners",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Exhibitors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    NameArabic = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ContactEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    ContactPhone = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Website = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    ContactId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Exhibitors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Exhibitors_Contacts_ContactId",
                        column: x => x.ContactId,
                        principalTable: "Contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExhibitorMemberships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExhibitorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContactName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    RoleLabel = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExhibitorMemberships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExhibitorMemberships_Exhibitors_ExhibitorId",
                        column: x => x.ExhibitorId,
                        principalTable: "Exhibitors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Booths_ExhibitorId",
                table: "Booths",
                column: "ExhibitorId");

            migrationBuilder.CreateIndex(
                name: "IX_ExhibitorMemberships_ExhibitorId",
                table: "ExhibitorMemberships",
                column: "ExhibitorId");

            migrationBuilder.CreateIndex(
                name: "IX_ExhibitorMemberships_UserId",
                table: "ExhibitorMemberships",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Exhibitors_ContactId",
                table: "Exhibitors",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_Exhibitors_IsActive_NameArabic",
                table: "Exhibitors",
                columns: new[] { "IsActive", "NameArabic" });

            migrationBuilder.AddForeignKey(
                name: "FK_Booths_Exhibitors_ExhibitorId",
                table: "Booths",
                column: "ExhibitorId",
                principalTable: "Exhibitors",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BusinessMeetingParticipants_Exhibitors_ExhibitorId",
                table: "BusinessMeetingParticipants",
                column: "ExhibitorId",
                principalTable: "Exhibitors",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Booths_Exhibitors_ExhibitorId",
                table: "Booths");

            migrationBuilder.DropForeignKey(
                name: "FK_BusinessMeetingParticipants_Exhibitors_ExhibitorId",
                table: "BusinessMeetingParticipants");

            migrationBuilder.DropTable(
                name: "ExhibitorMemberships");

            migrationBuilder.DropTable(
                name: "Exhibitors");

            migrationBuilder.DropIndex(
                name: "IX_Booths_ExhibitorId",
                table: "Booths");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Themes");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Themes");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "Themes");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Speakers");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Speakers");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "Speakers");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "SpeakerPresentations");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "SpeakerPresentations");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "SpeakerPresentations");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "SpeakerPresentations");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "SessionComments");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "SessionComments");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "SessionComments");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "SessionCategories");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "SessionCategories");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "SessionCategories");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "News");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "News");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "News");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "MediaPartners");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "MediaPartners");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "MediaPartners");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Invitations");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Invitations");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "Invitations");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Halls");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Halls");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "Halls");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Delegations");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Delegations");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "Delegations");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Connections");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Connections");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Connections");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "Connections");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Booths");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Booths");

            migrationBuilder.DropColumn(
                name: "ExhibitorId",
                table: "Booths");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Banners");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Banners");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "Banners");

            migrationBuilder.RenameColumn(
                name: "ExhibitorId",
                table: "BusinessMeetingParticipants",
                newName: "CompanyId");

            migrationBuilder.RenameIndex(
                name: "IX_BusinessMeetingParticipants_ExhibitorId",
                table: "BusinessMeetingParticipants",
                newName: "IX_BusinessMeetingParticipants_CompanyId");

            migrationBuilder.RenameColumn(
                name: "UpdatedBy",
                table: "Booths",
                newName: "CompanyId");

            migrationBuilder.CreateTable(
                name: "Companies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContactId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ContactEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    ContactPhone = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    NameArabic = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Website = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Companies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Companies_Contacts_ContactId",
                        column: x => x.ContactId,
                        principalTable: "Contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CompanyMemberships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContactName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RoleLabel = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyMemberships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanyMemberships_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Booths_CompanyId",
                table: "Booths",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Companies_ContactId",
                table: "Companies",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_Companies_IsActive_Type_NameArabic",
                table: "Companies",
                columns: new[] { "IsActive", "Type", "NameArabic" });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyMemberships_CompanyId",
                table: "CompanyMemberships",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyMemberships_UserId",
                table: "CompanyMemberships",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Booths_Companies_CompanyId",
                table: "Booths",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BusinessMeetingParticipants_Companies_CompanyId",
                table: "BusinessMeetingParticipants",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
