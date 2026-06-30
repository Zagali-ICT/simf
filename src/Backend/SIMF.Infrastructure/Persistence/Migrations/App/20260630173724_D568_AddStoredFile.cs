using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMF.Infrastructure.Persistence.Migrations.App
{
    /// <inheritdoc />
    public partial class D568_AddStoredFile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StoredFiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Service = table.Column<int>(type: "int", nullable: false),
                    SensitivityTier = table.Column<int>(type: "int", nullable: false),
                    FileType = table.Column<int>(type: "int", nullable: false),
                    SourceType = table.Column<int>(type: "int", nullable: false),
                    IsEncrypted = table.Column<bool>(type: "bit", nullable: false),
                    CipherFormatVersion = table.Column<byte>(type: "tinyint", nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    ExternalUrl = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    OriginalFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
                    ContentType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    Sha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    IsDeletable = table.Column<bool>(type: "bit", nullable: false),
                    RetainUntilUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SecureDestroyedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    OwnerEntityType = table.Column<int>(type: "int", nullable: false),
                    OwnerEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoredFiles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StoredFiles_CreatedBy",
                table: "StoredFiles",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_StoredFiles_OwnerEntityType_OwnerEntityId",
                table: "StoredFiles",
                columns: new[] { "OwnerEntityType", "OwnerEntityId" },
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_StoredFiles_RetainUntilUtc",
                table: "StoredFiles",
                column: "RetainUntilUtc",
                filter: "[IsActive] = 1 AND [RetainUntilUtc] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StoredFiles_Service_IsActive",
                table: "StoredFiles",
                columns: new[] { "Service", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StoredFiles");
        }
    }
}
