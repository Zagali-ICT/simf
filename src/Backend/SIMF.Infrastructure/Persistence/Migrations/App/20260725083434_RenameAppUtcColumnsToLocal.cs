using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMF.Infrastructure.Persistence.Migrations.App
{
    /// <inheritdoc />
    public partial class RenameAppUtcColumnsToLocal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StoredFiles_RetainUntilUtc",
                table: "StoredFiles");

            migrationBuilder.DropIndex(
                name: "IX_SpeakerMeetingRequests_HallId_SlotStartUtc",
                table: "SpeakerMeetingRequests");

            migrationBuilder.DropIndex(
                name: "IX_SpeakerMeetingRequests_SpeakerId_SlotStartUtc",
                table: "SpeakerMeetingRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SpeakerMeetingRequests_Slot",
                table: "SpeakerMeetingRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SpeakerAvailabilityWindows_TimeWindow",
                table: "SpeakerAvailabilityWindows");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Sessions_TimeWindow",
                table: "Sessions");

            migrationBuilder.DropIndex(
                name: "IX_SeatReservations_ExpiresUtc",
                table: "SeatReservations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_HallAvailabilityWindows_TimeWindow",
                table: "HallAvailabilityWindows");

            migrationBuilder.DropIndex(
                name: "IX_HallAttendances_SessionId_UserId",
                table: "HallAttendances");

            migrationBuilder.DropCheckConstraint(
                name: "CK_HallAllocations_TimeWindow",
                table: "HallAllocations");

            migrationBuilder.DropIndex(
                name: "IX_DelegationMeetingRequests_HallId_SlotStartUtc",
                table: "DelegationMeetingRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DelegationMeetingRequests_Slot",
                table: "DelegationMeetingRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DelegationAvailabilityWindows_TimeWindow",
                table: "DelegationAvailabilityWindows");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BusinessMeetings_TimeWindow",
                table: "BusinessMeetings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Banners_TimeWindow",
                table: "Banners");

            migrationBuilder.RenameColumn(
                name: "SecureDestroyedUtc",
                table: "StoredFiles",
                newName: "SecureDestroyed");

            migrationBuilder.RenameColumn(
                name: "RetainUntilUtc",
                table: "StoredFiles",
                newName: "RetainUntil");

            migrationBuilder.RenameColumn(
                name: "SlotStartUtc",
                table: "SpeakerMeetingRequests",
                newName: "SlotStart");

            migrationBuilder.RenameColumn(
                name: "SlotEndUtc",
                table: "SpeakerMeetingRequests",
                newName: "SlotEnd");

            migrationBuilder.RenameColumn(
                name: "ReminderSentUtc",
                table: "SpeakerMeetingRequests",
                newName: "ReminderSent");

            migrationBuilder.RenameColumn(
                name: "StartUtc",
                table: "SpeakerAvailabilityWindows",
                newName: "Start");

            migrationBuilder.RenameColumn(
                name: "EndUtc",
                table: "SpeakerAvailabilityWindows",
                newName: "End");

            migrationBuilder.RenameIndex(
                name: "IX_SpeakerAvailabilityWindows_SpeakerId_StartUtc",
                table: "SpeakerAvailabilityWindows",
                newName: "IX_SpeakerAvailabilityWindows_SpeakerId_Start");

            migrationBuilder.RenameIndex(
                name: "IX_SpeakerAvailabilityWindows_SpeakerId_IsActive_StartUtc",
                table: "SpeakerAvailabilityWindows",
                newName: "IX_SpeakerAvailabilityWindows_SpeakerId_IsActive_Start");

            migrationBuilder.RenameColumn(
                name: "StartUtc",
                table: "Sessions",
                newName: "Start");

            migrationBuilder.RenameColumn(
                name: "ReminderSentUtc",
                table: "Sessions",
                newName: "ReminderSent");

            migrationBuilder.RenameColumn(
                name: "RatingPromptSentUtc",
                table: "Sessions",
                newName: "RatingPromptSent");

            migrationBuilder.RenameColumn(
                name: "EndUtc",
                table: "Sessions",
                newName: "End");

            migrationBuilder.RenameIndex(
                name: "IX_Sessions_Status_StartUtc",
                table: "Sessions",
                newName: "IX_Sessions_Status_Start");

            migrationBuilder.RenameIndex(
                name: "IX_Sessions_IsActive_StartUtc",
                table: "Sessions",
                newName: "IX_Sessions_IsActive_Start");

            migrationBuilder.RenameIndex(
                name: "IX_Sessions_HallId_StartUtc",
                table: "Sessions",
                newName: "IX_Sessions_HallId_Start");

            migrationBuilder.RenameColumn(
                name: "ExpiresUtc",
                table: "SeatReservations",
                newName: "Expires");

            migrationBuilder.RenameColumn(
                name: "AutoCloseUtc",
                table: "RegistrationGate",
                newName: "AutoClose");

            migrationBuilder.RenameColumn(
                name: "RatingPromptSentUtc",
                table: "ProgrammeDays",
                newName: "RatingPromptSent");

            migrationBuilder.RenameColumn(
                name: "TimestampUtc",
                table: "OperationLog",
                newName: "Timestamp");

            migrationBuilder.RenameIndex(
                name: "IX_OperationLog_TimestampUtc",
                table: "OperationLog",
                newName: "IX_OperationLog_Timestamp");

            migrationBuilder.RenameIndex(
                name: "IX_OperationLog_EventType_TimestampUtc",
                table: "OperationLog",
                newName: "IX_OperationLog_EventType_Timestamp");

            migrationBuilder.RenameIndex(
                name: "IX_OperationLog_ActorUserId_TimestampUtc",
                table: "OperationLog",
                newName: "IX_OperationLog_ActorUserId_Timestamp");

            migrationBuilder.RenameColumn(
                name: "ExpiresUtc",
                table: "MeetingActionTokens",
                newName: "Expires");

            migrationBuilder.RenameColumn(
                name: "StartUtc",
                table: "HallAvailabilityWindows",
                newName: "Start");

            migrationBuilder.RenameColumn(
                name: "EndUtc",
                table: "HallAvailabilityWindows",
                newName: "End");

            migrationBuilder.RenameIndex(
                name: "IX_HallAvailabilityWindows_HallId_StartUtc",
                table: "HallAvailabilityWindows",
                newName: "IX_HallAvailabilityWindows_HallId_Start");

            migrationBuilder.RenameIndex(
                name: "IX_HallAvailabilityWindows_HallId_IsActive_StartUtc",
                table: "HallAvailabilityWindows",
                newName: "IX_HallAvailabilityWindows_HallId_IsActive_Start");

            migrationBuilder.RenameColumn(
                name: "LeaveUtc",
                table: "HallAttendances",
                newName: "Leave");

            migrationBuilder.RenameColumn(
                name: "EnterUtc",
                table: "HallAttendances",
                newName: "Enter");

            migrationBuilder.RenameIndex(
                name: "IX_HallAttendances_HallId_LeaveUtc",
                table: "HallAttendances",
                newName: "IX_HallAttendances_HallId_Leave");

            migrationBuilder.RenameColumn(
                name: "StartUtc",
                table: "HallAllocations",
                newName: "Start");

            migrationBuilder.RenameColumn(
                name: "EndUtc",
                table: "HallAllocations",
                newName: "End");

            migrationBuilder.RenameColumn(
                name: "ScannedAtUtc",
                table: "GateScans",
                newName: "ScannedAt");

            migrationBuilder.RenameColumn(
                name: "ClientScannedAtUtc",
                table: "GateScans",
                newName: "ClientScannedAt");

            migrationBuilder.RenameColumn(
                name: "SlotStartUtc",
                table: "DelegationMeetingRequests",
                newName: "SlotStart");

            migrationBuilder.RenameColumn(
                name: "SlotEndUtc",
                table: "DelegationMeetingRequests",
                newName: "SlotEnd");

            migrationBuilder.RenameColumn(
                name: "ReminderSentUtc",
                table: "DelegationMeetingRequests",
                newName: "ReminderSent");

            migrationBuilder.RenameColumn(
                name: "StartUtc",
                table: "DelegationAvailabilityWindows",
                newName: "Start");

            migrationBuilder.RenameColumn(
                name: "EndUtc",
                table: "DelegationAvailabilityWindows",
                newName: "End");

            migrationBuilder.RenameIndex(
                name: "IX_DelegationAvailabilityWindows_CountryId_StartUtc",
                table: "DelegationAvailabilityWindows",
                newName: "IX_DelegationAvailabilityWindows_CountryId_Start");

            migrationBuilder.RenameIndex(
                name: "IX_DelegationAvailabilityWindows_CountryId_IsActive_StartUtc",
                table: "DelegationAvailabilityWindows",
                newName: "IX_DelegationAvailabilityWindows_CountryId_IsActive_Start");

            migrationBuilder.RenameColumn(
                name: "HandledAtUtc",
                table: "ContactInquiries",
                newName: "HandledAt");

            migrationBuilder.RenameColumn(
                name: "StartUtc",
                table: "BusinessMeetings",
                newName: "Start");

            migrationBuilder.RenameColumn(
                name: "EndUtc",
                table: "BusinessMeetings",
                newName: "End");

            migrationBuilder.RenameIndex(
                name: "IX_BusinessMeetings_Status_StartUtc",
                table: "BusinessMeetings",
                newName: "IX_BusinessMeetings_Status_Start");

            migrationBuilder.RenameColumn(
                name: "StartUtc",
                table: "Banners",
                newName: "Start");

            migrationBuilder.RenameColumn(
                name: "EndUtc",
                table: "Banners",
                newName: "End");

            migrationBuilder.RenameIndex(
                name: "IX_Banners_IsActive_StartUtc_EndUtc_DisplayOrder",
                table: "Banners",
                newName: "IX_Banners_IsActive_Start_End_DisplayOrder");

            migrationBuilder.CreateIndex(
                name: "IX_StoredFiles_RetainUntil",
                table: "StoredFiles",
                column: "RetainUntil",
                filter: "[IsActive] = 1 AND [RetainUntil] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SpeakerMeetingRequests_HallId_SlotStart",
                table: "SpeakerMeetingRequests",
                columns: new[] { "HallId", "SlotStart" },
                unique: true,
                filter: "[HallId] IS NOT NULL AND [SlotStart] IS NOT NULL AND [Status] <> 0 AND [Status] <> 2 AND [Status] <> 3");

            migrationBuilder.CreateIndex(
                name: "IX_SpeakerMeetingRequests_SpeakerId_SlotStart",
                table: "SpeakerMeetingRequests",
                columns: new[] { "SpeakerId", "SlotStart" },
                unique: true,
                filter: "[SlotStart] IS NOT NULL AND [Status] <> 0 AND [Status] <> 2 AND [Status] <> 3");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SpeakerMeetingRequests_Slot",
                table: "SpeakerMeetingRequests",
                sql: "[SlotStart] IS NULL OR [SlotEnd] IS NULL OR [SlotEnd] > [SlotStart]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SpeakerAvailabilityWindows_TimeWindow",
                table: "SpeakerAvailabilityWindows",
                sql: "[End] > [Start]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Sessions_TimeWindow",
                table: "Sessions",
                sql: "[End] > [Start]");

            migrationBuilder.CreateIndex(
                name: "IX_SeatReservations_Expires",
                table: "SeatReservations",
                column: "Expires",
                filter: "[ReleasedAt] IS NULL AND [Expires] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_HallAvailabilityWindows_TimeWindow",
                table: "HallAvailabilityWindows",
                sql: "[End] > [Start]");

            migrationBuilder.CreateIndex(
                name: "IX_HallAttendances_SessionId_UserId",
                table: "HallAttendances",
                columns: new[] { "SessionId", "UserId" },
                unique: true,
                filter: "[Leave] IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_HallAllocations_TimeWindow",
                table: "HallAllocations",
                sql: "[End] > [Start]");

            migrationBuilder.CreateIndex(
                name: "IX_DelegationMeetingRequests_HallId_SlotStart",
                table: "DelegationMeetingRequests",
                columns: new[] { "HallId", "SlotStart" },
                unique: true,
                filter: "[HallId] IS NOT NULL AND [SlotStart] IS NOT NULL AND [Status] <> 0 AND [Status] <> 2 AND [Status] <> 3");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DelegationMeetingRequests_Slot",
                table: "DelegationMeetingRequests",
                sql: "[SlotStart] IS NULL OR [SlotEnd] IS NULL OR [SlotEnd] > [SlotStart]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DelegationAvailabilityWindows_TimeWindow",
                table: "DelegationAvailabilityWindows",
                sql: "[End] > [Start]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BusinessMeetings_TimeWindow",
                table: "BusinessMeetings",
                sql: "[End] > [Start]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Banners_TimeWindow",
                table: "Banners",
                sql: "[End] > [Start]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StoredFiles_RetainUntil",
                table: "StoredFiles");

            migrationBuilder.DropIndex(
                name: "IX_SpeakerMeetingRequests_HallId_SlotStart",
                table: "SpeakerMeetingRequests");

            migrationBuilder.DropIndex(
                name: "IX_SpeakerMeetingRequests_SpeakerId_SlotStart",
                table: "SpeakerMeetingRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SpeakerMeetingRequests_Slot",
                table: "SpeakerMeetingRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SpeakerAvailabilityWindows_TimeWindow",
                table: "SpeakerAvailabilityWindows");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Sessions_TimeWindow",
                table: "Sessions");

            migrationBuilder.DropIndex(
                name: "IX_SeatReservations_Expires",
                table: "SeatReservations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_HallAvailabilityWindows_TimeWindow",
                table: "HallAvailabilityWindows");

            migrationBuilder.DropIndex(
                name: "IX_HallAttendances_SessionId_UserId",
                table: "HallAttendances");

            migrationBuilder.DropCheckConstraint(
                name: "CK_HallAllocations_TimeWindow",
                table: "HallAllocations");

            migrationBuilder.DropIndex(
                name: "IX_DelegationMeetingRequests_HallId_SlotStart",
                table: "DelegationMeetingRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DelegationMeetingRequests_Slot",
                table: "DelegationMeetingRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DelegationAvailabilityWindows_TimeWindow",
                table: "DelegationAvailabilityWindows");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BusinessMeetings_TimeWindow",
                table: "BusinessMeetings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Banners_TimeWindow",
                table: "Banners");

            migrationBuilder.RenameColumn(
                name: "SecureDestroyed",
                table: "StoredFiles",
                newName: "SecureDestroyedUtc");

            migrationBuilder.RenameColumn(
                name: "RetainUntil",
                table: "StoredFiles",
                newName: "RetainUntilUtc");

            migrationBuilder.RenameColumn(
                name: "SlotStart",
                table: "SpeakerMeetingRequests",
                newName: "SlotStartUtc");

            migrationBuilder.RenameColumn(
                name: "SlotEnd",
                table: "SpeakerMeetingRequests",
                newName: "SlotEndUtc");

            migrationBuilder.RenameColumn(
                name: "ReminderSent",
                table: "SpeakerMeetingRequests",
                newName: "ReminderSentUtc");

            migrationBuilder.RenameColumn(
                name: "Start",
                table: "SpeakerAvailabilityWindows",
                newName: "StartUtc");

            migrationBuilder.RenameColumn(
                name: "End",
                table: "SpeakerAvailabilityWindows",
                newName: "EndUtc");

            migrationBuilder.RenameIndex(
                name: "IX_SpeakerAvailabilityWindows_SpeakerId_Start",
                table: "SpeakerAvailabilityWindows",
                newName: "IX_SpeakerAvailabilityWindows_SpeakerId_StartUtc");

            migrationBuilder.RenameIndex(
                name: "IX_SpeakerAvailabilityWindows_SpeakerId_IsActive_Start",
                table: "SpeakerAvailabilityWindows",
                newName: "IX_SpeakerAvailabilityWindows_SpeakerId_IsActive_StartUtc");

            migrationBuilder.RenameColumn(
                name: "Start",
                table: "Sessions",
                newName: "StartUtc");

            migrationBuilder.RenameColumn(
                name: "ReminderSent",
                table: "Sessions",
                newName: "ReminderSentUtc");

            migrationBuilder.RenameColumn(
                name: "RatingPromptSent",
                table: "Sessions",
                newName: "RatingPromptSentUtc");

            migrationBuilder.RenameColumn(
                name: "End",
                table: "Sessions",
                newName: "EndUtc");

            migrationBuilder.RenameIndex(
                name: "IX_Sessions_Status_Start",
                table: "Sessions",
                newName: "IX_Sessions_Status_StartUtc");

            migrationBuilder.RenameIndex(
                name: "IX_Sessions_IsActive_Start",
                table: "Sessions",
                newName: "IX_Sessions_IsActive_StartUtc");

            migrationBuilder.RenameIndex(
                name: "IX_Sessions_HallId_Start",
                table: "Sessions",
                newName: "IX_Sessions_HallId_StartUtc");

            migrationBuilder.RenameColumn(
                name: "Expires",
                table: "SeatReservations",
                newName: "ExpiresUtc");

            migrationBuilder.RenameColumn(
                name: "AutoClose",
                table: "RegistrationGate",
                newName: "AutoCloseUtc");

            migrationBuilder.RenameColumn(
                name: "RatingPromptSent",
                table: "ProgrammeDays",
                newName: "RatingPromptSentUtc");

            migrationBuilder.RenameColumn(
                name: "Timestamp",
                table: "OperationLog",
                newName: "TimestampUtc");

            migrationBuilder.RenameIndex(
                name: "IX_OperationLog_Timestamp",
                table: "OperationLog",
                newName: "IX_OperationLog_TimestampUtc");

            migrationBuilder.RenameIndex(
                name: "IX_OperationLog_EventType_Timestamp",
                table: "OperationLog",
                newName: "IX_OperationLog_EventType_TimestampUtc");

            migrationBuilder.RenameIndex(
                name: "IX_OperationLog_ActorUserId_Timestamp",
                table: "OperationLog",
                newName: "IX_OperationLog_ActorUserId_TimestampUtc");

            migrationBuilder.RenameColumn(
                name: "Expires",
                table: "MeetingActionTokens",
                newName: "ExpiresUtc");

            migrationBuilder.RenameColumn(
                name: "Start",
                table: "HallAvailabilityWindows",
                newName: "StartUtc");

            migrationBuilder.RenameColumn(
                name: "End",
                table: "HallAvailabilityWindows",
                newName: "EndUtc");

            migrationBuilder.RenameIndex(
                name: "IX_HallAvailabilityWindows_HallId_Start",
                table: "HallAvailabilityWindows",
                newName: "IX_HallAvailabilityWindows_HallId_StartUtc");

            migrationBuilder.RenameIndex(
                name: "IX_HallAvailabilityWindows_HallId_IsActive_Start",
                table: "HallAvailabilityWindows",
                newName: "IX_HallAvailabilityWindows_HallId_IsActive_StartUtc");

            migrationBuilder.RenameColumn(
                name: "Leave",
                table: "HallAttendances",
                newName: "LeaveUtc");

            migrationBuilder.RenameColumn(
                name: "Enter",
                table: "HallAttendances",
                newName: "EnterUtc");

            migrationBuilder.RenameIndex(
                name: "IX_HallAttendances_HallId_Leave",
                table: "HallAttendances",
                newName: "IX_HallAttendances_HallId_LeaveUtc");

            migrationBuilder.RenameColumn(
                name: "Start",
                table: "HallAllocations",
                newName: "StartUtc");

            migrationBuilder.RenameColumn(
                name: "End",
                table: "HallAllocations",
                newName: "EndUtc");

            migrationBuilder.RenameColumn(
                name: "ScannedAt",
                table: "GateScans",
                newName: "ScannedAtUtc");

            migrationBuilder.RenameColumn(
                name: "ClientScannedAt",
                table: "GateScans",
                newName: "ClientScannedAtUtc");

            migrationBuilder.RenameColumn(
                name: "SlotStart",
                table: "DelegationMeetingRequests",
                newName: "SlotStartUtc");

            migrationBuilder.RenameColumn(
                name: "SlotEnd",
                table: "DelegationMeetingRequests",
                newName: "SlotEndUtc");

            migrationBuilder.RenameColumn(
                name: "ReminderSent",
                table: "DelegationMeetingRequests",
                newName: "ReminderSentUtc");

            migrationBuilder.RenameColumn(
                name: "Start",
                table: "DelegationAvailabilityWindows",
                newName: "StartUtc");

            migrationBuilder.RenameColumn(
                name: "End",
                table: "DelegationAvailabilityWindows",
                newName: "EndUtc");

            migrationBuilder.RenameIndex(
                name: "IX_DelegationAvailabilityWindows_CountryId_Start",
                table: "DelegationAvailabilityWindows",
                newName: "IX_DelegationAvailabilityWindows_CountryId_StartUtc");

            migrationBuilder.RenameIndex(
                name: "IX_DelegationAvailabilityWindows_CountryId_IsActive_Start",
                table: "DelegationAvailabilityWindows",
                newName: "IX_DelegationAvailabilityWindows_CountryId_IsActive_StartUtc");

            migrationBuilder.RenameColumn(
                name: "HandledAt",
                table: "ContactInquiries",
                newName: "HandledAtUtc");

            migrationBuilder.RenameColumn(
                name: "Start",
                table: "BusinessMeetings",
                newName: "StartUtc");

            migrationBuilder.RenameColumn(
                name: "End",
                table: "BusinessMeetings",
                newName: "EndUtc");

            migrationBuilder.RenameIndex(
                name: "IX_BusinessMeetings_Status_Start",
                table: "BusinessMeetings",
                newName: "IX_BusinessMeetings_Status_StartUtc");

            migrationBuilder.RenameColumn(
                name: "Start",
                table: "Banners",
                newName: "StartUtc");

            migrationBuilder.RenameColumn(
                name: "End",
                table: "Banners",
                newName: "EndUtc");

            migrationBuilder.RenameIndex(
                name: "IX_Banners_IsActive_Start_End_DisplayOrder",
                table: "Banners",
                newName: "IX_Banners_IsActive_StartUtc_EndUtc_DisplayOrder");

            migrationBuilder.CreateIndex(
                name: "IX_StoredFiles_RetainUntilUtc",
                table: "StoredFiles",
                column: "RetainUntilUtc",
                filter: "[IsActive] = 1 AND [RetainUntilUtc] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SpeakerMeetingRequests_HallId_SlotStartUtc",
                table: "SpeakerMeetingRequests",
                columns: new[] { "HallId", "SlotStartUtc" },
                unique: true,
                filter: "[HallId] IS NOT NULL AND [SlotStartUtc] IS NOT NULL AND [Status] <> 0 AND [Status] <> 2 AND [Status] <> 3");

            migrationBuilder.CreateIndex(
                name: "IX_SpeakerMeetingRequests_SpeakerId_SlotStartUtc",
                table: "SpeakerMeetingRequests",
                columns: new[] { "SpeakerId", "SlotStartUtc" },
                unique: true,
                filter: "[SlotStartUtc] IS NOT NULL AND [Status] <> 0 AND [Status] <> 2 AND [Status] <> 3");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SpeakerMeetingRequests_Slot",
                table: "SpeakerMeetingRequests",
                sql: "[SlotStartUtc] IS NULL OR [SlotEndUtc] IS NULL OR [SlotEndUtc] > [SlotStartUtc]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SpeakerAvailabilityWindows_TimeWindow",
                table: "SpeakerAvailabilityWindows",
                sql: "[EndUtc] > [StartUtc]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Sessions_TimeWindow",
                table: "Sessions",
                sql: "[EndUtc] > [StartUtc]");

            migrationBuilder.CreateIndex(
                name: "IX_SeatReservations_ExpiresUtc",
                table: "SeatReservations",
                column: "ExpiresUtc",
                filter: "[ReleasedAt] IS NULL AND [ExpiresUtc] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_HallAvailabilityWindows_TimeWindow",
                table: "HallAvailabilityWindows",
                sql: "[EndUtc] > [StartUtc]");

            migrationBuilder.CreateIndex(
                name: "IX_HallAttendances_SessionId_UserId",
                table: "HallAttendances",
                columns: new[] { "SessionId", "UserId" },
                unique: true,
                filter: "[LeaveUtc] IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_HallAllocations_TimeWindow",
                table: "HallAllocations",
                sql: "[EndUtc] > [StartUtc]");

            migrationBuilder.CreateIndex(
                name: "IX_DelegationMeetingRequests_HallId_SlotStartUtc",
                table: "DelegationMeetingRequests",
                columns: new[] { "HallId", "SlotStartUtc" },
                unique: true,
                filter: "[HallId] IS NOT NULL AND [SlotStartUtc] IS NOT NULL AND [Status] <> 0 AND [Status] <> 2 AND [Status] <> 3");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DelegationMeetingRequests_Slot",
                table: "DelegationMeetingRequests",
                sql: "[SlotStartUtc] IS NULL OR [SlotEndUtc] IS NULL OR [SlotEndUtc] > [SlotStartUtc]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DelegationAvailabilityWindows_TimeWindow",
                table: "DelegationAvailabilityWindows",
                sql: "[EndUtc] > [StartUtc]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BusinessMeetings_TimeWindow",
                table: "BusinessMeetings",
                sql: "[EndUtc] > [StartUtc]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Banners_TimeWindow",
                table: "Banners",
                sql: "[EndUtc] > [StartUtc]");
        }
    }
}
