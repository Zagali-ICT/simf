using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMF.Infrastructure.Persistence.Migrations.App
{
    /// <inheritdoc />
    public partial class D272_AuditStampAndProfileTypeCollapse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProfileTypes_UserType_IsActive",
                table: "ProfileTypes");

            migrationBuilder.DropIndex(
                name: "IX_Companies_IsActive_Type_NameAr",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "UserType",
                table: "ProfileTypes");

            migrationBuilder.RenameColumn(
                name: "EnglishName",
                table: "UserProfiles",
                newName: "NameArabic");

            migrationBuilder.RenameColumn(
                name: "ArabicName",
                table: "UserProfiles",
                newName: "Name");

            migrationBuilder.RenameColumn(
                name: "NameEn",
                table: "Sponsors",
                newName: "NameArabic");

            migrationBuilder.RenameColumn(
                name: "NameAr",
                table: "Sponsors",
                newName: "Name");

            migrationBuilder.RenameColumn(
                name: "IsVisitor",
                table: "ProfileTypes",
                newName: "IsForVisitor");

            migrationBuilder.RenameIndex(
                name: "IX_ProfileTypes_IsVisitor_IsActive",
                table: "ProfileTypes",
                newName: "IX_ProfileTypes_IsForVisitor_IsActive");

            migrationBuilder.RenameColumn(
                name: "NameEn",
                table: "Organisations",
                newName: "Name");

            migrationBuilder.RenameColumn(
                name: "NameAr",
                table: "Organisations",
                newName: "NameArabic");

            migrationBuilder.RenameIndex(
                name: "IX_Organisations_IsActive_NameAr",
                table: "Organisations",
                newName: "IX_Organisations_IsActive_NameArabic");

            migrationBuilder.RenameColumn(
                name: "TitleAr",
                table: "MediaItems",
                newName: "TitleArabic");

            migrationBuilder.RenameColumn(
                name: "AssignedByUserId",
                table: "GateAssignments",
                newName: "CreateBy");

            migrationBuilder.RenameColumn(
                name: "AssignedAt",
                table: "GateAssignments",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "NameEn",
                table: "FaqGroups",
                newName: "NameArabic");

            migrationBuilder.RenameColumn(
                name: "NameAr",
                table: "FaqGroups",
                newName: "Name");

            migrationBuilder.RenameColumn(
                name: "NameEn",
                table: "Contacts",
                newName: "Name");

            migrationBuilder.RenameColumn(
                name: "NameAr",
                table: "Contacts",
                newName: "NameArabic");

            migrationBuilder.RenameIndex(
                name: "IX_Contacts_IsActive_NameAr",
                table: "Contacts",
                newName: "IX_Contacts_IsActive_NameArabic");

            migrationBuilder.RenameColumn(
                name: "NameEn",
                table: "Companies",
                newName: "NameArabic");

            migrationBuilder.RenameColumn(
                name: "NameAr",
                table: "Companies",
                newName: "Name");

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                table: "VenueMapNodes",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "VenueMapNodes",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedBy",
                table: "VenueMapNodes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                table: "UserProfiles",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "UserProfiles",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "UserProfiles",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedBy",
                table: "UserProfiles",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                table: "SystemSettings",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "SystemSettings",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedBy",
                table: "SystemSettings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                table: "Sponsors",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "Sponsors",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedBy",
                table: "Sponsors",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                table: "Ratings",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "Ratings",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedBy",
                table: "Ratings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                table: "ProfileTypes",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "ProfileTypes",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedBy",
                table: "ProfileTypes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                table: "Organisations",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "Organisations",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedBy",
                table: "Organisations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                table: "MediaItems",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "MediaItems",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedBy",
                table: "MediaItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                table: "Interests",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "Interests",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedBy",
                table: "Interests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                table: "Gates",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "Gates",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedBy",
                table: "Gates",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                table: "FaqGroups",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "FaqGroups",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedBy",
                table: "FaqGroups",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                table: "FaqEntries",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "FaqEntries",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedBy",
                table: "FaqEntries",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                table: "Contacts",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "Contacts",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedBy",
                table: "Contacts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                table: "CompanyMemberships",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "CompanyMemberships",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedBy",
                table: "CompanyMemberships",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                table: "Companies",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "Companies",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedBy",
                table: "Companies",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                table: "ArchiveEditions",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "ArchiveEditions",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedBy",
                table: "ArchiveEditions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Companies_IsActive_Type_NameArabic",
                table: "Companies",
                columns: new[] { "IsActive", "Type", "NameArabic" });

            migrationBuilder.AddForeignKey(
                name: "FK_CompanyMemberships_Companies_CompanyId",
                table: "CompanyMemberships",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GateScans_UserProfiles_UserProfileId",
                table: "GateScans",
                column: "UserProfileId",
                principalTable: "UserProfiles",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CompanyMemberships_Companies_CompanyId",
                table: "CompanyMemberships");

            migrationBuilder.DropForeignKey(
                name: "FK_GateScans_UserProfiles_UserProfileId",
                table: "GateScans");

            migrationBuilder.DropIndex(
                name: "IX_Companies_IsActive_Type_NameArabic",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "VenueMapNodes");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "VenueMapNodes");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "VenueMapNodes");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Sponsors");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Sponsors");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "Sponsors");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Ratings");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Ratings");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "Ratings");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "ProfileTypes");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "ProfileTypes");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "ProfileTypes");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Organisations");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Organisations");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "Organisations");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "MediaItems");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "MediaItems");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "MediaItems");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Interests");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Interests");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "Interests");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Gates");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Gates");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "Gates");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "FaqGroups");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "FaqGroups");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "FaqGroups");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "FaqEntries");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "FaqEntries");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "FaqEntries");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Contacts");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Contacts");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "Contacts");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "CompanyMemberships");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "CompanyMemberships");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "CompanyMemberships");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "ArchiveEditions");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "ArchiveEditions");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "ArchiveEditions");

            migrationBuilder.RenameColumn(
                name: "NameArabic",
                table: "UserProfiles",
                newName: "EnglishName");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "UserProfiles",
                newName: "ArabicName");

            migrationBuilder.RenameColumn(
                name: "NameArabic",
                table: "Sponsors",
                newName: "NameEn");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "Sponsors",
                newName: "NameAr");

            migrationBuilder.RenameColumn(
                name: "IsForVisitor",
                table: "ProfileTypes",
                newName: "IsVisitor");

            migrationBuilder.RenameIndex(
                name: "IX_ProfileTypes_IsForVisitor_IsActive",
                table: "ProfileTypes",
                newName: "IX_ProfileTypes_IsVisitor_IsActive");

            migrationBuilder.RenameColumn(
                name: "NameArabic",
                table: "Organisations",
                newName: "NameAr");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "Organisations",
                newName: "NameEn");

            migrationBuilder.RenameIndex(
                name: "IX_Organisations_IsActive_NameArabic",
                table: "Organisations",
                newName: "IX_Organisations_IsActive_NameAr");

            migrationBuilder.RenameColumn(
                name: "TitleArabic",
                table: "MediaItems",
                newName: "TitleAr");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "GateAssignments",
                newName: "AssignedAt");

            migrationBuilder.RenameColumn(
                name: "CreateBy",
                table: "GateAssignments",
                newName: "AssignedByUserId");

            migrationBuilder.RenameColumn(
                name: "NameArabic",
                table: "FaqGroups",
                newName: "NameEn");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "FaqGroups",
                newName: "NameAr");

            migrationBuilder.RenameColumn(
                name: "NameArabic",
                table: "Contacts",
                newName: "NameAr");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "Contacts",
                newName: "NameEn");

            migrationBuilder.RenameIndex(
                name: "IX_Contacts_IsActive_NameArabic",
                table: "Contacts",
                newName: "IX_Contacts_IsActive_NameAr");

            migrationBuilder.RenameColumn(
                name: "NameArabic",
                table: "Companies",
                newName: "NameEn");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "Companies",
                newName: "NameAr");

            migrationBuilder.AddColumn<string>(
                name: "UserType",
                table: "ProfileTypes",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_ProfileTypes_UserType_IsActive",
                table: "ProfileTypes",
                columns: new[] { "UserType", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Companies_IsActive_Type_NameAr",
                table: "Companies",
                columns: new[] { "IsActive", "Type", "NameAr" });
        }
    }
}
