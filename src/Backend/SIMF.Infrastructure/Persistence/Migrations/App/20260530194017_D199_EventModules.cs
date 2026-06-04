using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMF.Infrastructure.Persistence.Migrations.App
{
    /// <inheritdoc />
    public partial class D199_EventModules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ArchiveEditions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    TitleEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TitleAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SummaryEn = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    SummaryAr = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    Attendees = table.Column<int>(type: "int", nullable: false),
                    Sessions = table.Column<int>(type: "int", nullable: false),
                    Speakers = table.Column<int>(type: "int", nullable: false),
                    CoverImageRelativePath = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchiveEditions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Booths",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ExhibitorNameEn = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ExhibitorNameAr = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    SectorEn = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    SectorAr = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    DescriptionEn = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    DescriptionAr = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    HallId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MapX = table.Column<double>(type: "float", nullable: true),
                    MapY = table.Column<double>(type: "float", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Booths", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Booths_Halls_HallId",
                        column: x => x.HallId,
                        principalTable: "Halls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MediaItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    TitleEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TitleAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ImageRelativePath = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Url = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    ThumbnailRelativePath = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    AlbumEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AlbumAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MediaPartners",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    LogoRelativePath = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Url = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaPartners", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "News",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TitleEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TitleAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ExcerptEn = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ExcerptAr = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    BodyEn = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    BodyAr = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    CategoryEn = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CategoryAr = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ImageRelativePath = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    PublishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_News", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Ratings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Stars = table.Column<int>(type: "int", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ratings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SessionComments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Body = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AiFilterVerdict = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModeratedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ModeratedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SessionComments_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "Sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sponsors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Tier = table.Column<int>(type: "int", nullable: false),
                    LogoRelativePath = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Url = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sponsors", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArchiveEditions_IsActive_Year",
                table: "ArchiveEditions",
                columns: new[] { "IsActive", "Year" });

            migrationBuilder.CreateIndex(
                name: "IX_ArchiveEditions_Year",
                table: "ArchiveEditions",
                column: "Year",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Booths_Code",
                table: "Booths",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Booths_HallId",
                table: "Booths",
                column: "HallId");

            migrationBuilder.CreateIndex(
                name: "IX_Booths_IsActive",
                table: "Booths",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_MediaItems_IsActive_AlbumEn_DisplayOrder",
                table: "MediaItems",
                columns: new[] { "IsActive", "AlbumEn", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_MediaPartners_IsActive_DisplayOrder",
                table: "MediaPartners",
                columns: new[] { "IsActive", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_News_IsActive_PublishedAt",
                table: "News",
                columns: new[] { "IsActive", "PublishedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Ratings_UserId",
                table: "Ratings",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SessionComments_SessionId_Status_IsActive",
                table: "SessionComments",
                columns: new[] { "SessionId", "Status", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_SessionComments_UserId",
                table: "SessionComments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Sponsors_IsActive_Tier_DisplayOrder",
                table: "Sponsors",
                columns: new[] { "IsActive", "Tier", "DisplayOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArchiveEditions");

            migrationBuilder.DropTable(
                name: "Booths");

            migrationBuilder.DropTable(
                name: "MediaItems");

            migrationBuilder.DropTable(
                name: "MediaPartners");

            migrationBuilder.DropTable(
                name: "News");

            migrationBuilder.DropTable(
                name: "Ratings");

            migrationBuilder.DropTable(
                name: "SessionComments");

            migrationBuilder.DropTable(
                name: "Sponsors");
        }
    }
}
