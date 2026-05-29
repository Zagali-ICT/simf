using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SIMF.Infrastructure.Persistence.Migrations.App
{
    /// <inheritdoc />
    public partial class InitialModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "app");

            migrationBuilder.CreateTable(
                name: "Countries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    PhonePrefix = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Countries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Gates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    NameArabic = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    DescriptionArabic = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    DirectionMode = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Gates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Halls",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    NameArabic = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Capacity = table.Column<int>(type: "int", nullable: false),
                    Floor = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    EquipmentNotes = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Halls", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OperationLog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TimestampUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    SubjectEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    SubjectUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceIp = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ErrorCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Detail = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationLog", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RowAudits",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    TableName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Operation = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    PrimaryKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    OldValuesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewValuesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AffectedColumns = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RowAudits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ScanIdempotency",
                columns: table => new
                {
                    Key = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    GateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ResponseHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ScanId = table.Column<long>(type: "bigint", nullable: true),
                    StoredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScanIdempotency", x => new { x.Key, x.GateId });
                });

            migrationBuilder.CreateTable(
                name: "Speakers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    NameArabic = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Rank = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CountryId = table.Column<int>(type: "int", nullable: true),
                    UserProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Bio = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    BioArabic = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    Qualifications = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    QualificationsArabic = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    TrainingExperience = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    TrainingExperienceArabic = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    Awards = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    AwardsArabic = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    AllowsMeetingRequests = table.Column<bool>(type: "bit", nullable: false),
                    AllowsDataSharing = table.Column<bool>(type: "bit", nullable: false),
                    FacebookUrl = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    LinkedInUrl = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    XUrl = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    PhotoRelativePath = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Speakers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Themes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    NameArabic = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    DescriptionArabic = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    PageColor = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Themes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GateAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    AssignedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AssignedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RevokedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GateAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GateAssignments_Gates_GateId",
                        column: x => x.GateId,
                        principalTable: "Gates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GateProfileTypeAllow",
                columns: table => new
                {
                    GateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProfileTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GateProfileTypeAllow", x => new { x.GateId, x.ProfileTypeId });
                    table.ForeignKey(
                        name: "FK_GateProfileTypeAllow_Gates_GateId",
                        column: x => x.GateId,
                        principalTable: "Gates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GateScans",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    QrIdAtScan = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Direction = table.Column<int>(type: "int", nullable: false),
                    Outcome = table.Column<int>(type: "int", nullable: false),
                    DenialReasonCode = table.Column<int>(type: "int", nullable: true),
                    ScannedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ClientScannedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ScannedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Source = table.Column<int>(type: "int", nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GateScans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GateScans_Gates_GateId",
                        column: x => x.GateId,
                        principalTable: "Gates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Countries",
                columns: new[] { "Id", "Code", "CreatedAt", "DisplayOrder", "IsActive", "NameAr", "NameEn", "PhonePrefix", "UpdatedAt" },
                values: new object[,]
                {
                    { 32, "AR", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 630, true, "الأرجنتين", "Argentina", "+54", null },
                    { 36, "AU", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 520, true, "أستراليا", "Australia", "+61", null },
                    { 40, "AT", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 300, true, "النمسا", "Austria", "+43", null },
                    { 48, "BH", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 10, true, "البحرين", "Bahrain", "+973", null },
                    { 50, "BD", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 450, true, "بنغلاديش", "Bangladesh", "+880", null },
                    { 56, "BE", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 280, true, "بلجيكا", "Belgium", "+32", null },
                    { 76, "BR", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 610, true, "البرازيل", "Brazil", "+55", null },
                    { 124, "CA", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 600, true, "كندا", "Canada", "+1", null },
                    { 156, "CN", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 420, true, "الصين", "China", "+86", null },
                    { 208, "DK", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 330, true, "الدنمارك", "Denmark", "+45", null },
                    { 231, "ET", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 730, true, "إثيوبيا", "Ethiopia", "+251", null },
                    { 246, "FI", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 340, true, "فنلندا", "Finland", "+358", null },
                    { 250, "FR", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 220, true, "فرنسا", "France", "+33", null },
                    { 262, "DJ", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 170, true, "جيبوتي", "Djibouti", "+253", null },
                    { 275, "PS", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 160, true, "فلسطين", "Palestine", "+970", null },
                    { 276, "DE", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 230, true, "ألمانيا", "Germany", "+49", null },
                    { 300, "GR", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 360, true, "اليونان", "Greece", "+30", null },
                    { 356, "IN", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 430, true, "الهند", "India", "+91", null },
                    { 360, "ID", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 460, true, "إندونيسيا", "Indonesia", "+62", null },
                    { 364, "IR", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 140, true, "إيران", "Iran", "+98", null },
                    { 368, "IQ", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 150, true, "العراق", "Iraq", "+964", null },
                    { 372, "IE", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 350, true, "أيرلندا", "Ireland", "+353", null },
                    { 380, "IT", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 240, true, "إيطاليا", "Italy", "+39", null },
                    { 392, "JP", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 400, true, "اليابان", "Japan", "+81", null },
                    { 400, "JO", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 80, true, "الأردن", "Jordan", "+962", null },
                    { 404, "KE", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 720, true, "كينيا", "Kenya", "+254", null },
                    { 410, "KR", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 410, true, "كوريا الجنوبية", "South Korea", "+82", null },
                    { 414, "KW", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 20, true, "الكويت", "Kuwait", "+965", null },
                    { 422, "LB", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 90, true, "لبنان", "Lebanon", "+961", null },
                    { 458, "MY", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 470, true, "ماليزيا", "Malaysia", "+60", null },
                    { 484, "MX", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 620, true, "المكسيك", "Mexico", "+52", null },
                    { 504, "MA", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 100, true, "المغرب", "Morocco", "+212", null },
                    { 512, "OM", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 30, true, "عُمان", "Oman", "+968", null },
                    { 528, "NL", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 270, true, "هولندا", "Netherlands", "+31", null },
                    { 554, "NZ", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 530, true, "نيوزيلندا", "New Zealand", "+64", null },
                    { 566, "NG", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 710, true, "نيجيريا", "Nigeria", "+234", null },
                    { 578, "NO", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 320, true, "النرويج", "Norway", "+47", null },
                    { 586, "PK", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 440, true, "باكستان", "Pakistan", "+92", null },
                    { 608, "PH", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 510, true, "الفلبين", "Philippines", "+63", null },
                    { 620, "PT", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 260, true, "البرتغال", "Portugal", "+351", null },
                    { 634, "QA", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 40, true, "قطر", "Qatar", "+974", null },
                    { 643, "RU", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 370, true, "روسيا", "Russia", "+7", null },
                    { 682, "SA", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 50, true, "المملكة العربية السعودية", "Saudi Arabia", "+966", null },
                    { 702, "SG", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 480, true, "سنغافورة", "Singapore", "+65", null },
                    { 704, "VN", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 500, true, "فيتنام", "Viet Nam", "+84", null },
                    { 710, "ZA", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 700, true, "جنوب أفريقيا", "South Africa", "+27", null },
                    { 724, "ES", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 250, true, "إسبانيا", "Spain", "+34", null },
                    { 729, "SD", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 120, true, "السودان", "Sudan", "+249", null },
                    { 752, "SE", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 310, true, "السويد", "Sweden", "+46", null },
                    { 756, "CH", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 290, true, "سويسرا", "Switzerland", "+41", null },
                    { 764, "TH", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 490, true, "تايلاند", "Thailand", "+66", null },
                    { 784, "AE", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 60, true, "الإمارات العربية المتحدة", "United Arab Emirates", "+971", null },
                    { 792, "TR", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 110, true, "تركيا", "Türkiye", "+90", null },
                    { 818, "EG", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 70, true, "مصر", "Egypt", "+20", null },
                    { 826, "GB", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 210, true, "المملكة المتحدة", "United Kingdom", "+44", null },
                    { 840, "US", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 200, true, "الولايات المتحدة الأمريكية", "United States", "+1", null },
                    { 887, "YE", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 130, true, "اليمن", "Yemen", "+967", null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Countries_Code",
                table: "Countries",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Countries_IsActive_DisplayOrder",
                table: "Countries",
                columns: new[] { "IsActive", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_GateAssignments_GateId_IsActive",
                table: "GateAssignments",
                columns: new[] { "GateId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_GateAssignments_UserId_IsActive",
                table: "GateAssignments",
                columns: new[] { "UserId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Gates_Code",
                table: "Gates",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Gates_IsActive_Name",
                table: "Gates",
                columns: new[] { "IsActive", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_GateScan_Gate_ScannedAt",
                table: "GateScans",
                columns: new[] { "GateId", "ScannedAtUtc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_GateScan_Gate_UserProfile_5sWindow",
                table: "GateScans",
                columns: new[] { "GateId", "UserProfileId", "ScannedAtUtc" },
                descending: new[] { false, false, true },
                filter: "[UserProfileId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_GateScan_ScannedBy_ScannedAt",
                table: "GateScans",
                columns: new[] { "ScannedByUserId", "ScannedAtUtc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_GateScan_UserProfile_LastAllowed",
                table: "GateScans",
                columns: new[] { "UserProfileId", "ScannedAtUtc" },
                descending: new[] { false, true },
                filter: "[Outcome] = 0 AND [UserProfileId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_GateScan_Idempotency",
                table: "GateScans",
                columns: new[] { "IdempotencyKey", "GateId" },
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Halls_Code",
                table: "Halls",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Halls_IsActive_Name",
                table: "Halls",
                columns: new[] { "IsActive", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_OperationLog_EventType_TimestampUtc",
                table: "OperationLog",
                columns: new[] { "EventType", "TimestampUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OperationLog_SubjectEmail",
                table: "OperationLog",
                column: "SubjectEmail");

            migrationBuilder.CreateIndex(
                name: "IX_OperationLog_TimestampUtc",
                table: "OperationLog",
                column: "TimestampUtc");

            migrationBuilder.CreateIndex(
                name: "IX_RowAudits_ActorUserId_OccurredAt",
                schema: "app",
                table: "RowAudits",
                columns: new[] { "ActorUserId", "OccurredAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_RowAudits_TableName_OccurredAt",
                schema: "app",
                table: "RowAudits",
                columns: new[] { "TableName", "OccurredAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_ScanIdempotency_StoredAt",
                table: "ScanIdempotency",
                column: "StoredAt");

            migrationBuilder.CreateIndex(
                name: "IX_Speakers_Code",
                table: "Speakers",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Speakers_CountryId",
                table: "Speakers",
                column: "CountryId");

            migrationBuilder.CreateIndex(
                name: "IX_Speakers_IsActive_DisplayOrder",
                table: "Speakers",
                columns: new[] { "IsActive", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Speakers_UserProfileId",
                table: "Speakers",
                column: "UserProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_Themes_Code",
                table: "Themes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Themes_IsActive_DisplayOrder",
                table: "Themes",
                columns: new[] { "IsActive", "DisplayOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Countries");

            migrationBuilder.DropTable(
                name: "GateAssignments");

            migrationBuilder.DropTable(
                name: "GateProfileTypeAllow");

            migrationBuilder.DropTable(
                name: "GateScans");

            migrationBuilder.DropTable(
                name: "Halls");

            migrationBuilder.DropTable(
                name: "OperationLog");

            migrationBuilder.DropTable(
                name: "RowAudits",
                schema: "app");

            migrationBuilder.DropTable(
                name: "ScanIdempotency");

            migrationBuilder.DropTable(
                name: "Speakers");

            migrationBuilder.DropTable(
                name: "Themes");

            migrationBuilder.DropTable(
                name: "Gates");
        }
    }
}
