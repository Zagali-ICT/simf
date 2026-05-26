using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMF.Infrastructure.Persistence.Migrations.Identity
{
    /// <inheritdoc />
    public partial class MoveQrAndRejectionToUserProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // D-106: move QrId + RejectionReason + RejectionReasonArabic from
            // AspNetUsers to UserProfiles. The hand-written ordering preserves
            // existing data:
            //   1. ADD the three columns to UserProfiles (nullable).
            //   2. INSERT a stub UserProfile for any AspNetUsers row that has
            //      a non-null QrId / RejectionReason and does NOT already have
            //      a UserProfile row — otherwise the data is lost on the COPY.
            //   3. COPY the column values across via UPDATE … FROM joins.
            //   4. DROP the AspNetUsers columns and their index.
            //   5. CREATE the unique filtered index on UserProfiles.QrId now
            //      that the data is in.

            migrationBuilder.AddColumn<string>(
                name: "QrId",
                table: "UserProfiles",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "UserProfiles",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReasonArabic",
                table: "UserProfiles",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            // Insert a stub UserProfile for any user that carries a non-null
            // QrId / RejectionReason / RejectionReasonArabic but does not yet
            // have a profile row (admins approved before D-106 had no
            // profile; visitors approved without filling the form same).
            migrationBuilder.Sql(@"
                INSERT INTO [UserProfiles]
                    ([Id], [UserId], [ArabicName], [EnglishName], [NationalityCode],
                     [PlaceOfBirth], [IsSaudi], [CreatedAt])
                SELECT
                    NEWID(), u.[Id], '', '', '', '', 0, SYSUTCDATETIME()
                FROM [AspNetUsers] u
                WHERE NOT EXISTS (
                    SELECT 1 FROM [UserProfiles] p WHERE p.[UserId] = u.[Id]
                )
                AND (
                    u.[QrId] IS NOT NULL
                    OR u.[RejectionReason] IS NOT NULL
                    OR u.[RejectionReasonArabic] IS NOT NULL
                );");

            migrationBuilder.Sql(@"
                UPDATE p
                SET p.[QrId] = u.[QrId],
                    p.[RejectionReason] = u.[RejectionReason],
                    p.[RejectionReasonArabic] = u.[RejectionReasonArabic]
                FROM [UserProfiles] p
                INNER JOIN [AspNetUsers] u ON u.[Id] = p.[UserId];");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_QrId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "QrId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "RejectionReasonArabic",
                table: "AspNetUsers");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_QrId",
                table: "UserProfiles",
                column: "QrId",
                unique: true,
                filter: "[QrId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "QrId",
                table: "AspNetUsers",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "AspNetUsers",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReasonArabic",
                table: "AspNetUsers",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE u
                SET u.[QrId] = p.[QrId],
                    u.[RejectionReason] = p.[RejectionReason],
                    u.[RejectionReasonArabic] = p.[RejectionReasonArabic]
                FROM [AspNetUsers] u
                INNER JOIN [UserProfiles] p ON p.[UserId] = u.[Id];");

            migrationBuilder.DropIndex(
                name: "IX_UserProfiles_QrId",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "QrId",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "RejectionReasonArabic",
                table: "UserProfiles");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_QrId",
                table: "AspNetUsers",
                column: "QrId",
                unique: true,
                filter: "[QrId] IS NOT NULL");
        }
    }
}
