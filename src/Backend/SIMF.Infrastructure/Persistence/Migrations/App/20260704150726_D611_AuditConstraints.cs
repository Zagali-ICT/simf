using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMF.Infrastructure.Persistence.Migrations.App
{
    /// <inheritdoc />
    public partial class D611_AuditConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ExhibitorMemberships_Exhibitors_ExhibitorId",
                table: "ExhibitorMemberships");

            migrationBuilder.DropForeignKey(
                name: "FK_GateAssignments_Gates_GateId",
                table: "GateAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_GateScans_Gates_GateId",
                table: "GateScans");

            migrationBuilder.DropForeignKey(
                name: "FK_SeatReservations_Sessions_SessionId",
                table: "SeatReservations");

            migrationBuilder.DropForeignKey(
                name: "FK_SpeakerMeetingRequests_Speakers_SpeakerId",
                table: "SpeakerMeetingRequests");

            migrationBuilder.DropIndex(
                name: "IX_VisitorShareTokens_UserId",
                table: "VisitorShareTokens");

            migrationBuilder.DropIndex(
                name: "IX_SavedContacts_OwnerUserId",
                table: "SavedContacts");

            migrationBuilder.DropIndex(
                name: "IX_SavedContacts_OwnerUserId_SubjectUserId",
                table: "SavedContacts");

            migrationBuilder.DropIndex(
                name: "IX_ExhibitorVisitorScans_ExhibitorUserId",
                table: "ExhibitorVisitorScans");

            migrationBuilder.DropIndex(
                name: "IX_ExhibitorVisitorScans_ExhibitorUserId_VisitorUserId",
                table: "ExhibitorVisitorScans");

            migrationBuilder.DropIndex(
                name: "IX_ExhibitorMemberships_UserId",
                table: "ExhibitorMemberships");

            migrationBuilder.AddColumn<Guid>(
                name: "RegionId",
                table: "UserProfiles",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AvailabilityWindowId",
                table: "SpeakerMeetingRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AllowsVipMeetingSlots",
                table: "ProfileTypes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_VisitorShareTokens_UserId",
                table: "VisitorShareTokens",
                column: "UserId",
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_VenueMapNodes_KindArc",
                table: "VenueMapNodes",
                sql: "([HallId] IS NULL OR [Kind] = 0) AND ([BoothId] IS NULL OR [Kind] = 2) AND NOT ([HallId] IS NOT NULL AND [BoothId] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_RegionId",
                table: "UserProfiles",
                column: "RegionId");

            migrationBuilder.CreateIndex(
                name: "IX_SpeakerMeetingRequests_AvailabilityWindowId",
                table: "SpeakerMeetingRequests",
                column: "AvailabilityWindowId");

            migrationBuilder.CreateIndex(
                name: "IX_SpeakerMeetingRequests_SpeakerId_SlotStartUtc",
                table: "SpeakerMeetingRequests",
                columns: new[] { "SpeakerId", "SlotStartUtc" },
                unique: true,
                filter: "[Status] = 1 AND [SlotStartUtc] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SpeakerMeetingRequests_Slot",
                table: "SpeakerMeetingRequests",
                sql: "[SlotStartUtc] IS NULL OR [SlotEndUtc] IS NULL OR [SlotEndUtc] > [SlotStartUtc]");

            migrationBuilder.CreateIndex(
                name: "IX_SpeakerAvailabilityWindows_SpeakerId_StartUtc",
                table: "SpeakerAvailabilityWindows",
                columns: new[] { "SpeakerId", "StartUtc" },
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SpeakerAvailabilityWindows_TimeWindow",
                table: "SpeakerAvailabilityWindows",
                sql: "[EndUtc] > [StartUtc]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SessionSummaries_ReviewOrder",
                table: "SessionSummaries",
                sql: "[ApprovedAt] IS NULL OR ([ReviewSubmittedAt] IS NOT NULL AND [ApprovedAt] >= [ReviewSubmittedAt])");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Sessions_TimeWindow",
                table: "Sessions",
                sql: "[EndUtc] > [StartUtc]");

            migrationBuilder.CreateIndex(
                name: "IX_SessionCategories_Name",
                table: "SessionCategories",
                column: "Name",
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_SavedContacts_OwnerUserId_SubjectUserId",
                table: "SavedContacts",
                columns: new[] { "OwnerUserId", "SubjectUserId" },
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_RatingResponses_OverallStars",
                table: "RatingResponses",
                sql: "[OverallStars] IS NULL OR [OverallStars] BETWEEN 1 AND 5");

            migrationBuilder.AddCheckConstraint(
                name: "CK_RatingAnswers_Stars",
                table: "RatingAnswers",
                sql: "[Stars] BETWEEN 1 AND 5");

            migrationBuilder.CreateIndex(
                name: "IX_ProgrammeDays_Date",
                table: "ProgrammeDays",
                column: "Date",
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_ProfileTypes_Name",
                table: "ProfileTypes",
                column: "Name",
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_OperationLog_ActorUserId_TimestampUtc",
                table: "OperationLog",
                columns: new[] { "ActorUserId", "TimestampUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MediaItems_IsActive_Kind_DisplayOrder",
                table: "MediaItems",
                columns: new[] { "IsActive", "Kind", "DisplayOrder" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Halls_Geofence",
                table: "Halls",
                sql: "([GeofenceCenterLat] IS NULL AND [GeofenceCenterLon] IS NULL AND [GeofenceRadiusMeters] IS NULL) OR ([GeofenceCenterLat] IS NOT NULL AND [GeofenceCenterLon] IS NOT NULL AND [GeofenceRadiusMeters] IS NOT NULL AND [GeofenceRadiusMeters] > 0)");

            migrationBuilder.CreateIndex(
                name: "IX_HallAttendances_UserId",
                table: "HallAttendances",
                column: "UserId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_HallAllocations_TimeWindow",
                table: "HallAllocations",
                sql: "[EndUtc] > [StartUtc]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_GateScans_DenialPin",
                table: "GateScans",
                sql: "([Outcome] = 1 AND [DenialReasonCode] IS NOT NULL) OR ([Outcome] = 0 AND [DenialReasonCode] IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_GateAssignments_GateId_UserId",
                table: "GateAssignments",
                columns: new[] { "GateId", "UserId" },
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_ExhibitorVisitorScans_ExhibitorUserId_VisitorUserId",
                table: "ExhibitorVisitorScans",
                columns: new[] { "ExhibitorUserId", "VisitorUserId" },
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_ExhibitorMemberships_UserId",
                table: "ExhibitorMemberships",
                column: "UserId",
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DelegationMeetingRequests_Slot",
                table: "DelegationMeetingRequests",
                sql: "[SlotStartUtc] IS NULL OR [SlotEndUtc] IS NULL OR [SlotEndUtc] > [SlotStartUtc]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BusinessMeetings_TimeWindow",
                table: "BusinessMeetings",
                sql: "[EndUtc] > [StartUtc]");

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

            migrationBuilder.AddCheckConstraint(
                name: "CK_BusinessMeetingParticipants_PartyXor",
                table: "BusinessMeetingParticipants",
                sql: "([Kind] = 0 AND [ExhibitorId] IS NOT NULL AND [VisitorUserId] IS NULL) OR ([Kind] = 1 AND [VisitorUserId] IS NOT NULL AND [ExhibitorId] IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Banners_TimeWindow",
                table: "Banners",
                sql: "[EndUtc] > [StartUtc]");

            migrationBuilder.AddForeignKey(
                name: "FK_AiPromptHistory_AiPrompts_AiPromptId",
                table: "AiPromptHistory",
                column: "AiPromptId",
                principalTable: "AiPrompts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ExhibitorMemberships_Exhibitors_ExhibitorId",
                table: "ExhibitorMemberships",
                column: "ExhibitorId",
                principalTable: "Exhibitors",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_GateAssignments_Gates_GateId",
                table: "GateAssignments",
                column: "GateId",
                principalTable: "Gates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_GateScans_Gates_GateId",
                table: "GateScans",
                column: "GateId",
                principalTable: "Gates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SeatReservations_Sessions_SessionId",
                table: "SeatReservations",
                column: "SessionId",
                principalTable: "Sessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SpeakerMeetingRequests_SpeakerAvailabilityWindows_AvailabilityWindowId",
                table: "SpeakerMeetingRequests",
                column: "AvailabilityWindowId",
                principalTable: "SpeakerAvailabilityWindows",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_SpeakerMeetingRequests_Speakers_SpeakerId",
                table: "SpeakerMeetingRequests",
                column: "SpeakerId",
                principalTable: "Speakers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserProfiles_Regions_RegionId",
                table: "UserProfiles",
                column: "RegionId",
                principalTable: "Regions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AiPromptHistory_AiPrompts_AiPromptId",
                table: "AiPromptHistory");

            migrationBuilder.DropForeignKey(
                name: "FK_ExhibitorMemberships_Exhibitors_ExhibitorId",
                table: "ExhibitorMemberships");

            migrationBuilder.DropForeignKey(
                name: "FK_GateAssignments_Gates_GateId",
                table: "GateAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_GateScans_Gates_GateId",
                table: "GateScans");

            migrationBuilder.DropForeignKey(
                name: "FK_SeatReservations_Sessions_SessionId",
                table: "SeatReservations");

            migrationBuilder.DropForeignKey(
                name: "FK_SpeakerMeetingRequests_SpeakerAvailabilityWindows_AvailabilityWindowId",
                table: "SpeakerMeetingRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_SpeakerMeetingRequests_Speakers_SpeakerId",
                table: "SpeakerMeetingRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_UserProfiles_Regions_RegionId",
                table: "UserProfiles");

            migrationBuilder.DropIndex(
                name: "IX_VisitorShareTokens_UserId",
                table: "VisitorShareTokens");

            migrationBuilder.DropCheckConstraint(
                name: "CK_VenueMapNodes_KindArc",
                table: "VenueMapNodes");

            migrationBuilder.DropIndex(
                name: "IX_UserProfiles_RegionId",
                table: "UserProfiles");

            migrationBuilder.DropIndex(
                name: "IX_SpeakerMeetingRequests_AvailabilityWindowId",
                table: "SpeakerMeetingRequests");

            migrationBuilder.DropIndex(
                name: "IX_SpeakerMeetingRequests_SpeakerId_SlotStartUtc",
                table: "SpeakerMeetingRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SpeakerMeetingRequests_Slot",
                table: "SpeakerMeetingRequests");

            migrationBuilder.DropIndex(
                name: "IX_SpeakerAvailabilityWindows_SpeakerId_StartUtc",
                table: "SpeakerAvailabilityWindows");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SpeakerAvailabilityWindows_TimeWindow",
                table: "SpeakerAvailabilityWindows");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SessionSummaries_ReviewOrder",
                table: "SessionSummaries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Sessions_TimeWindow",
                table: "Sessions");

            migrationBuilder.DropIndex(
                name: "IX_SessionCategories_Name",
                table: "SessionCategories");

            migrationBuilder.DropIndex(
                name: "IX_SavedContacts_OwnerUserId_SubjectUserId",
                table: "SavedContacts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_RatingResponses_OverallStars",
                table: "RatingResponses");

            migrationBuilder.DropCheckConstraint(
                name: "CK_RatingAnswers_Stars",
                table: "RatingAnswers");

            migrationBuilder.DropIndex(
                name: "IX_ProgrammeDays_Date",
                table: "ProgrammeDays");

            migrationBuilder.DropIndex(
                name: "IX_ProfileTypes_Name",
                table: "ProfileTypes");

            migrationBuilder.DropIndex(
                name: "IX_OperationLog_ActorUserId_TimestampUtc",
                table: "OperationLog");

            migrationBuilder.DropIndex(
                name: "IX_MediaItems_IsActive_Kind_DisplayOrder",
                table: "MediaItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Halls_Geofence",
                table: "Halls");

            migrationBuilder.DropIndex(
                name: "IX_HallAttendances_UserId",
                table: "HallAttendances");

            migrationBuilder.DropCheckConstraint(
                name: "CK_HallAllocations_TimeWindow",
                table: "HallAllocations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_GateScans_DenialPin",
                table: "GateScans");

            migrationBuilder.DropIndex(
                name: "IX_GateAssignments_GateId_UserId",
                table: "GateAssignments");

            migrationBuilder.DropIndex(
                name: "IX_ExhibitorVisitorScans_ExhibitorUserId_VisitorUserId",
                table: "ExhibitorVisitorScans");

            migrationBuilder.DropIndex(
                name: "IX_ExhibitorMemberships_UserId",
                table: "ExhibitorMemberships");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DelegationMeetingRequests_Slot",
                table: "DelegationMeetingRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BusinessMeetings_TimeWindow",
                table: "BusinessMeetings");

            migrationBuilder.DropIndex(
                name: "IX_BusinessMeetingParticipants_BusinessMeetingId_ExhibitorId",
                table: "BusinessMeetingParticipants");

            migrationBuilder.DropIndex(
                name: "IX_BusinessMeetingParticipants_BusinessMeetingId_VisitorUserId",
                table: "BusinessMeetingParticipants");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BusinessMeetingParticipants_PartyXor",
                table: "BusinessMeetingParticipants");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Banners_TimeWindow",
                table: "Banners");

            migrationBuilder.DropColumn(
                name: "RegionId",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "AvailabilityWindowId",
                table: "SpeakerMeetingRequests");

            migrationBuilder.DropColumn(
                name: "AllowsVipMeetingSlots",
                table: "ProfileTypes");

            migrationBuilder.CreateIndex(
                name: "IX_VisitorShareTokens_UserId",
                table: "VisitorShareTokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_SavedContacts_OwnerUserId",
                table: "SavedContacts",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SavedContacts_OwnerUserId_SubjectUserId",
                table: "SavedContacts",
                columns: new[] { "OwnerUserId", "SubjectUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_ExhibitorVisitorScans_ExhibitorUserId",
                table: "ExhibitorVisitorScans",
                column: "ExhibitorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ExhibitorVisitorScans_ExhibitorUserId_VisitorUserId",
                table: "ExhibitorVisitorScans",
                columns: new[] { "ExhibitorUserId", "VisitorUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_ExhibitorMemberships_UserId",
                table: "ExhibitorMemberships",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ExhibitorMemberships_Exhibitors_ExhibitorId",
                table: "ExhibitorMemberships",
                column: "ExhibitorId",
                principalTable: "Exhibitors",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GateAssignments_Gates_GateId",
                table: "GateAssignments",
                column: "GateId",
                principalTable: "Gates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GateScans_Gates_GateId",
                table: "GateScans",
                column: "GateId",
                principalTable: "Gates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SeatReservations_Sessions_SessionId",
                table: "SeatReservations",
                column: "SessionId",
                principalTable: "Sessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SpeakerMeetingRequests_Speakers_SpeakerId",
                table: "SpeakerMeetingRequests",
                column: "SpeakerId",
                principalTable: "Speakers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
