using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SIMF.Infrastructure.Persistence.Migrations.App
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "app");

            migrationBuilder.CreateSequence(
                name: "RegistrationReferenceSequence");

            migrationBuilder.CreateTable(
                name: "AiChatMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiChatMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AiInvocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PromptKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Feature = table.Column<int>(type: "int", nullable: false),
                    Provider = table.Column<int>(type: "int", nullable: false),
                    Model = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    InputJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OutputText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TokensInput = table.Column<int>(type: "int", nullable: true),
                    TokensOutput = table.Column<int>(type: "int", nullable: true),
                    LatencyMs = table.Column<int>(type: "int", nullable: false),
                    ErrorCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CallerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CallerKind = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiInvocations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AiPrompts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Feature = table.Column<int>(type: "int", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    DisplayNameArabic = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    DescriptionArabic = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Provider = table.Column<int>(type: "int", nullable: false),
                    Model = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SystemPrompt = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UserPromptTemplate = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Temperature = table.Column<double>(type: "float", nullable: false),
                    MaxOutputTokens = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiPrompts", x => x.Id);
                    table.CheckConstraint("CK_AiPrompts_MaxOutputTokens", "[MaxOutputTokens] >= 1 AND [MaxOutputTokens] <= 8000");
                    table.CheckConstraint("CK_AiPrompts_Temperature", "[Temperature] >= 0 AND [Temperature] <= 2");
                });

            migrationBuilder.CreateTable(
                name: "ArchiveVisibility",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsVisible = table.Column<bool>(type: "bit", nullable: false),
                    LastChangedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastChangedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchiveVisibility", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BadgeBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameArabic = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsDelegate = table.Column<bool>(type: "bit", nullable: false),
                    RecipientEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BadgeBatches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BadgeUpdateRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedJobTitle = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CurrentJobTitle = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ResponseNote = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RespondedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RespondedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BadgeUpdateRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Connections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequesterUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    State = table.Column<int>(type: "int", nullable: false),
                    RespondedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PairLowUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PairHighUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Connections", x => x.Id);
                    table.CheckConstraint("CK_Connections_NotSelf", "[RequesterUserId] <> [TargetUserId]");
                });

            migrationBuilder.CreateTable(
                name: "ContactInquiries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    SubmittedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsHandled = table.Column<bool>(type: "bit", nullable: false),
                    HandledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    HandledByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContactInquiries", x => x.Id);
                    table.CheckConstraint("CK_ContactInquiries_HandledPin", "([IsHandled] = 0 AND [HandledAt] IS NULL AND [HandledByUserId] IS NULL) OR ([IsHandled] = 1 AND [HandledAt] IS NOT NULL AND [HandledByUserId] IS NOT NULL)");
                });

            migrationBuilder.CreateTable(
                name: "ContentBlocks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    ContentArabic = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    LastUpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentBlocks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DevicePositionPings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HallId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CapturedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Latitude = table.Column<double>(type: "float", nullable: false),
                    Longitude = table.Column<double>(type: "float", nullable: false),
                    AccuracyMeters = table.Column<double>(type: "float", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DevicePositionPings", x => x.Id);
                    table.CheckConstraint("CK_DevicePositionPings_Coordinates", "[Latitude] >= -90 AND [Latitude] <= 90 AND [Longitude] >= -180 AND [Longitude] <= 180");
                });

            migrationBuilder.CreateTable(
                name: "EmailTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(48)", maxLength: 48, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    BodyEn = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BodyAr = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EventEdition",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    OpenedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OpenedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastReissueCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventEdition", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FaqGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    NameArabic = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FaqGroups", x => x.Id);
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
                    FacilityNotes = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    Purpose = table.Column<int>(type: "int", nullable: false),
                    SeatSelectionMode = table.Column<int>(type: "int", nullable: false),
                    GeofenceCenterLat = table.Column<double>(type: "float", nullable: true),
                    GeofenceCenterLon = table.Column<double>(type: "float", nullable: true),
                    GeofenceRadiusMeters = table.Column<double>(type: "float", nullable: true),
                    ArrivalGraceMinutes = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Halls", x => x.Id);
                    table.CheckConstraint("CK_Halls_ArrivalGrace", "[ArrivalGraceMinutes] IS NULL OR ([ArrivalGraceMinutes] >= 0 AND [ArrivalGraceMinutes] <= 240)");
                    table.CheckConstraint("CK_Halls_Capacity", "[Capacity] >= 0");
                    table.CheckConstraint("CK_Halls_Geofence", "([GeofenceCenterLat] IS NULL AND [GeofenceCenterLon] IS NULL AND [GeofenceRadiusMeters] IS NULL) OR ([GeofenceCenterLat] IS NOT NULL AND [GeofenceCenterLon] IS NOT NULL AND [GeofenceRadiusMeters] IS NOT NULL AND [GeofenceCenterLat] >= -90 AND [GeofenceCenterLat] <= 90 AND [GeofenceCenterLon] >= -180 AND [GeofenceCenterLon] <= 180 AND [GeofenceRadiusMeters] > 0 AND [GeofenceRadiusMeters] <= 100000)");
                });

            migrationBuilder.CreateTable(
                name: "Interests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    NameArabic = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Interests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotificationBroadcasts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetMode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AudienceScope = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TitleArabic = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    BodyArabic = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    TotalRecipients = table.Column<int>(type: "int", nullable: false),
                    Dispatched = table.Column<int>(type: "int", nullable: false),
                    EmailsEnqueued = table.Column<int>(type: "int", nullable: false),
                    Skipped = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Error = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationBroadcasts", x => x.Id);
                    table.CheckConstraint("CK_NotificationBroadcasts_TargetArc", "([TargetMode] = 'Session' AND [SessionId] IS NOT NULL AND [AudienceScope] IS NULL) OR ([TargetMode] = 'Audience' AND [AudienceScope] IS NOT NULL AND [SessionId] IS NULL)");
                });

            migrationBuilder.CreateTable(
                name: "OperationLog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    SubjectEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    SubjectUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubjectDisplayName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActorDisplayName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
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
                name: "Organisations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    NameArabic = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    CommercialRegistration = table.Column<string>(type: "nvarchar(700)", maxLength: 700, nullable: true),
                    Sector = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    City = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    Website = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Organisations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ParticipationDocumentRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentType = table.Column<int>(type: "int", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ResponseNote = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RespondedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RespondedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParticipationDocumentRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProfileTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    NameArabic = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    IsForVisitor = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    PageColor = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    MobileAppRole = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false, defaultValue: "None"),
                    IsVipTier = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsAppRegisterable = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    ShowInPartnerDirectory = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Code = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfileTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProgrammeDays",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    TitleArabic = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    RatingPromptSent = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProgrammeDays", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RatingTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    NameArabic = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Scope = table.Column<int>(type: "int", nullable: false),
                    HasOverallStars = table.Column<bool>(type: "bit", nullable: false),
                    AllowComment = table.Column<bool>(type: "bit", nullable: false),
                    CommentLabel = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CommentLabelArabic = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    IsSystem = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RatingTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Regions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    NameArabic = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Regions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RegistrationGate",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsOpen = table.Column<bool>(type: "bit", nullable: false),
                    AutoClose = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastChangedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastChangedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistrationGate", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RowAudits",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TableName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Operation = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    PrimaryKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActorDisplayName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
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
                name: "SavedContacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubjectUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedContacts", x => x.Id);
                    table.CheckConstraint("CK_SavedContacts_NotSelf", "[OwnerUserId] <> [SubjectUserId]");
                });

            migrationBuilder.CreateTable(
                name: "ScanIdempotency",
                columns: table => new
                {
                    Key = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    GateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    ResponseHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    ScanId = table.Column<long>(type: "bigint", nullable: true),
                    StoredAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScanIdempotency", x => new { x.Key, x.GateId });
                });

            migrationBuilder.CreateTable(
                name: "SessionCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    NameArabic = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionCategories", x => x.Id);
                });

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
                    Sha256 = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: true),
                    IsDeletable = table.Column<bool>(type: "bit", nullable: false),
                    RetainUntil = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SecureDestroyedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OwnerEntityType = table.Column<int>(type: "int", nullable: false),
                    OwnerEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoredFiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SystemSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemSettings", x => x.Id);
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
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Themes", x => x.Id);
                    table.CheckConstraint("CK_Themes_DisplayOrder", "[DisplayOrder] >= 0");
                });

            migrationBuilder.CreateTable(
                name: "VisitorShareTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Token = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VisitorShareTokens", x => x.Id);
                    table.CheckConstraint("CK_VisitorShareTokens_RevocationPin", "([IsActive] = 1 AND [RevokedAt] IS NULL) OR ([IsActive] = 0 AND [RevokedAt] IS NOT NULL)");
                });

            migrationBuilder.CreateTable(
                name: "AiPromptHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AiPromptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    SystemPrompt = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UserPromptTemplate = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Provider = table.Column<int>(type: "int", nullable: false),
                    Model = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Temperature = table.Column<double>(type: "float", nullable: false),
                    MaxOutputTokens = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    ContentHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CapturedFromUpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CapturedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiPromptHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiPromptHistory_AiPrompts_AiPromptId",
                        column: x => x.AiPromptId,
                        principalTable: "AiPrompts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FaqEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FaqGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Question = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    QuestionArabic = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Answer = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    AnswerArabic = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FaqEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FaqEntries_FaqGroups_FaqGroupId",
                        column: x => x.FaqGroupId,
                        principalTable: "FaqGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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
                    HallId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Gates", x => x.Id);
                    table.CheckConstraint("CK_Gates_DirectionModeRange", "[DirectionMode] BETWEEN 0 AND 2");
                    table.ForeignKey(
                        name: "FK_Gates_Halls_HallId",
                        column: x => x.HallId,
                        principalTable: "Halls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HallAllocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HallId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Purpose = table.Column<int>(type: "int", nullable: false),
                    Mode = table.Column<int>(type: "int", nullable: false),
                    UnitCount = table.Column<int>(type: "int", nullable: true),
                    RowColumnSpec = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Start = table.Column<DateTime>(type: "datetime2", nullable: false),
                    End = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReleasedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HallAllocations", x => x.Id);
                    table.CheckConstraint("CK_HallAllocations_RowColumnSpec", "([Mode] = 2 AND [RowColumnSpec] IS NOT NULL) OR ([Mode] <> 2 AND [RowColumnSpec] IS NULL)");
                    table.CheckConstraint("CK_HallAllocations_TimeWindow", "[End] > [Start]");
                    table.CheckConstraint("CK_HallAllocations_UnitCount", "([Mode] = 1 AND [UnitCount] >= 1) OR ([Mode] <> 1 AND [UnitCount] IS NULL)");
                    table.ForeignKey(
                        name: "FK_HallAllocations_Halls_HallId",
                        column: x => x.HallId,
                        principalTable: "Halls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HallAvailabilityWindows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HallId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Start = table.Column<DateTime>(type: "datetime2", nullable: false),
                    End = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SlotMinutes = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HallAvailabilityWindows", x => x.Id);
                    table.CheckConstraint("CK_HallAvailabilityWindows_SlotMinutes", "[SlotMinutes] >= 5 AND [SlotMinutes] <= 480");
                    table.CheckConstraint("CK_HallAvailabilityWindows_TimeWindow", "[End] > [Start]");
                    table.ForeignKey(
                        name: "FK_HallAvailabilityWindows_Halls_HallId",
                        column: x => x.HallId,
                        principalTable: "Halls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HallSeatLayouts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HallId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowLabels = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    SeatsPerRow = table.Column<int>(type: "int", nullable: false),
                    SeatCounts = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    SeatTiers = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HallSeatLayouts", x => x.Id);
                    table.CheckConstraint("CK_HallSeatLayouts_SeatsPerRow", "[SeatsPerRow] >= 1 AND [SeatsPerRow] <= 80");
                    table.ForeignKey(
                        name: "FK_HallSeatLayouts_Halls_HallId",
                        column: x => x.HallId,
                        principalTable: "Halls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MeetingTables",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HallId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    RowLabel = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: true),
                    ColumnNumber = table.Column<int>(type: "int", nullable: true),
                    Capacity = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingTables", x => x.Id);
                    table.CheckConstraint("CK_MeetingTables_Capacity", "[Capacity] >= 2 AND [Capacity] <= 100");
                    table.ForeignKey(
                        name: "FK_MeetingTables_Halls_HallId",
                        column: x => x.HallId,
                        principalTable: "Halls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BadgeBatchItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BadgeBatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProfileTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Count = table.Column<int>(type: "int", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BadgeBatchItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BadgeBatchItems_BadgeBatches_BadgeBatchId",
                        column: x => x.BadgeBatchId,
                        principalTable: "BadgeBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BadgeBatchItems_ProfileTypes_ProfileTypeId",
                        column: x => x.ProfileTypeId,
                        principalTable: "ProfileTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RatingQuestionGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RatingTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    NameArabic = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RatingQuestionGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RatingQuestionGroups_RatingTypes_RatingTypeId",
                        column: x => x.RatingTypeId,
                        principalTable: "RatingTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RatingResponses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RatingTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OverallStars = table.Column<int>(type: "int", nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RatingResponses", x => x.Id);
                    table.CheckConstraint("CK_RatingResponses_OverallStars", "[OverallStars] IS NULL OR [OverallStars] BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "FK_RatingResponses_RatingTypes_RatingTypeId",
                        column: x => x.RatingTypeId,
                        principalTable: "RatingTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

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
                    CoverImageFileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LocationEn = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    LocationAr = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    DateLabelEn = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    DateLabelAr = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchiveEditions", x => x.Id);
                    table.CheckConstraint("CK_ArchiveEditions_CountersNonNegative", "[Attendees] >= 0 AND [Sessions] >= 0 AND [Speakers] >= 0");
                    table.CheckConstraint("CK_ArchiveEditions_YearRange", "[Year] >= 2000 AND [Year] <= 2100");
                    table.ForeignKey(
                        name: "FK_ArchiveEditions_StoredFiles_CoverImageFileId",
                        column: x => x.CoverImageFileId,
                        principalTable: "StoredFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Banners",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    TitleArabic = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    BodyArabic = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ImageFileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LinkUrl = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    Start = table.Column<DateTime>(type: "datetime2", nullable: false),
                    End = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Banners", x => x.Id);
                    table.CheckConstraint("CK_Banners_TimeWindow", "[End] > [Start]");
                    table.ForeignKey(
                        name: "FK_Banners_StoredFiles_ImageFileId",
                        column: x => x.ImageFileId,
                        principalTable: "StoredFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MediaItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TitleArabic = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ImageFileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VideoFileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ThumbnailFileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Album = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AlbumArabic = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MediaItems_StoredFiles_ImageFileId",
                        column: x => x.ImageFileId,
                        principalTable: "StoredFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MediaItems_StoredFiles_ThumbnailFileId",
                        column: x => x.ThumbnailFileId,
                        principalTable: "StoredFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MediaItems_StoredFiles_VideoFileId",
                        column: x => x.VideoFileId,
                        principalTable: "StoredFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "News",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TitleArabic = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Excerpt = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ExcerptArabic = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Body = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    BodyArabic = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CategoryArabic = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ImageFileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_News", x => x.Id);
                    table.ForeignKey(
                        name: "FK_News_StoredFiles_ImageFileId",
                        column: x => x.ImageFileId,
                        principalTable: "StoredFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationProfile",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    NameArabic = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    TitleArabic = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Slogan = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    SloganArabic = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Bio = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    BioArabic = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Version = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    VersionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SysVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ReleaseDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EventStartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EventEndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CurrentYear = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    LocationText = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    LocationTextArabic = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Latitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                    Longitude = table.Column<decimal>(type: "decimal(10,6)", precision: 10, scale: 6, nullable: true),
                    ContactPhone = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ContactEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ContactWebsite = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    LiveStreamFileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BackgroundVideoFileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FacebookUrl = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    XUrl = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    InstagramUrl = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    LinkedInUrl = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    YouTubeUrl = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    TikTokUrl = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    SnapchatUrl = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    RegistrationSuccessMessage = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    RegistrationSuccessMessageArabic = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    PartnerDirectoryEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationProfile", x => x.Id);
                    table.CheckConstraint("CK_OrganizationProfile_Coordinates", "([Latitude] IS NULL OR ([Latitude] >= -90 AND [Latitude] <= 90)) AND ([Longitude] IS NULL OR ([Longitude] >= -180 AND [Longitude] <= 180))");
                    table.CheckConstraint("CK_OrganizationProfile_EventWindow", "[EventStartDate] IS NULL OR [EventEndDate] IS NULL OR [EventEndDate] >= [EventStartDate]");
                    table.ForeignKey(
                        name: "FK_OrganizationProfile_StoredFiles_BackgroundVideoFileId",
                        column: x => x.BackgroundVideoFileId,
                        principalTable: "StoredFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrganizationProfile_StoredFiles_LiveStreamFileId",
                        column: x => x.LiveStreamFileId,
                        principalTable: "StoredFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    TitleArabic = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    DescriptionArabic = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    Language = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    LanguageArabic = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    HallId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Type = table.Column<int>(type: "int", nullable: true),
                    Start = table.Column<DateTime>(type: "datetime2", nullable: false),
                    End = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CapacityOverride = table.Column<int>(type: "int", nullable: true),
                    SeatSelectionModeOverride = table.Column<int>(type: "int", nullable: true),
                    ArrivalGraceMinutesOverride = table.Column<int>(type: "int", nullable: true),
                    ReminderSentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RatingPromptSentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RecordingFileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RecordingFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
                    RecordingContentType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    RecordingSizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    RecordingUploadedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RecordingUploadedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LiveStreamFileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LiveSignLanguageFileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LiveCaptions = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    LiveCaptionsArabic = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    LiveNotice = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    LiveNoticeArabic = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sessions", x => x.Id);
                    table.CheckConstraint("CK_Sessions_ArrivalGrace", "[ArrivalGraceMinutesOverride] IS NULL OR ([ArrivalGraceMinutesOverride] >= 0 AND [ArrivalGraceMinutesOverride] <= 240)");
                    table.CheckConstraint("CK_Sessions_CapacityOverride", "[CapacityOverride] IS NULL OR [CapacityOverride] >= 0");
                    table.CheckConstraint("CK_Sessions_PublishedAtPin", "([Status] = 3 AND [PublishedAt] IS NOT NULL) OR ([Status] <> 3 AND [PublishedAt] IS NULL)");
                    table.CheckConstraint("CK_Sessions_TimeWindow", "[End] > [Start]");
                    table.ForeignKey(
                        name: "FK_Sessions_Halls_HallId",
                        column: x => x.HallId,
                        principalTable: "Halls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Sessions_SessionCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "SessionCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Sessions_StoredFiles_LiveSignLanguageFileId",
                        column: x => x.LiveSignLanguageFileId,
                        principalTable: "StoredFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Sessions_StoredFiles_LiveStreamFileId",
                        column: x => x.LiveStreamFileId,
                        principalTable: "StoredFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Sessions_StoredFiles_RecordingFileId",
                        column: x => x.RecordingFileId,
                        principalTable: "StoredFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    NameArabic = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Gender = table.Column<int>(type: "int", nullable: false),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: true),
                    PlaceOfBirth = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    NationalityId = table.Column<int>(type: "int", nullable: false),
                    IsSaudi = table.Column<bool>(type: "bit", nullable: false),
                    NationalId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    IqamaNumber = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    PassportNumber = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NationalIdHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    IqamaNumberHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    PassportNumberHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    JobTitle = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    JobTitleArabic = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    OrganisationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RegionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BadgeBatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValue: new Guid("0f1e2d3c-4b5a-6978-8796-a5b4c3d2e1f0")),
                    EditionYear = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    ShowInMeetLikeYou = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    MobileNumber = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    SaudiMobile = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    InternationalMobile = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    PlateNumber = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: true),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    MawjId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Honorific = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    HonorificArabic = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    PreferredLanguage = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    VipPhotoFileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDelegate = table.Column<bool>(type: "bit", nullable: false),
                    AllowsDelegationMeeting = table.Column<bool>(type: "bit", nullable: false),
                    AllowsSpeakerMeeting = table.Column<bool>(type: "bit", nullable: false),
                    ProfileTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AdmissionState = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, defaultValue: "PendingApproval"),
                    StateChangedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StateChangedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    QrId = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RejectionReasonArabic = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IdImageFileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AccessibilityTextSize = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false, defaultValue: "normal"),
                    AccessibilityHighContrast = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    AccessibilityReduceMotion = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    AccessibilityScreenReaderAssist = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    AccessibilityCaptions = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    AccessibilityConfiguredAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserProfiles_BadgeBatches_BadgeBatchId",
                        column: x => x.BadgeBatchId,
                        principalTable: "BadgeBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserProfiles_Organisations_OrganisationId",
                        column: x => x.OrganisationId,
                        principalTable: "Organisations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserProfiles_ProfileTypes_ProfileTypeId",
                        column: x => x.ProfileTypeId,
                        principalTable: "ProfileTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserProfiles_Regions_RegionId",
                        column: x => x.RegionId,
                        principalTable: "Regions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserProfiles_StoredFiles_IdImageFileId",
                        column: x => x.IdImageFileId,
                        principalTable: "StoredFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserProfiles_StoredFiles_VipPhotoFileId",
                        column: x => x.VipPhotoFileId,
                        principalTable: "StoredFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GateAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RevokedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GateAssignments", x => x.Id);
                    table.CheckConstraint("CK_GateAssignments_RevocationPin", "([IsActive] = 1 AND [RevokedAt] IS NULL AND [RevokedByUserId] IS NULL) OR ([IsActive] = 0 AND [RevokedAt] IS NOT NULL AND [RevokedByUserId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_GateAssignments_Gates_GateId",
                        column: x => x.GateId,
                        principalTable: "Gates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
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
                    table.ForeignKey(
                        name: "FK_GateProfileTypeAllow_ProfileTypes_ProfileTypeId",
                        column: x => x.ProfileTypeId,
                        principalTable: "ProfileTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BusinessMeetings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MeetingTableId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MeetingType = table.Column<int>(type: "int", nullable: false),
                    Start = table.Column<DateTime>(type: "datetime2", nullable: false),
                    End = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    ScheduledByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CancelledByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessMeetings", x => x.Id);
                    table.CheckConstraint("CK_BusinessMeetings_TimeWindow", "[End] > [Start]");
                    table.ForeignKey(
                        name: "FK_BusinessMeetings_MeetingTables_MeetingTableId",
                        column: x => x.MeetingTableId,
                        principalTable: "MeetingTables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RatingQuestions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RatingTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RatingQuestionGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Text = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    TextArabic = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RatingQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RatingQuestions_RatingQuestionGroups_RatingQuestionGroupId",
                        column: x => x.RatingQuestionGroupId,
                        principalTable: "RatingQuestionGroups",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RatingQuestions_RatingTypes_RatingTypeId",
                        column: x => x.RatingTypeId,
                        principalTable: "RatingTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ArchiveMediaItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ArchiveEditionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    MediaFileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CaptionEn = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CaptionAr = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchiveMediaItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArchiveMediaItems_ArchiveEditions_ArchiveEditionId",
                        column: x => x.ArchiveEditionId,
                        principalTable: "ArchiveEditions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ArchiveMediaItems_StoredFiles_MediaFileId",
                        column: x => x.MediaFileId,
                        principalTable: "StoredFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ArchiveSessionTitles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ArchiveEditionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TitleEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TitleAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchiveSessionTitles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArchiveSessionTitles_ArchiveEditions_ArchiveEditionId",
                        column: x => x.ArchiveEditionId,
                        principalTable: "ArchiveEditions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationAboutItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    TitleArabic = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Text = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    TextArabic = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationAboutItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrganizationAboutItems_OrganizationProfile_OrganizationProfileId",
                        column: x => x.OrganizationProfileId,
                        principalTable: "OrganizationProfile",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationDetails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    NameArabic = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    ValueArabic = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrganizationDetails_OrganizationProfile_OrganizationProfileId",
                        column: x => x.OrganizationProfileId,
                        principalTable: "OrganizationProfile",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SessionFavourites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionFavourites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SessionFavourites_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "Sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SessionModerators",
                columns: table => new
                {
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionModerators", x => new { x.SessionId, x.UserId });
                    table.ForeignKey(
                        name: "FK_SessionModerators_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "Sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SessionOutcomes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    TextArabic = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionOutcomes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SessionOutcomes_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "Sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SessionQuestions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmittedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuestionText = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Recipient = table.Column<int>(type: "int", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    IsHidden = table.Column<bool>(type: "bit", nullable: false),
                    IsPushed = table.Column<bool>(type: "bit", nullable: false),
                    PushedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Phase = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StatusBeforeHidden = table.Column<int>(type: "int", nullable: true),
                    AiFilterVerdict = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    AssignedToRole = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    EscalatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EscalatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionQuestions", x => x.Id);
                    table.CheckConstraint("CK_SessionQuestions_EscalationTrio", "([AssignedToRole] IS NULL AND [EscalatedByUserId] IS NULL AND [EscalatedAt] IS NULL) OR ([AssignedToRole] IS NOT NULL AND [EscalatedByUserId] IS NOT NULL AND [EscalatedAt] IS NOT NULL)");
                    table.CheckConstraint("CK_SessionQuestions_PushedPair", "([IsPushed] = 0 AND [PushedAt] IS NULL) OR ([IsPushed] = 1 AND [PushedAt] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_SessionQuestions_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "Sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SessionSummaries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    KeyPoints = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    KeyPointsArabic = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Recommendations = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    RecommendationsArabic = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Speakers = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    SpeakersArabic = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    FullText = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    FullTextArabic = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    AiModel = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    SummaryVideoFileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AiDraftFullTextArabic = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true),
                    AiDraftGeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PublishedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewSubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewSubmittedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionSummaries", x => x.Id);
                    table.CheckConstraint("CK_SessionSummaries_ReviewOrder", "[ApprovedAt] IS NULL OR ([ReviewSubmittedAt] IS NOT NULL AND [ApprovedAt] >= [ReviewSubmittedAt])");
                    table.ForeignKey(
                        name: "FK_SessionSummaries_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "Sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SessionSummaries_StoredFiles_SummaryVideoFileId",
                        column: x => x.SummaryVideoFileId,
                        principalTable: "StoredFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SessionThemes",
                columns: table => new
                {
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ThemeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionThemes", x => new { x.SessionId, x.ThemeId });
                    table.ForeignKey(
                        name: "FK_SessionThemes_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "Sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SessionThemes_Themes_ThemeId",
                        column: x => x.ThemeId,
                        principalTable: "Themes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Countries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    NameArabic = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    PhonePrefix = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsInvited = table.Column<bool>(type: "bit", nullable: false),
                    DelegationArrivalDate = table.Column<DateOnly>(type: "date", nullable: true),
                    DelegationDepartureDate = table.Column<DateOnly>(type: "date", nullable: true),
                    HeadOfDelegationUserProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Countries", x => x.Id);
                    table.CheckConstraint("CK_Countries_DelegationWindow", "[DelegationArrivalDate] IS NULL OR [DelegationDepartureDate] IS NULL OR [DelegationDepartureDate] >= [DelegationArrivalDate]");
                    table.ForeignKey(
                        name: "FK_Countries_UserProfiles_HeadOfDelegationUserProfileId",
                        column: x => x.HeadOfDelegationUserProfileId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "GateScans",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    QrIdAtScan = table.Column<string>(type: "nvarchar(96)", maxLength: 96, nullable: false),
                    ScannedDisplayName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ScannedProfileTypeName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Direction = table.Column<int>(type: "int", nullable: false),
                    Outcome = table.Column<int>(type: "int", nullable: false),
                    DenialReasonCode = table.Column<int>(type: "int", nullable: true),
                    Source = table.Column<int>(type: "int", nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ScannedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScannedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClientScannedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GateScans", x => x.Id);
                    table.CheckConstraint("CK_GateScans_DenialPin", "([Outcome] = 1 AND [DenialReasonCode] IS NOT NULL) OR ([Outcome] = 0 AND [DenialReasonCode] IS NULL)");
                    table.CheckConstraint("CK_GateScans_DenialReasonRange", "[DenialReasonCode] IS NULL OR [DenialReasonCode] BETWEEN 0 AND 8");
                    table.CheckConstraint("CK_GateScans_DirectionRange", "[Direction] BETWEEN 0 AND 1");
                    table.CheckConstraint("CK_GateScans_SourceRange", "[Source] BETWEEN 0 AND 2");
                    table.ForeignKey(
                        name: "FK_GateScans_Gates_GateId",
                        column: x => x.GateId,
                        principalTable: "Gates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GateScans_UserProfiles_UserProfileId",
                        column: x => x.UserProfileId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "HallAttendances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HallId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Method = table.Column<int>(type: "int", nullable: false),
                    Enter = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Leave = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HallAttendances", x => x.Id);
                    table.CheckConstraint("CK_HallAttendances_LeaveOrder", "[Leave] IS NULL OR [Leave] >= [Enter]");
                    table.ForeignKey(
                        name: "FK_HallAttendances_Halls_HallId",
                        column: x => x.HallId,
                        principalTable: "Halls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HallAttendances_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "Sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HallAttendances_UserProfiles_UserProfileId",
                        column: x => x.UserProfileId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Invitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SentByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SentToUserProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    State = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RespondedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invitations", x => x.Id);
                    table.CheckConstraint("CK_Invitations_ResponsePin", "([State] = 0 AND [RespondedAt] IS NULL) OR ([State] <> 0 AND [RespondedAt] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_Invitations_UserProfiles_SentToUserProfileId",
                        column: x => x.SentToUserProfileId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProfileIdentityDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Number = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    NumberHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfileIdentityDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProfileIdentityDocuments_UserProfiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SeatReservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowLabel = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: true),
                    SeatNumber = table.Column<int>(type: "int", nullable: true),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    ReservedForProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReleasedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ReleasedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NoShowReleaseAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    GuestHint = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    GuestHintArabic = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeatReservations", x => x.Id);
                    table.CheckConstraint("CK_SeatReservations_ReviewPair", "([ReviewedByUserId] IS NULL AND [ReviewedAt] IS NULL) OR ([ReviewedByUserId] IS NOT NULL AND [ReviewedAt] IS NOT NULL)");
                    table.CheckConstraint("CK_SeatReservations_SeatNumber", "[SeatNumber] >= 1");
                    table.CheckConstraint("CK_SeatReservations_SeatPair", "([RowLabel] IS NULL AND [SeatNumber] IS NULL) OR ([RowLabel] IS NOT NULL AND [SeatNumber] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_SeatReservations_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "Sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SeatReservations_UserProfiles_ReservedForProfileId",
                        column: x => x.ReservedForProfileId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserProfileInterests",
                columns: table => new
                {
                    UserProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InterestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserProfileInterests", x => new { x.UserProfileId, x.InterestId });
                    table.ForeignKey(
                        name: "FK_UserProfileInterests_Interests_InterestId",
                        column: x => x.InterestId,
                        principalTable: "Interests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserProfileInterests_UserProfiles_UserProfileId",
                        column: x => x.UserProfileId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RatingAnswers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RatingResponseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RatingQuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Stars = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RatingAnswers", x => x.Id);
                    table.CheckConstraint("CK_RatingAnswers_Stars", "[Stars] BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "FK_RatingAnswers_RatingQuestions_RatingQuestionId",
                        column: x => x.RatingQuestionId,
                        principalTable: "RatingQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RatingAnswers_RatingResponses_RatingResponseId",
                        column: x => x.RatingResponseId,
                        principalTable: "RatingResponses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ArchivePastSpeakers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ArchiveEditionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    PhotoFileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CountryId = table.Column<int>(type: "int", nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchivePastSpeakers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArchivePastSpeakers_ArchiveEditions_ArchiveEditionId",
                        column: x => x.ArchiveEditionId,
                        principalTable: "ArchiveEditions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ArchivePastSpeakers_Countries_CountryId",
                        column: x => x.CountryId,
                        principalTable: "Countries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ArchivePastSpeakers_StoredFiles_PhotoFileId",
                        column: x => x.PhotoFileId,
                        principalTable: "StoredFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DelegationAvailabilityWindows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CountryId = table.Column<int>(type: "int", nullable: false),
                    Start = table.Column<DateTime>(type: "datetime2", nullable: false),
                    End = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SlotMinutes = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DelegationAvailabilityWindows", x => x.Id);
                    table.CheckConstraint("CK_DelegationAvailabilityWindows_SlotMinutes", "[SlotMinutes] >= 5 AND [SlotMinutes] <= 480");
                    table.CheckConstraint("CK_DelegationAvailabilityWindows_TimeWindow", "[End] > [Start]");
                    table.ForeignKey(
                        name: "FK_DelegationAvailabilityWindows_Countries_CountryId",
                        column: x => x.CountryId,
                        principalTable: "Countries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

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
                    Tier = table.Column<int>(type: "int", nullable: true),
                    PhoneSecondary = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    FacebookUrl = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    XUrl = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    LinkedInUrl = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    InstagramUrl = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    City = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CityArabic = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Latitude = table.Column<double>(type: "float", nullable: true),
                    Longitude = table.Column<double>(type: "float", nullable: true),
                    CountryId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Exhibitors", x => x.Id);
                    table.CheckConstraint("CK_Exhibitors_Coordinates", "([Latitude] IS NULL AND [Longitude] IS NULL) OR ([Latitude] IS NOT NULL AND [Longitude] IS NOT NULL AND [Latitude] >= -90 AND [Latitude] <= 90 AND [Longitude] >= -180 AND [Longitude] <= 180)");
                    table.ForeignKey(
                        name: "FK_Exhibitors_Countries_CountryId",
                        column: x => x.CountryId,
                        principalTable: "Countries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MediaPartners",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    NameArabic = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    LogoFileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Url = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    PhonePrimary = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    PhoneSecondary = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    FacebookUrl = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    XUrl = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    LinkedInUrl = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    InstagramUrl = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    City = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CityArabic = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Latitude = table.Column<double>(type: "float", nullable: true),
                    Longitude = table.Column<double>(type: "float", nullable: true),
                    CountryId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaPartners", x => x.Id);
                    table.CheckConstraint("CK_MediaPartners_Coordinates", "([Latitude] IS NULL AND [Longitude] IS NULL) OR ([Latitude] IS NOT NULL AND [Longitude] IS NOT NULL AND [Latitude] >= -90 AND [Latitude] <= 90 AND [Longitude] >= -180 AND [Longitude] <= 180)");
                    table.ForeignKey(
                        name: "FK_MediaPartners_Countries_CountryId",
                        column: x => x.CountryId,
                        principalTable: "Countries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MediaPartners_StoredFiles_LogoFileId",
                        column: x => x.LogoFileId,
                        principalTable: "StoredFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Speakers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    NameArabic = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Rank = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    RankArabic = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
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
                    InstagramUrl = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    WebsiteUrl = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    PhonePrimary = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    PhoneSecondary = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    City = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CityArabic = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Latitude = table.Column<double>(type: "float", nullable: true),
                    Longitude = table.Column<double>(type: "float", nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Speakers", x => x.Id);
                    table.CheckConstraint("CK_Speakers_DisplayOrder", "[DisplayOrder] >= 0");
                    table.CheckConstraint("CK_Speakers_Location", "([Latitude] IS NULL AND [Longitude] IS NULL) OR ([Latitude] IS NOT NULL AND [Longitude] IS NOT NULL AND [Latitude] >= -90 AND [Latitude] <= 90 AND [Longitude] >= -180 AND [Longitude] <= 180)");
                    table.ForeignKey(
                        name: "FK_Speakers_Countries_CountryId",
                        column: x => x.CountryId,
                        principalTable: "Countries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Speakers_UserProfiles_UserProfileId",
                        column: x => x.UserProfileId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Sponsors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    NameArabic = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Tier = table.Column<int>(type: "int", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    LogoFileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Url = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Tagline = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    TaglineArabic = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    About = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    AboutArabic = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    PhonePrimary = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    PhoneSecondary = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    FacebookUrl = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    XUrl = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    LinkedInUrl = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    InstagramUrl = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    City = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CityArabic = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Latitude = table.Column<double>(type: "float", nullable: true),
                    Longitude = table.Column<double>(type: "float", nullable: true),
                    CountryId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sponsors", x => x.Id);
                    table.CheckConstraint("CK_Sponsors_Coordinates", "([Latitude] IS NULL AND [Longitude] IS NULL) OR ([Latitude] IS NOT NULL AND [Longitude] IS NOT NULL AND [Latitude] >= -90 AND [Latitude] <= 90 AND [Longitude] >= -180 AND [Longitude] <= 180)");
                    table.ForeignKey(
                        name: "FK_Sponsors_Countries_CountryId",
                        column: x => x.CountryId,
                        principalTable: "Countries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Sponsors_StoredFiles_LogoFileId",
                        column: x => x.LogoFileId,
                        principalTable: "StoredFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DelegationMeetingRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestingCountryId = table.Column<int>(type: "int", nullable: false),
                    TargetCountryId = table.Column<int>(type: "int", nullable: false),
                    AttendeeCount = table.Column<int>(type: "int", nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    SlotStart = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SlotEnd = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AvailabilityWindowId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    HallId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MeetingTableId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ResponseNote = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConfirmedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReminderSentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CheckedInAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CheckedInByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RespondedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RespondedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DelegationMeetingRequests", x => x.Id);
                    table.CheckConstraint("CK_DelegationMeetingRequests_AttendeeCount", "[AttendeeCount] >= 1 AND [AttendeeCount] <= 100");
                    table.CheckConstraint("CK_DelegationMeetingRequests_Slot", "[SlotStart] IS NULL OR [SlotEnd] IS NULL OR [SlotEnd] > [SlotStart]");
                    table.ForeignKey(
                        name: "FK_DelegationMeetingRequests_Countries_RequestingCountryId",
                        column: x => x.RequestingCountryId,
                        principalTable: "Countries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DelegationMeetingRequests_Countries_TargetCountryId",
                        column: x => x.TargetCountryId,
                        principalTable: "Countries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DelegationMeetingRequests_DelegationAvailabilityWindows_AvailabilityWindowId",
                        column: x => x.AvailabilityWindowId,
                        principalTable: "DelegationAvailabilityWindows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DelegationMeetingRequests_Halls_HallId",
                        column: x => x.HallId,
                        principalTable: "Halls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DelegationMeetingRequests_MeetingTables_MeetingTableId",
                        column: x => x.MeetingTableId,
                        principalTable: "MeetingTables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Booths",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    NameArabic = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ExhibitorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OfficerName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    OfficerPhone = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    OfficerEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    OfficerNameArabic = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    OfficerPhoneSecondary = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    OfficerWebsite = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    OfficerFacebookUrl = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    OfficerXUrl = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    OfficerLinkedInUrl = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    OfficerInstagramUrl = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    OfficerCity = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    OfficerCityArabic = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    OfficerLatitude = table.Column<double>(type: "float", nullable: true),
                    OfficerLongitude = table.Column<double>(type: "float", nullable: true),
                    OfficerCountryId = table.Column<int>(type: "int", nullable: true),
                    ExhibitorName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ExhibitorNameArabic = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Sector = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    SectorArabic = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    DescriptionArabic = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    HallId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MapX = table.Column<double>(type: "float", nullable: true),
                    MapY = table.Column<double>(type: "float", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Booths", x => x.Id);
                    table.CheckConstraint("CK_Booths_OfficerCoordinates", "([OfficerLatitude] IS NULL AND [OfficerLongitude] IS NULL) OR ([OfficerLatitude] IS NOT NULL AND [OfficerLongitude] IS NOT NULL AND [OfficerLatitude] >= -90 AND [OfficerLatitude] <= 90 AND [OfficerLongitude] >= -180 AND [OfficerLongitude] <= 180)");
                    table.ForeignKey(
                        name: "FK_Booths_Countries_OfficerCountryId",
                        column: x => x.OfficerCountryId,
                        principalTable: "Countries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Booths_Exhibitors_ExhibitorId",
                        column: x => x.ExhibitorId,
                        principalTable: "Exhibitors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Booths_Halls_HallId",
                        column: x => x.HallId,
                        principalTable: "Halls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BusinessMeetingParticipants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusinessMeetingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    ExhibitorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VisitorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DisplayNameSnapshot = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessMeetingParticipants", x => x.Id);
                    table.CheckConstraint("CK_BusinessMeetingParticipants_PartyXor", "([Kind] = 0 AND [ExhibitorId] IS NOT NULL AND [VisitorUserId] IS NULL) OR ([Kind] = 1 AND [VisitorUserId] IS NOT NULL AND [ExhibitorId] IS NULL)");
                    table.ForeignKey(
                        name: "FK_BusinessMeetingParticipants_BusinessMeetings_BusinessMeetingId",
                        column: x => x.BusinessMeetingId,
                        principalTable: "BusinessMeetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BusinessMeetingParticipants_Exhibitors_ExhibitorId",
                        column: x => x.ExhibitorId,
                        principalTable: "Exhibitors",
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
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExhibitorMemberships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExhibitorMemberships_Exhibitors_ExhibitorId",
                        column: x => x.ExhibitorId,
                        principalTable: "Exhibitors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExhibitorVisitorScans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExhibitorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExhibitorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VisitorProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExhibitorVisitorScans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExhibitorVisitorScans_Exhibitors_ExhibitorId",
                        column: x => x.ExhibitorId,
                        principalTable: "Exhibitors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExhibitorVisitorScans_UserProfiles_VisitorProfileId",
                        column: x => x.VisitorProfileId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SessionSpeakers",
                columns: table => new
                {
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SpeakerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    Role = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionSpeakers", x => new { x.SessionId, x.SpeakerId });
                    table.ForeignKey(
                        name: "FK_SessionSpeakers_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "Sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SessionSpeakers_Speakers_SpeakerId",
                        column: x => x.SpeakerId,
                        principalTable: "Speakers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SpeakerAvailabilityWindows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SpeakerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Start = table.Column<DateTime>(type: "datetime2", nullable: false),
                    End = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SlotMinutes = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpeakerAvailabilityWindows", x => x.Id);
                    table.CheckConstraint("CK_SpeakerAvailabilityWindows_SlotMinutes", "[SlotMinutes] >= 5 AND [SlotMinutes] <= 480");
                    table.CheckConstraint("CK_SpeakerAvailabilityWindows_TimeWindow", "[End] > [Start]");
                    table.ForeignKey(
                        name: "FK_SpeakerAvailabilityWindows_Speakers_SpeakerId",
                        column: x => x.SpeakerId,
                        principalTable: "Speakers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SpeakerPresentations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SpeakerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    StoredFileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpeakerPresentations", x => x.Id);
                    table.CheckConstraint("CK_SpeakerPresentations_SizeBytes", "[SizeBytes] > 0");
                    table.ForeignKey(
                        name: "FK_SpeakerPresentations_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "Sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SpeakerPresentations_Speakers_SpeakerId",
                        column: x => x.SpeakerId,
                        principalTable: "Speakers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SpeakerPresentations_StoredFiles_StoredFileId",
                        column: x => x.StoredFileId,
                        principalTable: "StoredFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DelegationMeetingActionTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DelegationMeetingRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenHash = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DelegationMeetingActionTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DelegationMeetingActionTokens_DelegationMeetingRequests_DelegationMeetingRequestId",
                        column: x => x.DelegationMeetingRequestId,
                        principalTable: "DelegationMeetingRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VenueMapNodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    LabelArabic = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    X = table.Column<double>(type: "float", nullable: false),
                    Y = table.Column<double>(type: "float", nullable: false),
                    HallId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BoothId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VenueMapNodes", x => x.Id);
                    table.CheckConstraint("CK_VenueMapNodes_KindArc", "([HallId] IS NULL OR [Kind] = 0) AND ([BoothId] IS NULL OR [Kind] = 2) AND NOT ([HallId] IS NOT NULL AND [BoothId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_VenueMapNodes_Booths_BoothId",
                        column: x => x.BoothId,
                        principalTable: "Booths",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VenueMapNodes_Halls_HallId",
                        column: x => x.HallId,
                        principalTable: "Halls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SpeakerMeetingRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SpeakerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequesterName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    SlotStart = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SlotEnd = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AvailabilityWindowId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    HallId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MeetingTableId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SpeakerDecisionAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResponseNote = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ReminderSentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CheckedInAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CheckedInByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RespondedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RespondedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpeakerMeetingRequests", x => x.Id);
                    table.CheckConstraint("CK_SpeakerMeetingRequests_Slot", "[SlotStart] IS NULL OR [SlotEnd] IS NULL OR [SlotEnd] > [SlotStart]");
                    table.ForeignKey(
                        name: "FK_SpeakerMeetingRequests_Halls_HallId",
                        column: x => x.HallId,
                        principalTable: "Halls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SpeakerMeetingRequests_MeetingTables_MeetingTableId",
                        column: x => x.MeetingTableId,
                        principalTable: "MeetingTables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SpeakerMeetingRequests_SpeakerAvailabilityWindows_AvailabilityWindowId",
                        column: x => x.AvailabilityWindowId,
                        principalTable: "SpeakerAvailabilityWindows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SpeakerMeetingRequests_Speakers_SpeakerId",
                        column: x => x.SpeakerId,
                        principalTable: "Speakers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MeetingActionTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SpeakerMeetingRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<int>(type: "int", nullable: false),
                    TokenHash = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
                    Expires = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingActionTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeetingActionTokens_SpeakerMeetingRequests_SpeakerMeetingRequestId",
                        column: x => x.SpeakerMeetingRequestId,
                        principalTable: "SpeakerMeetingRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "ArchiveVisibility",
                columns: new[] { "Id", "IsVisible", "LastChangedAt", "LastChangedByUserId" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null });

            migrationBuilder.InsertData(
                table: "BadgeBatches",
                columns: new[] { "Id", "CreatedAt", "CreatedBy", "DeletedAt", "IsActive", "IsDelegate", "Name", "NameArabic", "RecipientEmail", "UpdatedAt", "UpdatedBy" },
                values: new object[] { new Guid("0f1e2d3c-4b5a-6978-8796-a5b4c3d2e1f0"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new Guid("00000000-0000-0000-0000-000000000000"), null, true, false, "Direct registration", "تسجيل مباشر", null, null, null });

            migrationBuilder.InsertData(
                table: "Countries",
                columns: new[] { "Id", "Code", "CreatedAt", "DelegationArrivalDate", "DelegationDepartureDate", "DisplayOrder", "HeadOfDelegationUserProfileId", "IsActive", "IsInvited", "Name", "NameArabic", "PhonePrefix", "UpdatedAt" },
                values: new object[,]
                {
                    { 32, "AR", new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, 630, null, true, false, "Argentina", "الأرجنتين", "+54", null },
                    { 36, "AU", new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, 520, null, true, false, "Australia", "أستراليا", "+61", null },
                    { 40, "AT", new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, 300, null, true, false, "Austria", "النمسا", "+43", null },
                    { 48, "BH", new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, 10, null, true, false, "Bahrain", "البحرين", "+973", null },
                    { 50, "BD", new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, 450, null, true, false, "Bangladesh", "بنغلاديش", "+880", null },
                    { 56, "BE", new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, 280, null, true, false, "Belgium", "بلجيكا", "+32", null },
                    { 76, "BR", new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, 610, null, true, false, "Brazil", "البرازيل", "+55", null },
                    { 124, "CA", new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, 600, null, true, false, "Canada", "كندا", "+1", null },
                    { 156, "CN", new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, 420, null, true, false, "China", "الصين", "+86", null },
                    { 208, "DK", new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, 330, null, true, false, "Denmark", "الدنمارك", "+45", null },
                    { 231, "ET", new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, 730, null, true, false, "Ethiopia", "إثيوبيا", "+251", null },
                    { 246, "FI", new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, 340, null, true, false, "Finland", "فنلندا", "+358", null },
                    { 250, "FR", new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, 220, null, true, false, "France", "فرنسا", "+33", null },
                    { 262, "DJ", new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, 170, null, true, false, "Djibouti", "جيبوتي", "+253", null },
                    { 275, "PS", new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, 160, null, true, false, "Palestine", "فلسطين", "+970", null },
                    { 276, "DE", new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, 230, null, true, false, "Germany", "ألمانيا", "+49", null },
                    { 300, "GR", new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, 360, null, true, false, "Greece", "اليونان", "+30", null },
                    { 356, "IN", new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, 430, null, true, false, "India", "الهند", "+91", null },
                    { 360, "ID", new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, 460, null, true, false, "Indonesia", "إندونيسيا", "+62", null },
                    { 364, "IR", new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, 140, null, true, false, "Iran", "إيران", "+98", null },
                    { 368, "IQ", new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, 150, null, true, false, "Iraq", "العراق", "+964", null },
                    { 372, "IE", new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, 350, null, true, false, "Ireland", "أيرلندا", "+353", null },
                    { 380, "IT", new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, 240, null, true, false, "Italy", "إيطاليا", "+39", null },
                    { 392, "JP", new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, 400, null, true, false, "Japan", "اليابان", "+81", null },
                    { 400, "JO", new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, 80, null, true, false, "Jordan", "الأردن", "+962", null },
                    { 404, "KE", new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, 720, null, true, false, "Kenya", "كينيا", "+254", null },
                    { 410, "KR", new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, 410, null, true, false, "South Korea", "كوريا الجنوبية", "+82", null },
                    { 414, "KW", new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, 20, null, true, false, "Kuwait", "الكويت", "+965", null },
                    { 422, "LB", new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, 90, null, true, false, "Lebanon", "لبنان", "+961", null },
                    { 458, "MY", new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, 470, null, true, false, "Malaysia", "ماليزيا", "+60", null },
                    { 484, "MX", new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, 620, null, true, false, "Mexico", "المكسيك", "+52", null },
                    { 504, "MA", new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, 100, null, true, false, "Morocco", "المغرب", "+212", null },
                    { 512, "OM", new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, 30, null, true, false, "Oman", "عُمان", "+968", null },
                    { 528, "NL", new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, 270, null, true, false, "Netherlands", "هولندا", "+31", null },
                    { 554, "NZ", new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, 530, null, true, false, "New Zealand", "نيوزيلندا", "+64", null },
                    { 566, "NG", new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, 710, null, true, false, "Nigeria", "نيجيريا", "+234", null },
                    { 578, "NO", new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, 320, null, true, false, "Norway", "النرويج", "+47", null },
                    { 586, "PK", new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, 440, null, true, false, "Pakistan", "باكستان", "+92", null },
                    { 608, "PH", new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, 510, null, true, false, "Philippines", "الفلبين", "+63", null },
                    { 620, "PT", new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, 260, null, true, false, "Portugal", "البرتغال", "+351", null },
                    { 634, "QA", new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, 40, null, true, false, "Qatar", "قطر", "+974", null },
                    { 643, "RU", new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, 370, null, true, false, "Russia", "روسيا", "+7", null },
                    { 682, "SA", new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, 50, null, true, false, "Saudi Arabia", "المملكة العربية السعودية", "+966", null },
                    { 702, "SG", new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, 480, null, true, false, "Singapore", "سنغافورة", "+65", null },
                    { 704, "VN", new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, 500, null, true, false, "Viet Nam", "فيتنام", "+84", null },
                    { 710, "ZA", new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, 700, null, true, false, "South Africa", "جنوب أفريقيا", "+27", null },
                    { 724, "ES", new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, 250, null, true, false, "Spain", "إسبانيا", "+34", null },
                    { 729, "SD", new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, 120, null, true, false, "Sudan", "السودان", "+249", null },
                    { 752, "SE", new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, 310, null, true, false, "Sweden", "السويد", "+46", null },
                    { 756, "CH", new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, 290, null, true, false, "Switzerland", "سويسرا", "+41", null },
                    { 764, "TH", new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, 490, null, true, false, "Thailand", "تايلاند", "+66", null },
                    { 784, "AE", new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, 60, null, true, false, "United Arab Emirates", "الإمارات العربية المتحدة", "+971", null },
                    { 792, "TR", new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, 110, null, true, false, "Türkiye", "تركيا", "+90", null },
                    { 818, "EG", new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, 70, null, true, false, "Egypt", "مصر", "+20", null },
                    { 826, "GB", new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, 210, null, true, false, "United Kingdom", "المملكة المتحدة", "+44", null },
                    { 840, "US", new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, 200, null, true, false, "United States", "الولايات المتحدة الأمريكية", "+1", null },
                    { 887, "YE", new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, 130, null, true, false, "Yemen", "اليمن", "+967", null }
                });

            migrationBuilder.InsertData(
                table: "EventEdition",
                columns: new[] { "Id", "LastClosedAt", "LastReissueCount", "OpenedAt", "OpenedByUserId", "Year" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), null, 0, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 2026 });

            migrationBuilder.InsertData(
                table: "OrganizationProfile",
                columns: new[] { "Id", "BackgroundVideoFileId", "Bio", "BioArabic", "ContactEmail", "ContactPhone", "ContactWebsite", "CreatedAt", "CreatedBy", "CurrentYear", "DeletedAt", "EventEndDate", "EventStartDate", "FacebookUrl", "InstagramUrl", "IsActive", "Latitude", "LinkedInUrl", "LiveStreamFileId", "LocationText", "LocationTextArabic", "Longitude", "Name", "NameArabic", "PartnerDirectoryEnabled", "RegistrationSuccessMessage", "RegistrationSuccessMessageArabic", "ReleaseDate", "Slogan", "SloganArabic", "SnapchatUrl", "Status", "SysVersion", "TikTokUrl", "Title", "TitleArabic", "UpdatedAt", "UpdatedBy", "Version", "VersionDate", "XUrl", "YouTubeUrl" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), null, null, null, null, null, null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new Guid("00000000-0000-0000-0000-000000000000"), 2026, null, new DateTime(2026, 4, 30, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, true, null, null, null, "Saudi Arabia", "السعودية", null, "The International Maritime Forum", "الملتقى الدولي البحري", true, "Congratulations, welcome to the Fourth Saudi Forum.", "تهانينا، مرحباً بكم في الملتقى السعودي الرابع.", null, null, null, null, 1, null, null, "The Saudi International Maritime Forum", "الملتقى البحري السعودي الدولي", null, null, "1.0.0", null, null, null });

            migrationBuilder.InsertData(
                table: "RegistrationGate",
                columns: new[] { "Id", "AutoClose", "IsOpen", "LastChangedAt", "LastChangedByUserId" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), null, true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null });

            migrationBuilder.CreateIndex(
                name: "IX_AiChatMessages_UserId_CreatedAt",
                table: "AiChatMessages",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AiInvocations_CallerUserId_CreatedAt",
                table: "AiInvocations",
                columns: new[] { "CallerUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AiInvocations_CreatedAt",
                table: "AiInvocations",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AiInvocations_ErrorCode_CreatedAt",
                table: "AiInvocations",
                columns: new[] { "ErrorCode", "CreatedAt" },
                filter: "[ErrorCode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AiInvocations_Feature_CreatedAt",
                table: "AiInvocations",
                columns: new[] { "Feature", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AiPromptHistory_AiPromptId_Version",
                table: "AiPromptHistory",
                columns: new[] { "AiPromptId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiPromptHistory_CapturedAt",
                table: "AiPromptHistory",
                column: "CapturedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AiPrompts_Feature_IsActive",
                table: "AiPrompts",
                columns: new[] { "Feature", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_AiPrompts_Key",
                table: "AiPrompts",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ArchiveEditions_CoverImageFileId",
                table: "ArchiveEditions",
                column: "CoverImageFileId");

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
                name: "IX_ArchiveMediaItems_ArchiveEditionId_DisplayOrder",
                table: "ArchiveMediaItems",
                columns: new[] { "ArchiveEditionId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ArchiveMediaItems_MediaFileId",
                table: "ArchiveMediaItems",
                column: "MediaFileId");

            migrationBuilder.CreateIndex(
                name: "IX_ArchivePastSpeakers_ArchiveEditionId_DisplayOrder",
                table: "ArchivePastSpeakers",
                columns: new[] { "ArchiveEditionId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ArchivePastSpeakers_CountryId",
                table: "ArchivePastSpeakers",
                column: "CountryId");

            migrationBuilder.CreateIndex(
                name: "IX_ArchivePastSpeakers_PhotoFileId",
                table: "ArchivePastSpeakers",
                column: "PhotoFileId");

            migrationBuilder.CreateIndex(
                name: "IX_ArchiveSessionTitles_ArchiveEditionId_DisplayOrder",
                table: "ArchiveSessionTitles",
                columns: new[] { "ArchiveEditionId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_BadgeBatches_IsActive_CreatedAt",
                table: "BadgeBatches",
                columns: new[] { "IsActive", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BadgeBatchItems_BadgeBatchId_DisplayOrder",
                table: "BadgeBatchItems",
                columns: new[] { "BadgeBatchId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_BadgeBatchItems_ProfileTypeId",
                table: "BadgeBatchItems",
                column: "ProfileTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_BadgeUpdateRequests_RequestedByUserId",
                table: "BadgeUpdateRequests",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BadgeUpdateRequests_Status_CreatedAt",
                table: "BadgeUpdateRequests",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Banners_ImageFileId",
                table: "Banners",
                column: "ImageFileId");

            migrationBuilder.CreateIndex(
                name: "IX_Banners_IsActive_Start_End_DisplayOrder",
                table: "Banners",
                columns: new[] { "IsActive", "Start", "End", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Booths_Code",
                table: "Booths",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Booths_ExhibitorId",
                table: "Booths",
                column: "ExhibitorId");

            migrationBuilder.CreateIndex(
                name: "IX_Booths_HallId",
                table: "Booths",
                column: "HallId");

            migrationBuilder.CreateIndex(
                name: "IX_Booths_IsActive",
                table: "Booths",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Booths_OfficerCountryId",
                table: "Booths",
                column: "OfficerCountryId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessMeetingParticipants_BusinessMeetingId",
                table: "BusinessMeetingParticipants",
                column: "BusinessMeetingId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessMeetingParticipants_BusinessMeetingId_ExhibitorId",
                table: "BusinessMeetingParticipants",
                columns: new[] { "BusinessMeetingId", "ExhibitorId" },
                unique: true,
                filter: "[ExhibitorId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessMeetingParticipants_BusinessMeetingId_VisitorUserId",
                table: "BusinessMeetingParticipants",
                columns: new[] { "BusinessMeetingId", "VisitorUserId" },
                unique: true,
                filter: "[VisitorUserId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessMeetingParticipants_ExhibitorId",
                table: "BusinessMeetingParticipants",
                column: "ExhibitorId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessMeetingParticipants_VisitorUserId",
                table: "BusinessMeetingParticipants",
                column: "VisitorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessMeetings_MeetingTableId_Status",
                table: "BusinessMeetings",
                columns: new[] { "MeetingTableId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessMeetings_Status_Start",
                table: "BusinessMeetings",
                columns: new[] { "Status", "Start" });

            migrationBuilder.CreateIndex(
                name: "IX_Connections_PairLowUserId_PairHighUserId",
                table: "Connections",
                columns: new[] { "PairLowUserId", "PairHighUserId" },
                unique: true,
                filter: "[IsActive] = 1 AND [PairLowUserId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Connections_RequesterUserId",
                table: "Connections",
                column: "RequesterUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Connections_TargetUserId",
                table: "Connections",
                column: "TargetUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ContactInquiries_IsHandled_CreatedAt",
                table: "ContactInquiries",
                columns: new[] { "IsHandled", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ContentBlocks_IsActive_LastUpdatedAt",
                table: "ContentBlocks",
                columns: new[] { "IsActive", "LastUpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ContentBlocks_Key",
                table: "ContentBlocks",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Countries_Code",
                table: "Countries",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Countries_HeadOfDelegationUserProfileId",
                table: "Countries",
                column: "HeadOfDelegationUserProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_Countries_IsActive_DisplayOrder",
                table: "Countries",
                columns: new[] { "IsActive", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_DelegationAvailabilityWindows_CountryId_IsActive_Start",
                table: "DelegationAvailabilityWindows",
                columns: new[] { "CountryId", "IsActive", "Start" });

            migrationBuilder.CreateIndex(
                name: "IX_DelegationAvailabilityWindows_CountryId_Start",
                table: "DelegationAvailabilityWindows",
                columns: new[] { "CountryId", "Start" },
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_DelegationMeetingActionTokens_DelegationMeetingRequestId",
                table: "DelegationMeetingActionTokens",
                column: "DelegationMeetingRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_DelegationMeetingActionTokens_TokenHash",
                table: "DelegationMeetingActionTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DelegationMeetingRequests_AvailabilityWindowId",
                table: "DelegationMeetingRequests",
                column: "AvailabilityWindowId");

            migrationBuilder.CreateIndex(
                name: "IX_DelegationMeetingRequests_HallId_SlotStart",
                table: "DelegationMeetingRequests",
                columns: new[] { "HallId", "SlotStart" },
                unique: true,
                filter: "[HallId] IS NOT NULL AND [SlotStart] IS NOT NULL AND [Status] <> 0 AND [Status] <> 2 AND [Status] <> 3");

            migrationBuilder.CreateIndex(
                name: "IX_DelegationMeetingRequests_MeetingTableId",
                table: "DelegationMeetingRequests",
                column: "MeetingTableId");

            migrationBuilder.CreateIndex(
                name: "IX_DelegationMeetingRequests_RequestedByUserId",
                table: "DelegationMeetingRequests",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DelegationMeetingRequests_RequestingCountryId",
                table: "DelegationMeetingRequests",
                column: "RequestingCountryId");

            migrationBuilder.CreateIndex(
                name: "IX_DelegationMeetingRequests_TargetCountryId_Status_CreatedAt",
                table: "DelegationMeetingRequests",
                columns: new[] { "TargetCountryId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DevicePositionPings_HallId_CapturedAt",
                table: "DevicePositionPings",
                columns: new[] { "HallId", "CapturedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DevicePositionPings_UserId_CapturedAt",
                table: "DevicePositionPings",
                columns: new[] { "UserId", "CapturedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailTemplates_Type",
                table: "EmailTemplates",
                column: "Type",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExhibitorMemberships_ExhibitorId",
                table: "ExhibitorMemberships",
                column: "ExhibitorId");

            migrationBuilder.CreateIndex(
                name: "IX_ExhibitorMemberships_UserId",
                table: "ExhibitorMemberships",
                column: "UserId",
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Exhibitors_CountryId",
                table: "Exhibitors",
                column: "CountryId");

            migrationBuilder.CreateIndex(
                name: "IX_Exhibitors_IsActive_NameArabic",
                table: "Exhibitors",
                columns: new[] { "IsActive", "NameArabic" });

            migrationBuilder.CreateIndex(
                name: "IX_ExhibitorVisitorScans_ExhibitorId_VisitorProfileId",
                table: "ExhibitorVisitorScans",
                columns: new[] { "ExhibitorId", "VisitorProfileId" },
                unique: true,
                filter: "[IsActive] = 1 AND [ExhibitorId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ExhibitorVisitorScans_ExhibitorUserId_VisitorProfileId",
                table: "ExhibitorVisitorScans",
                columns: new[] { "ExhibitorUserId", "VisitorProfileId" });

            migrationBuilder.CreateIndex(
                name: "IX_ExhibitorVisitorScans_VisitorProfileId",
                table: "ExhibitorVisitorScans",
                column: "VisitorProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_FaqEntries_FaqGroupId_IsActive_DisplayOrder",
                table: "FaqEntries",
                columns: new[] { "FaqGroupId", "IsActive", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_FaqGroups_IsActive_DisplayOrder",
                table: "FaqGroups",
                columns: new[] { "IsActive", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_GateAssignments_GateId_IsActive",
                table: "GateAssignments",
                columns: new[] { "GateId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_GateAssignments_GateId_UserId",
                table: "GateAssignments",
                columns: new[] { "GateId", "UserId" },
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_GateAssignments_UserId_IsActive",
                table: "GateAssignments",
                columns: new[] { "UserId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_GateProfileTypeAllow_ProfileTypeId",
                table: "GateProfileTypeAllow",
                column: "ProfileTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Gates_Code",
                table: "Gates",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Gates_HallId",
                table: "Gates",
                column: "HallId");

            migrationBuilder.CreateIndex(
                name: "IX_Gates_IsActive_Name",
                table: "Gates",
                columns: new[] { "IsActive", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_GateScan_Gate_ScannedAt",
                table: "GateScans",
                columns: new[] { "GateId", "ScannedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_GateScan_Gate_UserProfile_5sWindow",
                table: "GateScans",
                columns: new[] { "GateId", "UserProfileId", "ScannedAt" },
                descending: new[] { false, false, true },
                filter: "[UserProfileId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_GateScan_ScannedBy_ScannedAt",
                table: "GateScans",
                columns: new[] { "ScannedByUserId", "ScannedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_GateScan_UserProfile_LastAllowed",
                table: "GateScans",
                columns: new[] { "UserProfileId", "ScannedAt" },
                descending: new[] { false, true },
                filter: "[Outcome] = 0 AND [UserProfileId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_GateScan_Idempotency",
                table: "GateScans",
                columns: new[] { "IdempotencyKey", "GateId" },
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_HallAllocations_HallId_Purpose_ReleasedAt",
                table: "HallAllocations",
                columns: new[] { "HallId", "Purpose", "ReleasedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_HallAttendances_HallId_Leave",
                table: "HallAttendances",
                columns: new[] { "HallId", "Leave" });

            migrationBuilder.CreateIndex(
                name: "IX_HallAttendances_SessionId_UserProfileId",
                table: "HallAttendances",
                columns: new[] { "SessionId", "UserProfileId" },
                unique: true,
                filter: "[Leave] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_HallAttendances_SessionId_UserProfileId_Leave",
                table: "HallAttendances",
                columns: new[] { "SessionId", "UserProfileId", "Leave" });

            migrationBuilder.CreateIndex(
                name: "IX_HallAttendances_UserProfileId",
                table: "HallAttendances",
                column: "UserProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_HallAvailabilityWindows_HallId_IsActive_Start",
                table: "HallAvailabilityWindows",
                columns: new[] { "HallId", "IsActive", "Start" });

            migrationBuilder.CreateIndex(
                name: "IX_HallAvailabilityWindows_HallId_Start",
                table: "HallAvailabilityWindows",
                columns: new[] { "HallId", "Start" },
                unique: true,
                filter: "[IsActive] = 1");

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
                name: "IX_HallSeatLayouts_HallId",
                table: "HallSeatLayouts",
                column: "HallId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Interests_IsActive_DisplayOrder",
                table: "Interests",
                columns: new[] { "IsActive", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Interests_Name",
                table: "Interests",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_IsActive_State_CreatedAt",
                table: "Invitations",
                columns: new[] { "IsActive", "State", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_SentByUserId",
                table: "Invitations",
                column: "SentByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_SentToUserProfileId",
                table: "Invitations",
                column: "SentToUserProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaItems_ImageFileId",
                table: "MediaItems",
                column: "ImageFileId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaItems_IsActive_Album_DisplayOrder",
                table: "MediaItems",
                columns: new[] { "IsActive", "Album", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_MediaItems_IsActive_Kind_DisplayOrder",
                table: "MediaItems",
                columns: new[] { "IsActive", "Kind", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_MediaItems_ThumbnailFileId",
                table: "MediaItems",
                column: "ThumbnailFileId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaItems_VideoFileId",
                table: "MediaItems",
                column: "VideoFileId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaPartners_CountryId",
                table: "MediaPartners",
                column: "CountryId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaPartners_IsActive_DisplayOrder",
                table: "MediaPartners",
                columns: new[] { "IsActive", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_MediaPartners_LogoFileId",
                table: "MediaPartners",
                column: "LogoFileId");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingActionTokens_SpeakerMeetingRequestId",
                table: "MeetingActionTokens",
                column: "SpeakerMeetingRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingActionTokens_TokenHash",
                table: "MeetingActionTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MeetingTables_HallId_Code",
                table: "MeetingTables",
                columns: new[] { "HallId", "Code" },
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingTables_HallId_IsActive",
                table: "MeetingTables",
                columns: new[] { "HallId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_News_ImageFileId",
                table: "News",
                column: "ImageFileId");

            migrationBuilder.CreateIndex(
                name: "IX_News_IsActive_PublishedAt",
                table: "News",
                columns: new[] { "IsActive", "PublishedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationBroadcasts_Status_CreatedAt",
                table: "NotificationBroadcasts",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OperationLog_ActorUserId_Timestamp",
                table: "OperationLog",
                columns: new[] { "ActorUserId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_OperationLog_EventType_Timestamp",
                table: "OperationLog",
                columns: new[] { "EventType", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_OperationLog_SubjectEmail",
                table: "OperationLog",
                column: "SubjectEmail");

            migrationBuilder.CreateIndex(
                name: "IX_OperationLog_Timestamp",
                table: "OperationLog",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_Organisations_CommercialRegistration",
                table: "Organisations",
                column: "CommercialRegistration",
                unique: true,
                filter: "[CommercialRegistration] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Organisations_IsActive_NameArabic",
                table: "Organisations",
                columns: new[] { "IsActive", "NameArabic" });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationAboutItems_OrganizationProfileId_IsActive_DisplayOrder",
                table: "OrganizationAboutItems",
                columns: new[] { "OrganizationProfileId", "IsActive", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationDetails_OrganizationProfileId_IsActive_DisplayOrder",
                table: "OrganizationDetails",
                columns: new[] { "OrganizationProfileId", "IsActive", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationProfile_BackgroundVideoFileId",
                table: "OrganizationProfile",
                column: "BackgroundVideoFileId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationProfile_LiveStreamFileId",
                table: "OrganizationProfile",
                column: "LiveStreamFileId");

            migrationBuilder.CreateIndex(
                name: "IX_ParticipationDocumentRequests_RequestedByUserId",
                table: "ParticipationDocumentRequests",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ParticipationDocumentRequests_Status_CreatedAt",
                table: "ParticipationDocumentRequests",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProfileIdentityDocuments_NumberHash",
                table: "ProfileIdentityDocuments",
                column: "NumberHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProfileIdentityDocuments_ProfileId_Kind",
                table: "ProfileIdentityDocuments",
                columns: new[] { "ProfileId", "Kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProfileTypes_Code",
                table: "ProfileTypes",
                column: "Code",
                unique: true,
                filter: "[IsActive] = 1 AND [Code] <> 0");

            migrationBuilder.CreateIndex(
                name: "IX_ProfileTypes_IsForVisitor_IsActive",
                table: "ProfileTypes",
                columns: new[] { "IsForVisitor", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ProfileTypes_Name",
                table: "ProfileTypes",
                column: "Name",
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_ProgrammeDays_Date",
                table: "ProgrammeDays",
                column: "Date",
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_ProgrammeDays_IsActive_DisplayOrder_Date",
                table: "ProgrammeDays",
                columns: new[] { "IsActive", "DisplayOrder", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_RatingAnswers_RatingQuestionId",
                table: "RatingAnswers",
                column: "RatingQuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_RatingAnswers_RatingResponseId_RatingQuestionId",
                table: "RatingAnswers",
                columns: new[] { "RatingResponseId", "RatingQuestionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RatingQuestionGroups_RatingTypeId_IsActive_DisplayOrder",
                table: "RatingQuestionGroups",
                columns: new[] { "RatingTypeId", "IsActive", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_RatingQuestions_RatingQuestionGroupId",
                table: "RatingQuestions",
                column: "RatingQuestionGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_RatingQuestions_RatingTypeId_IsActive_DisplayOrder",
                table: "RatingQuestions",
                columns: new[] { "RatingTypeId", "IsActive", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_RatingResponses_RatingTypeId",
                table: "RatingResponses",
                column: "RatingTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_RatingResponses_TargetId_IsActive",
                table: "RatingResponses",
                columns: new[] { "TargetId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_RatingResponses_UserId_RatingTypeId_TargetId",
                table: "RatingResponses",
                columns: new[] { "UserId", "RatingTypeId", "TargetId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RatingTypes_Code",
                table: "RatingTypes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RatingTypes_IsActive_DisplayOrder",
                table: "RatingTypes",
                columns: new[] { "IsActive", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Regions_Code",
                table: "Regions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Regions_IsActive_SortOrder",
                table: "Regions",
                columns: new[] { "IsActive", "SortOrder" });

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
                name: "IX_SavedContacts_OwnerUserId_SubjectUserId",
                table: "SavedContacts",
                columns: new[] { "OwnerUserId", "SubjectUserId" },
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_ScanIdempotency_StoredAt",
                table: "ScanIdempotency",
                column: "StoredAt");

            migrationBuilder.CreateIndex(
                name: "IX_SeatReservations_NoShowReleaseAt",
                table: "SeatReservations",
                column: "NoShowReleaseAt",
                filter: "[ReleasedAt] IS NULL AND [NoShowReleaseAt] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SeatReservations_ReservedForProfileId_ReleasedAt",
                table: "SeatReservations",
                columns: new[] { "ReservedForProfileId", "ReleasedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SeatReservations_SessionId_ReleasedAt",
                table: "SeatReservations",
                columns: new[] { "SessionId", "ReleasedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SeatReservations_SessionId_ReservedForProfileId",
                table: "SeatReservations",
                columns: new[] { "SessionId", "ReservedForProfileId" },
                unique: true,
                filter: "[ReleasedAt] IS NULL AND [ReservedForProfileId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SeatReservations_SessionId_RowLabel_SeatNumber",
                table: "SeatReservations",
                columns: new[] { "SessionId", "RowLabel", "SeatNumber" },
                unique: true,
                filter: "[ReleasedAt] IS NULL AND [RowLabel] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SeatReservations_Status_ReleasedAt",
                table: "SeatReservations",
                columns: new[] { "Status", "ReleasedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SessionCategories_IsActive_DisplayOrder",
                table: "SessionCategories",
                columns: new[] { "IsActive", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_SessionCategories_Name",
                table: "SessionCategories",
                column: "Name",
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_SessionFavourites_SessionId",
                table: "SessionFavourites",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionFavourites_UserId_SessionId",
                table: "SessionFavourites",
                columns: new[] { "UserId", "SessionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SessionModerators_UserId",
                table: "SessionModerators",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionOutcomes_SessionId_IsActive_DisplayOrder",
                table: "SessionOutcomes",
                columns: new[] { "SessionId", "IsActive", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_SessionQuestions_SessionId_IsPushed_Order",
                table: "SessionQuestions",
                columns: new[] { "SessionId", "IsPushed", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_SessionQuestions_SessionId_Status_Order",
                table: "SessionQuestions",
                columns: new[] { "SessionId", "Status", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_SessionQuestions_Status_CreatedAt",
                table: "SessionQuestions",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_CategoryId",
                table: "Sessions",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_Code",
                table: "Sessions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_HallId_Start",
                table: "Sessions",
                columns: new[] { "HallId", "Start" });

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_IsActive_Start",
                table: "Sessions",
                columns: new[] { "IsActive", "Start" });

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_LiveSignLanguageFileId",
                table: "Sessions",
                column: "LiveSignLanguageFileId");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_LiveStreamFileId",
                table: "Sessions",
                column: "LiveStreamFileId");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_RecordingFileId",
                table: "Sessions",
                column: "RecordingFileId");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_Status_Start",
                table: "Sessions",
                columns: new[] { "Status", "Start" });

            migrationBuilder.CreateIndex(
                name: "IX_SessionSpeakers_SpeakerId",
                table: "SessionSpeakers",
                column: "SpeakerId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionSummaries_IsActive_PublishedAt",
                table: "SessionSummaries",
                columns: new[] { "IsActive", "PublishedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SessionSummaries_SessionId",
                table: "SessionSummaries",
                column: "SessionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SessionSummaries_SummaryVideoFileId",
                table: "SessionSummaries",
                column: "SummaryVideoFileId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionThemes_ThemeId",
                table: "SessionThemes",
                column: "ThemeId");

            migrationBuilder.CreateIndex(
                name: "IX_SpeakerAvailabilityWindows_SpeakerId_IsActive_Start",
                table: "SpeakerAvailabilityWindows",
                columns: new[] { "SpeakerId", "IsActive", "Start" });

            migrationBuilder.CreateIndex(
                name: "IX_SpeakerAvailabilityWindows_SpeakerId_Start",
                table: "SpeakerAvailabilityWindows",
                columns: new[] { "SpeakerId", "Start" },
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_SpeakerMeetingRequests_AvailabilityWindowId",
                table: "SpeakerMeetingRequests",
                column: "AvailabilityWindowId");

            migrationBuilder.CreateIndex(
                name: "IX_SpeakerMeetingRequests_HallId_SlotStart",
                table: "SpeakerMeetingRequests",
                columns: new[] { "HallId", "SlotStart" },
                unique: true,
                filter: "[HallId] IS NOT NULL AND [SlotStart] IS NOT NULL AND [Status] <> 0 AND [Status] <> 2 AND [Status] <> 3");

            migrationBuilder.CreateIndex(
                name: "IX_SpeakerMeetingRequests_MeetingTableId",
                table: "SpeakerMeetingRequests",
                column: "MeetingTableId");

            migrationBuilder.CreateIndex(
                name: "IX_SpeakerMeetingRequests_RequestedByUserId",
                table: "SpeakerMeetingRequests",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SpeakerMeetingRequests_SpeakerId_SlotStart",
                table: "SpeakerMeetingRequests",
                columns: new[] { "SpeakerId", "SlotStart" },
                unique: true,
                filter: "[SlotStart] IS NOT NULL AND [Status] <> 0 AND [Status] <> 2 AND [Status] <> 3");

            migrationBuilder.CreateIndex(
                name: "IX_SpeakerMeetingRequests_SpeakerId_Status_CreatedAt",
                table: "SpeakerMeetingRequests",
                columns: new[] { "SpeakerId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SpeakerPresentations_SessionId",
                table: "SpeakerPresentations",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_SpeakerPresentations_SpeakerId_IsActive",
                table: "SpeakerPresentations",
                columns: new[] { "SpeakerId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_SpeakerPresentations_StoredFileId",
                table: "SpeakerPresentations",
                column: "StoredFileId");

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
                column: "UserProfileId",
                unique: true,
                filter: "[UserProfileId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Sponsors_CountryId",
                table: "Sponsors",
                column: "CountryId");

            migrationBuilder.CreateIndex(
                name: "IX_Sponsors_IsActive_Tier_DisplayOrder",
                table: "Sponsors",
                columns: new[] { "IsActive", "Tier", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Sponsors_LogoFileId",
                table: "Sponsors",
                column: "LogoFileId");

            migrationBuilder.CreateIndex(
                name: "IX_Sponsors_Tier_NameArabic",
                table: "Sponsors",
                columns: new[] { "Tier", "NameArabic" },
                unique: true,
                filter: "[IsActive] = 1");

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
                name: "IX_StoredFiles_RetainUntil",
                table: "StoredFiles",
                column: "RetainUntil",
                filter: "[IsActive] = 1 AND [RetainUntil] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StoredFiles_Service_IsActive",
                table: "StoredFiles",
                columns: new[] { "Service", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_SystemSettings_Key",
                table: "SystemSettings",
                column: "Key",
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Themes_Code",
                table: "Themes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Themes_IsActive_DisplayOrder",
                table: "Themes",
                columns: new[] { "IsActive", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_UserProfileInterests_InterestId",
                table: "UserProfileInterests",
                column: "InterestId");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_AdmissionState",
                table: "UserProfiles",
                column: "AdmissionState");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_BadgeBatchId",
                table: "UserProfiles",
                column: "BadgeBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_IdImageFileId",
                table: "UserProfiles",
                column: "IdImageFileId");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_IqamaNumberHash",
                table: "UserProfiles",
                column: "IqamaNumberHash",
                unique: true,
                filter: "[IqamaNumberHash] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_NationalIdHash",
                table: "UserProfiles",
                column: "NationalIdHash",
                unique: true,
                filter: "[NationalIdHash] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_NationalityId",
                table: "UserProfiles",
                column: "NationalityId");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_OrganisationId",
                table: "UserProfiles",
                column: "OrganisationId");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_PassportNumberHash",
                table: "UserProfiles",
                column: "PassportNumberHash",
                unique: true,
                filter: "[PassportNumberHash] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_ProfileTypeId",
                table: "UserProfiles",
                column: "ProfileTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_QrId",
                table: "UserProfiles",
                column: "QrId",
                unique: true,
                filter: "[QrId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_ReferenceNumber",
                table: "UserProfiles",
                column: "ReferenceNumber",
                unique: true,
                filter: "[ReferenceNumber] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_RegionId",
                table: "UserProfiles",
                column: "RegionId");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_UserId",
                table: "UserProfiles",
                column: "UserId",
                unique: true,
                filter: "[UserId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_VipPhotoFileId",
                table: "UserProfiles",
                column: "VipPhotoFileId");

            migrationBuilder.CreateIndex(
                name: "IX_VenueMapNodes_BoothId",
                table: "VenueMapNodes",
                column: "BoothId");

            migrationBuilder.CreateIndex(
                name: "IX_VenueMapNodes_HallId",
                table: "VenueMapNodes",
                column: "HallId");

            migrationBuilder.CreateIndex(
                name: "IX_VenueMapNodes_IsActive",
                table: "VenueMapNodes",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_VisitorShareTokens_Token",
                table: "VisitorShareTokens",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VisitorShareTokens_UserId",
                table: "VisitorShareTokens",
                column: "UserId",
                unique: true,
                filter: "[IsActive] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiChatMessages");

            migrationBuilder.DropTable(
                name: "AiInvocations");

            migrationBuilder.DropTable(
                name: "AiPromptHistory");

            migrationBuilder.DropTable(
                name: "ArchiveMediaItems");

            migrationBuilder.DropTable(
                name: "ArchivePastSpeakers");

            migrationBuilder.DropTable(
                name: "ArchiveSessionTitles");

            migrationBuilder.DropTable(
                name: "ArchiveVisibility");

            migrationBuilder.DropTable(
                name: "BadgeBatchItems");

            migrationBuilder.DropTable(
                name: "BadgeUpdateRequests");

            migrationBuilder.DropTable(
                name: "Banners");

            migrationBuilder.DropTable(
                name: "BusinessMeetingParticipants");

            migrationBuilder.DropTable(
                name: "Connections");

            migrationBuilder.DropTable(
                name: "ContactInquiries");

            migrationBuilder.DropTable(
                name: "ContentBlocks");

            migrationBuilder.DropTable(
                name: "DelegationMeetingActionTokens");

            migrationBuilder.DropTable(
                name: "DevicePositionPings");

            migrationBuilder.DropTable(
                name: "EmailTemplates");

            migrationBuilder.DropTable(
                name: "EventEdition");

            migrationBuilder.DropTable(
                name: "ExhibitorMemberships");

            migrationBuilder.DropTable(
                name: "ExhibitorVisitorScans");

            migrationBuilder.DropTable(
                name: "FaqEntries");

            migrationBuilder.DropTable(
                name: "GateAssignments");

            migrationBuilder.DropTable(
                name: "GateProfileTypeAllow");

            migrationBuilder.DropTable(
                name: "GateScans");

            migrationBuilder.DropTable(
                name: "HallAllocations");

            migrationBuilder.DropTable(
                name: "HallAttendances");

            migrationBuilder.DropTable(
                name: "HallAvailabilityWindows");

            migrationBuilder.DropTable(
                name: "HallSeatLayouts");

            migrationBuilder.DropTable(
                name: "Invitations");

            migrationBuilder.DropTable(
                name: "MediaItems");

            migrationBuilder.DropTable(
                name: "MediaPartners");

            migrationBuilder.DropTable(
                name: "MeetingActionTokens");

            migrationBuilder.DropTable(
                name: "News");

            migrationBuilder.DropTable(
                name: "NotificationBroadcasts");

            migrationBuilder.DropTable(
                name: "OperationLog");

            migrationBuilder.DropTable(
                name: "OrganizationAboutItems");

            migrationBuilder.DropTable(
                name: "OrganizationDetails");

            migrationBuilder.DropTable(
                name: "ParticipationDocumentRequests");

            migrationBuilder.DropTable(
                name: "ProfileIdentityDocuments");

            migrationBuilder.DropTable(
                name: "ProgrammeDays");

            migrationBuilder.DropTable(
                name: "RatingAnswers");

            migrationBuilder.DropTable(
                name: "RegistrationGate");

            migrationBuilder.DropTable(
                name: "RowAudits",
                schema: "app");

            migrationBuilder.DropTable(
                name: "SavedContacts");

            migrationBuilder.DropTable(
                name: "ScanIdempotency");

            migrationBuilder.DropTable(
                name: "SeatReservations");

            migrationBuilder.DropTable(
                name: "SessionFavourites");

            migrationBuilder.DropTable(
                name: "SessionModerators");

            migrationBuilder.DropTable(
                name: "SessionOutcomes");

            migrationBuilder.DropTable(
                name: "SessionQuestions");

            migrationBuilder.DropTable(
                name: "SessionSpeakers");

            migrationBuilder.DropTable(
                name: "SessionSummaries");

            migrationBuilder.DropTable(
                name: "SessionThemes");

            migrationBuilder.DropTable(
                name: "SpeakerPresentations");

            migrationBuilder.DropTable(
                name: "Sponsors");

            migrationBuilder.DropTable(
                name: "SystemSettings");

            migrationBuilder.DropTable(
                name: "UserProfileInterests");

            migrationBuilder.DropTable(
                name: "VenueMapNodes");

            migrationBuilder.DropTable(
                name: "VisitorShareTokens");

            migrationBuilder.DropTable(
                name: "AiPrompts");

            migrationBuilder.DropTable(
                name: "ArchiveEditions");

            migrationBuilder.DropTable(
                name: "BusinessMeetings");

            migrationBuilder.DropTable(
                name: "DelegationMeetingRequests");

            migrationBuilder.DropTable(
                name: "FaqGroups");

            migrationBuilder.DropTable(
                name: "Gates");

            migrationBuilder.DropTable(
                name: "SpeakerMeetingRequests");

            migrationBuilder.DropTable(
                name: "OrganizationProfile");

            migrationBuilder.DropTable(
                name: "RatingQuestions");

            migrationBuilder.DropTable(
                name: "RatingResponses");

            migrationBuilder.DropTable(
                name: "Themes");

            migrationBuilder.DropTable(
                name: "Sessions");

            migrationBuilder.DropTable(
                name: "Interests");

            migrationBuilder.DropTable(
                name: "Booths");

            migrationBuilder.DropTable(
                name: "DelegationAvailabilityWindows");

            migrationBuilder.DropTable(
                name: "MeetingTables");

            migrationBuilder.DropTable(
                name: "SpeakerAvailabilityWindows");

            migrationBuilder.DropTable(
                name: "RatingQuestionGroups");

            migrationBuilder.DropTable(
                name: "SessionCategories");

            migrationBuilder.DropTable(
                name: "Exhibitors");

            migrationBuilder.DropTable(
                name: "Halls");

            migrationBuilder.DropTable(
                name: "Speakers");

            migrationBuilder.DropTable(
                name: "RatingTypes");

            migrationBuilder.DropTable(
                name: "Countries");

            migrationBuilder.DropTable(
                name: "UserProfiles");

            migrationBuilder.DropTable(
                name: "BadgeBatches");

            migrationBuilder.DropTable(
                name: "Organisations");

            migrationBuilder.DropTable(
                name: "ProfileTypes");

            migrationBuilder.DropTable(
                name: "Regions");

            migrationBuilder.DropTable(
                name: "StoredFiles");

            migrationBuilder.DropSequence(
                name: "RegistrationReferenceSequence");
        }
    }
}
