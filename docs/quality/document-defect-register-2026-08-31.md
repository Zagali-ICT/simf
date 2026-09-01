# Document defect register

SIMF-HLD-004 v1.4, SIMF-LLD-003 v1.5, SIMF-BRD-001-EN v1.3. Compiled 2026-08-31.

## What this is, and how far to trust it

A cover-to-cover read of the three English documents by twenty-two independent readers, each told to settle any claim about the build against the source tree rather than against another document. The readers were given no prior findings list, so this is not a re-check of known issues.

**The read was cut short.** It hit the weekly agent quota with sixteen of thirty-eight agents finished. Every refuter died, so **nothing below has been attacked by a second reader**. A sample of six was verified by hand and five held. Treat the list as roughly right rather than proven, and note that every entry names the file it was checked against, so any one of them settles in a single command.

The forty-three **blocking** findings from the same read are not listed here. Each was verified by hand and fixed in `aa3d7d6f1`.

The Arabic BRD is out of scope, by owner instruction of 2026-08-31.

## Counts

| Cluster | Findings |
|---|---|
| 1. LLD data dictionary vs the real schema | 26 |
| 2. Claims the code disproves | 46 |
| 3. Internal contradictions | 14 |
| 4. Undefined terms, and sentences that read two ways | 21 |
| 5. Repetition and filler | 36 |
| 6. Features named but not described | 3 |
| **Total** | **146** |

By document: LLD 83, HLD 52, BRD 11. By severity: serious 97, minor 49.

## Where to start

Clusters 1 and 2 are what a reviewer can disprove from the repository, and they are the ones that cost credibility in a meeting. Clusters 4 and 5 are readability, and no reviewer can prove them wrong. If time is short, fix 1 and 2 and leave the rest.

---

## 1. LLD data dictionary vs the real schema

The largest cluster, and the fastest to settle, because the schema is one file per context. Both EF histories were regenerated (D-881, D-924, D-926, D-929) and this section of the LLD was not regenerated with them. **Generate the dictionary from `00000000000000_InitialCreate.cs` rather than editing these rows by hand**, or the next schema change reopens every one of them.

### 1. LLD paragraph 1493  <sub>serious</sub>

> 1 to 0..1

The 1 on the User side says every attendee profile belongs to a user account. The build creates profiles that have none. `src/Backend/SIMF.Infrastructure/Persistence/Configurations/App/UserProfileConfiguration.cs` makes `UserId` nullable with a filtered unique index and explains why: "An attendee need not have an account at all, so this column is nullable and most rows at a walk-in desk will be null. SQL Server treats NULLs as EQUAL in a unique index, so an unfiltered one would admit exactly ONE such row system-wide." `AdminAccountService.Bulk.cs` line 1289 writes `UserId = null` for bulk badge rows, and `AccessControl/QrResolver.cs` line 72 reads "Most holders at a gate have no account." The correct cardinality is 0..1 to 0..1. As written, the data model has no way to represent the badge holders who make up the majority of gate traffic, which is the first question a reviewer asks of an entry-control system.

**Fix.** "user profile; the cardinality is 0..1 to 0..1, because a badge holder with no account has a profile row and no user row"

### 2. LLD paragraph 1634  <sub>serious</sub>

> Visitor=0, (1 reserved), Admin=2

The column does not hold those integers. `src/Backend/SIMF.Infrastructure/Persistence/Configurations/SimfUserConfiguration.cs` applies `.HasConversion<string>().HasMaxLength(16)` to `UserType` ("UserType, stored as a string for readability in SQL diagnostics") and `.HasConversion<string>().HasMaxLength(32)` to `AccountState`, and the Identity InitialCreate creates `UserType = nvarchar(16)` and `AccountState = nvarchar(32)`. Both rows are typed `enum(int)`, which paragraph 295 defines as "integer-backed", so a reader who queries this table for `UserType = 2` gets nothing and the values 0 and 2 appear nowhere in the data. The C# enum values are correct; the claim about the column is not.

**Fix.** "Visitor or Admin, stored as the enum name in `nvarchar(16)`. The integer 1 is reserved"

### 3. LLD paragraph 1640  <sub>serious</sub>

> PasswordChangedAtUtc

Neither column exists under the name given. The Identity `00000000000000_InitialCreate.cs` creates `PasswordChangedAt` and `LastSuccessfulSignInAt` on the user table, and `SimfUser.cs` declares them with those names. The `Utc` suffix also asserts a storage convention this build does not use: `SimfUser.cs` states "Saudi local time, as is every other timestamp on this row", `src/Shared/SIMF.Common/SimfClock.cs` states "SIMF stores and works in Saudi local wall-clock time only, plain DateTime, no DateTimeOffset, no a zoned value anywhere in the database or on the wire", and `TokenIssuer.cs` writes the sign-in stamp from that clock (`var now = timeProvider.SimfNow();`). Do not generalise the rename: `Session.ReminderSentUtc` at 1900 is a real column. The two `datetime(UTC)` type cells in this row pair, the convention line at 295 ("`datetime(UTC)` = UTC timestamp") and the sentence at 244 ("Timestamps are stored in UTC") carry the same stale claim; the type cells are not uniquely anchorable and the two paragraphs are outside this slice.

**Fix.** "PasswordChangedAt"

### 4. LLD paragraph 1650  <sub>serious</sub>

> AvatarRelativePath

There is no such column. `src/Backend/SIMF.Infrastructure/Persistence/Migrations/Identity/00000000000000_InitialCreate.cs` creates the user row with `AvatarFileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)`, and `src/Backend/SIMF.Domain/IdentityAccess/SimfUser.cs` declares `public Guid? AvatarFileId`, with the note "This was `AvatarRelativePath`, a string, until the column was found still advertising a path it had not held since the file store was unified." The row therefore gets the name, the type and the meaning wrong: the value is an id into `StoredFiles` in the App database, held as a bare Guid because SQL Server cannot carry a foreign key across the database boundary, and the avatar bytes are resolved by owner instead. No `*RelativePath` column survives anywhere in the domain, so a reader cannot find this one to check it.

**Fix.** "AvatarFileId"

### 5. LLD paragraph 1674  <sub>serious</sub>

> Page the action belongs to

The `Permissions` table has exactly two columns. `src/Backend/SIMF.Infrastructure/Persistence/Migrations/Identity/00000000000000_InitialCreate.cs` lines 18 to 27 create it with `Id` and `Code` (`nvarchar(150)`) and nothing else. `src/Backend/SIMF.Domain/IdentityAccess/Permission.cs` declares only `Id`, `Code` and the `RolePermissions` collection, and its comment says `Code` is "The only persisted field of a permission: the page, action and display name a catalogue entry also carries are presentation metadata that the Control Panel reads straight off the in-process PermissionCatalog". The table therefore invents three columns. It also contradicts the same document at line 290, which states "`Permission` is `(Page, Action, Code)`".

**Fix.** "Permission code, in the format `Page.Action`. With `Id` this is the whole table; the page, action and display name are read from `PermissionCatalog` in process and are not stored."

### 6. LLD paragraph 1751  <sub>minor</sub>

> enum(int)

Three things in the `AccountCodes` rows are wrong. First, `Purpose` is not stored as an integer: `src/Backend/SIMF.Infrastructure/Persistence/Configurations/AccountCodeConfiguration.cs` applies `.HasConversion<string>().HasMaxLength(32)`, and the Identity migration line 158 declares `Purpose = table.Column<string>(type: "nvarchar(32)", ...)`, so it holds the value name. Second, the value list omits `EmailChangeVerification`, which is the sixth value of `AccountCodePurpose` in `src/Shared/SIMF.Common/Enums/AccountCodePurpose.cs`. Third, the `UserId` row above says Null "No", but the Identity migration line 155 declares it `nullable: true` beside a second owner column `UserProfileId`, with `CK_AccountCodes_OneOwner` requiring exactly one of the two; a badge activation code is issued against a profile that has no account at all.

**Fix.** "EmailVerification, PasswordReset, SignInOtp, BadgeActivationOtp, BiometricEnrolStepUp, EmailChangeVerification. Persisted as the value name in `nvarchar(32)`, not as an integer. The code belongs either to a `UserId` or to a `UserProfileId`, never both and never neither (`CK_AccountCodes_OneOwner`); BadgeActivationOtp is the profile-owned case, issued before the holder has an account."

### 7. LLD paragraph 1788  <sub>serious</sub>

> Unique; logical FK to AspNetUsers

`UserProfile.UserId` is nullable and its unique index is filtered. `src/Backend/SIMF.Infrastructure/Persistence/Migrations/App/00000000000000_InitialCreate.cs` line 1321 declares `UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)`, and line 4051 creates `IX_UserProfiles_UserId` as `unique: true, filter: "[UserId] IS NOT NULL"`. The row says Null "No" and a plain unique index. The distinction is load-bearing: `src/Backend/SIMF.Domain/Profiles/UserProfile.cs` records that an unfiltered unique index "would permit exactly ONE profile without an account across the whole system", and walk-in attendees and bulk-minted badges are profiles with no account. The referenced table is also named `Users`, not `AspNetUsers` (Identity migration line 69).

**Fix.** "Filtered unique index over non-null rows; logical link to the Identity `Users` table"

### 8. LLD paragraph 1800  <sub>serious</sub>

> NationalId / IqamaNumber / PassportNumber

These three columns are not on `UserProfiles`. The App migration `src/Backend/SIMF.Infrastructure/Persistence/Migrations/App/00000000000000_InitialCreate.cs` lines 1320 to 1369 list every column of that table and none of the three appears. Identity numbers live one row per document on the child table `ProfileIdentityDocuments`, as `src/Backend/SIMF.Domain/Profiles/UserProfile.cs` states: the child table is "the ONLY place those numbers live", and it replaced the per-number columns because an attendee can hold more than one document at once.

**Fix.** "Number (on child table `ProfileIdentityDocuments`)"

### 9. LLD paragraph 1813  <sub>serious</sub>

> logical FK to Organisation

Four rows in this section call a real database foreign key a "logical" one, against the definition this same document gives at line 286: "Any App to Identity reference is a logical FK, a bare `Guid` enforced in application code, never a DB constraint". All four are enforced by the database. `src/Backend/SIMF.Infrastructure/Persistence/Migrations/App/00000000000000_InitialCreate.cs` creates `FK_UserProfiles_Organisations_OrganisationId` (line 1382), `FK_UserProfiles_ProfileTypes_ProfileTypeId` (line 1388), `FK_GateScans_Gates_GateId` (line 1868) and `FK_GateScans_UserProfiles_UserProfileId` (line 1874). `src/Backend/SIMF.Domain/Profiles/UserProfile.cs` says of `OrganisationId`: "Unlike `NationalityId` this IS a real database foreign key (`OnDelete.Restrict`)". Both sides of each of the four references sit in `SIMF_App`, so no cross-database rule applies. As written, a reviewer auditing referential integrity would conclude four constraints are missing.

**Fix.** "FK to Organisation"

### 10. LLD paragraph 1829  <sub>serious</sub>

> Badge QR identifier

`QrId` and `ReferenceNumber` are both nullable, and both unique indexes are filtered. `src/Backend/SIMF.Infrastructure/Persistence/Migrations/App/00000000000000_InitialCreate.cs` line 1354 declares `QrId = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true)` and line 1341 `ReferenceNumber = ... nullable: true`; lines 4032 and 4039 create both indexes with `filter: "[QrId] IS NOT NULL"` and `filter: "[ReferenceNumber] IS NOT NULL"`. Both rows say Null "No". For `QrId` this contradicts the approval flow the documents describe: `src/Backend/SIMF.Domain/Profiles/UserProfile.cs` states the QR id is "Minted by `IQrIdMinter` the moment `AdmissionState` reaches `Approved`, so it stays null until the attendee is approved". A table saying every profile carries a QR id says a badge exists before approval.

**Fix.** "Badge QR identifier. Null until the attendee is approved; minted when `AdmissionState` reaches Approved. Filtered unique index over non-null rows."

### 11. LLD paragraph 1880  <sub>serious</sub>

> LiveStreamUrl

Neither live-feed column is a URL string. `src/Backend/SIMF.Infrastructure/Persistence/Migrations/App/00000000000000_InitialCreate.cs` lines 1263 to 1264 declare `LiveStreamFileId` and `LiveSignLanguageFileId` as `uniqueidentifier`, both foreign keys into `StoredFiles`. `src/Backend/SIMF.Domain/Programme/Session.cs` describes `LiveStreamFileId` as "An external link held as a `StoredFiles` row, which gives the feed a media type, a policy and an owner". The table names two string columns that do not exist and hides the fact that the feed is registered in the file store.

**Fix.** "LiveStreamFileId"

### 12. LLD paragraph 1890  <sub>serious</sub>

> LiveCaptions (Arabic)

The customer's technology team asked about Arabic transcription, and this row answers it wrongly. There are two columns, `LiveCaptions` and `LiveCaptionsArabic` (`src/Backend/SIMF.Infrastructure/Persistence/Migrations/App/00000000000000_InitialCreate.cs` lines 1265 to 1266), not one Arabic column. Nothing generates either: `src/Backend/SIMF.Domain/Programme/Session.cs` says of `LiveCaptions` "Typed by an admin, not generated: there is no speech-to-text integration." A row headed "LiveCaptions (Arabic)" and described only as "Live captions" reads as live Arabic captioning, which the same document denies at line 80: "SIMF therefore produces no live Arabic transcription and no live translation, and claims none."

**Fix.** "LiveCaptions / LiveCaptionsArabic"

### 13. LLD paragraph 1895  <sub>serious</sub>

> (recording columns)

There are no recording metadata columns on `Sessions`. The App migration `src/Backend/SIMF.Infrastructure/Persistence/Migrations/App/00000000000000_InitialCreate.cs` lines 1242 to 1274 list every column and the only recording one is `RecordingFileId` (line 1262). `src/Backend/SIMF.Domain/Programme/Session.cs` records the removal: the recording's "name, media type, size and uploader" were "columns here until they were removed: the store had the same four, so every upload wrote each fact twice and nothing kept the pairs equal afterwards." The row advertises a group of columns that were deleted precisely because they duplicated the file store.

**Fix.** "RecordingFileId"

### 14. LLD paragraph 1920  <sub>serious</sub>

> (SeatId)

There is no `Seat` table and no `SeatId` column. `src/Backend/SIMF.Infrastructure/Persistence/Migrations/App/00000000000000_InitialCreate.cs` lines 1969 to 2006 create `SeatReservations` with `RowLabel` (`nvarchar(8)`) and `SeatNumber` (`int`) and two foreign keys, to `Sessions` and to `UserProfiles`; there is no foreign key to any seat table, and no `Seats` table is created anywhere in the migration. `src/Backend/SIMF.Domain/SeatReservations/SeatReservation.cs` carries `RowLabel` and `SeatNumber` and no seat reference. A reviewer would look for a seat master table that the design does not have.

**Fix.** "RowLabel / SeatNumber"

### 15. LLD paragraph 1929  <sub>minor</sub>

> UserBooking/RandomAssignment/AdminReservedRow/OpenSeating

Every other integer enum listed in this section is written in value order, so a reader takes this list as 0 to 3. It is not. `src/Shared/SIMF.Common/Enums/SeatReservationKind.cs` declares `UserBooking = 0, AdminReservedRow = 1, RandomAssignment = 2, OpenSeating = 3`, so the middle two are swapped here. The value matters on its own: `CK_SeatReservations_AdminBlockHasNoHolder` in `src/Backend/SIMF.Infrastructure/Persistence/Migrations/App/00000000000000_InitialCreate.cs` line 1990 is written as `[Kind] = 1`, which is AdminReservedRow.

**Fix.** "UserBooking=0, AdminReservedRow=1, RandomAssignment=2, OpenSeating=3"

### 16. LLD paragraph 1934  <sub>serious</sub>

> Pending/Approved/Rejected

The booking approval workflow this row and the row below it describe was removed and is not in the schema. `BookingStatus` in `src/Shared/SIMF.Common/Enums/BookingStatus.cs` has four values, and its comment states that the owner removed the approval queue on 2026-07-18: `Approved` is "written by EVERY create path", `Cancelled` is written on release, and `Pending` and `Rejected` each have "NO production writer". The table therefore names the two dead values and omits the live one. The review-columns row is worse: `src/Backend/SIMF.Infrastructure/Persistence/Migrations/App/00000000000000_InitialCreate.cs` lines 1969 to 1986 list every column of `SeatReservations` and there is no `ReviewedByUserId`, no `ReviewedAt` and no reason column.

**Fix.** "Approved on every create path; Cancelled on release. Pending and Rejected are reserved and no code writes them."

### 17. LLD paragraph 1966  <sub>minor</sub>

> string(12)

`QrIdAtScan` is `nvarchar(96)`, not 12 characters. `src/Backend/SIMF.Infrastructure/Persistence/Migrations/App/00000000000000_InitialCreate.cs` line 1845 declares `QrIdAtScan = table.Column<string>(type: "nvarchar(96)", maxLength: 96, nullable: false)`. The width is deliberate: `src/Backend/SIMF.Domain/AccessControl/GateScan.cs` says the column holds "What the scanner physically presented", which is not always a bare 12-character QR id. Twelve is the length of `UserProfile.QrId`, which is a different column in a different table.

**Fix.** "string(96)"

### 18. LLD paragraph 1980  <sub>minor</sub>

> DenialReasonCode

`DenialReasonCode` is typed `string` in this row and is an integer in the schema. `src/Backend/SIMF.Infrastructure/Persistence/Migrations/App/00000000000000_InitialCreate.cs` line 1850 declares `DenialReasonCode = table.Column<int>(type: "int", nullable: true)`, and line 1864 constrains it with `CK_GateScans_DenialReasonRange` (`[DenialReasonCode] IS NULL OR [DenialReasonCode] BETWEEN 0 AND 8`). `src/Backend/SIMF.Domain/AccessControl/GateScan.cs` declares it as the nullable enum `DenialReasonCode?`. Line 1863 also pins it to the outcome (`CK_GateScans_DenialPin`), so it is not free text.

**Fix.** "Denial reason, an integer enum in the range 0 to 8. `CK_GateScans_DenialPin` requires it on a denial and forbids it on an allow."

### 19. LLD paragraph 2038  <sub>minor</sub>

> NotificationKind (by name)

The Type cell of this row reads `enum(int)`, which paragraph 295 defines as "integer-backed", while this cell says the value is stored by name. The code stores the name: `src/Backend/SIMF.Infrastructure/Persistence/Configurations/NotificationConfiguration.cs` applies `.HasConversion<string>().HasMaxLength(64)` to `Kind`, so the column is `nvarchar(64)`. The Severity row two rows below carries the same `enum(int)` type with no correcting note, and the same file applies `.HasConversion<string>().HasMaxLength(16)` to `Severity`, so that column is `nvarchar(16)` as well. Anyone sizing or querying these two columns from the dictionary gets the wrong type.

**Fix.** "NotificationKind, stored as its name in `nvarchar(64)`, not as an integer"

### 20. LLD paragraph 2075  <sub>minor</sub>

> Exhibition (section 5.5)

From this row on, the Context column cross-references the wrong sections. Section 5.5 is "Bookings & Attendance" (paragraph 179); Exhibition is 5.6 (paragraph 185). The same shift runs through the next four rows: Engagement & Live is given as 5.6 but is 5.7 (paragraph 191), Networking is given as 5.7 but is 5.8 (paragraph 197, "Networking and Cognitive AI"), Content & Media is given as 5.8 but is 5.10 (paragraph 209, "Media, News & Archive"), and Cognitive AI is given as 5.10 but shares 5.8 with Networking. A reader following any of these five references lands in the wrong module, which is exactly the complaint that the information cannot be found.

**Fix.** "Exhibition (section 5.6)"

### 21. LLD paragraph 2088  <sub>serious</sub>

> GpsPresence, StatisticSnapshot

Neither entity exists. A search for a class or record named `StatisticSnapshot` across `d:/SIMF/System/V1.0.0/src` returns nothing, `SimfAppDbContext` declares no such `DbSet`, and `src/Backend/SIMF.Infrastructure/Statistics/StatisticsService.cs` computes every dashboard figure on read through `GetDashboardAsync` and `GetProgrammeAsync`, so there is no snapshot table for a figure to be recomputed into. `GpsPresence` does not exist either: the positional record is `DevicePositionPing` (`src/Backend/SIMF.Domain/Programme/DevicePositionPing.cs`, `DbSet DevicePositionPings`). Neither name is covered by the as-built mapping at paragraph 312, so a reviewer following this row to the schema finds no table under either name.

**Fix.** "DevicePositionPing"

### 22. LLD paragraph 2096  <sub>serious</sub>

> Entities, aggregates, enums, domain rules; no framework dependencies

The Domain project does have a framework dependency. `src/Backend/SIMF.Domain/SIMF.Domain.csproj` contains `<PackageReference Include="Microsoft.Extensions.Identity.Stores" Version="10.0.8" />`, and it is needed: `src/Backend/SIMF.Domain/IdentityAccess/SimfUser.cs` opens with `using Microsoft.AspNetCore.Identity;` and declares `public class SimfUser : IdentityUser<Guid>`, as does `SimfRole.cs`. The claim is made twice, here and in the project-dependency table at paragraph 2186 ("Innermost; no framework deps"), and this document lists the very package at paragraph 2250. A ministry architect checking the DDD layering claim opens the csproj first, and this is the first thing found.

**Fix.** "Entities, aggregates, enums and domain rules. One framework package, `Microsoft.Extensions.Identity.Stores`, because `SimfUser` derives from `IdentityUser<Guid>`"

### 23. LLD paragraph 2128  <sub>minor</sub>

> Global per-IP + named policies (`auth`, `auth-email`, `ai-test`)

There are six named rate-limit policies, not three. `src/Backend/SIMF.Api/Program.cs` registers `RateLimitOptions.OperationalPolicy`, `RateLimitOptions.LookupPolicy`, `auth`, `auth-email`, `ai-test` and `ai-assistant`; the two constants resolve to "operational" and "lookup" in `src/Shared/SIMF.Common/Options/RateLimitOptions.cs` lines 43 and 59. The three missing ones matter to a reviewer counting controls: `ai-assistant` caps the cognitive assistant, `lookup` covers signed-in reference-data lookups, and `operational` registers a no-limiter partition that switches rate limiting off for the on-site gate, arrival and walk-in registration endpoints.

**Fix.** "Global per-IP cap plus named policies (`auth`, `auth-email`, `lookup`, `ai-test`, `ai-assistant`, `operational`); `operational` exempts the on-site gate and registration endpoints"

### 24. LLD paragraph 2206  <sub>serious</sub>

> Microsoft.AspNetCore.Components.Web

This is the References cell of the `SIMF.Components` row in a table headed "Project-reference graph", where every other row lists project references. It names a NuGet package and omits the project reference the project actually declares: `src/Shared/SIMF.Components/SIMF.Components.csproj` contains `<ProjectReference Include="..\SIMF.Common\SIMF.Common.csproj" />`. The same table covers only ten of the twelve projects under `src/`. It omits `SIMF.MobileEdge`, which paragraph 320 places in the presentation zone and the deployment diagram draws as a node, and which references no SIMF project because it is a YARP reverse proxy; and `SIMF.BadgeDesk`, which paragraph 53 names as one of the four clients calling the API, and which references `SIMF.Common` and `SIMF.Contracts`. A reviewer asking what the mobile edge is made of gets no answer from the section that exists to answer it.

**Fix.** "Shared Blazor components + theme; references `SIMF.Common`"

### 25. LLD paragraph 2305  <sub>serious</sub>

> ^2.6.1

This is the version cell of the `flutter_riverpod` row (paragraphs 2304 to 2306), and the caption at paragraph 371 says the list is read "from `pubspec`". The pubspec says otherwise: `src/Mobile/simf_app/pubspec.yaml` line 55 reads `flutter_riverpod: ^3.4.2`, and `src/Mobile/simf_app/packages/simf_data_pkg/pubspec.yaml` pins the same `^3.4.2`. Both are the same at HEAD, so this is not an uncommitted working-tree difference. Riverpod 2 and Riverpod 3 are different major versions with different APIs and different retry behaviour, so a reviewer opening the pubspec finds the document wrong on the app's central state-management dependency.

**Fix.** "^3.4.2"

### 26. LLD paragraph 2358  <sub>serious</sub>

> pretty_dio_logger

This package is not a dependency of this project. It appears in none of the three pubspec files (`src/Mobile/simf_app/pubspec.yaml`, `packages/simf_data_pkg/pubspec.yaml`, `packages/simf_auth_pkg/pubspec.yaml`), and `src/Mobile/simf_app/pubspec.lock` contains zero occurrences of the string "pretty", so it is not present transitively either. The row names a version, `^1.4.0`, that has no source. In the other direction the table omits direct dependencies the app really carries: `pointycastle ^3.9.1` (AES-256-GCM for the offline badge decoder), `crypto ^3.0.6` and `device_info_plus ^12.4.0`, plus `dio ^5.10.0`, `flutter_secure_storage ^9.2.2` and `shared_preferences ^2.3.3` inside `simf_data_pkg`. The three cells are replaced with a package that is real and security-relevant.

**Fix.** "pointycastle"

---

## 2. Claims the code disproves

### 27. BRD paragraph 60  <sub>serious</sub>

> Live sessions, questions, comments and session summaries, networking, the cognitive assistant and the FAQ.

Comments are in scope here, and the feature was removed from the system at the owner's instruction. docs/decisions/DECISIONS_LOG.md D-589 (2026-07-04) deletes the SessionComment and SessionCommentLike entities, the ISessionCommentService and ICommentAiFilter services, the public and admin comment endpoints, the Comments permissions and the Control Panel moderation page; D-605 removes the app screen, quoting the owner: "remove it totally from system - it has been rejected by customer". A search of src confirms none of it remains. A scope list that includes a rejected feature is the first thing a reviewer checks against what was delivered.

**Fix.** "Live sessions, questions and session summaries, networking, the cognitive assistant and the FAQ."

### 28. BRD paragraph 77  <sub>serious</sub>

> Exhibition: an organisation registers as an exhibitor, the PR team reviews and approves it, and assigns the booth in the same step.

No part of this process is built. Every exhibitor route in src/Backend/SIMF.Api/Endpoints/Exhibitors/ExhibitorEndpoints.cs is an admin route: POST /admin/exhibitors creates the company, POST /admin/exhibitors/{id}/accounts provisions an account and .../accounts/link attaches an existing one. The Exhibitor entity (src/Backend/SIMF.Domain/Exhibitors/Exhibitor.cs) carries no approval status, PermissionCatalog.cs has no Exhibitors.Approve code, and the Control Panel has Exhibitors and Booths add/edit/view pages with pending-approval queues only for Visitors, Others and Staff. The booth is linked to its exhibitor by Booth.ExhibitorId, set in the Control Panel, and the entity comments record that "a booth is coded before its exhibitor is signed". There is also no route by which an organisation could register: paragraph 55 states the website has no user registration, and the app has no exhibitor application.

**Fix.** "Exhibition: an administrator records the exhibiting organisation in the Control Panel, assigns its booth, and provisions or links the accounts that represent it on the stand."

### 29. BRD paragraph 82  <sub>serious</sub>

> the system fixes only the Administrator role and the catalogue of pages and actions. The table lists the system user types.

The word "only" is wrong, and the table directly below it proves it: rows 6, 7 and 8 are Moderator, Staff and Guest, none of which an administrator can build or remove. They are fixed in code, in the AppRole enum at src/Mobile/simf_app/packages/simf_auth_pkg/lib/src/domain/app_role.dart (guest, visitor, moderator, staff, exhibitor) and in MobileAppRole at src/Shared/SIMF.Common/Enums/MobileAppRole.cs; the app's router pins each route to that fixed set. What an administrator does configure is which profile type maps to which app role, not the roles themselves. The second sentence adds a second problem: the table is headed "Role Name" and mixes Control Panel roles, mobile-app roles and registration types, so calling it a list of "user types" leaves the reader unable to tell which column of the role model any row belongs to.

**Fix.** "Control Panel roles are dynamic: the Administrator builds and assigns them. The system fixes the Administrator role, the catalogue of pages and actions, and the mobile-app roles Guest, Visitor, Exhibitor, Moderator and Staff. The table lists the roles and the registration types."

### 30. HLD paragraph 66  <sub>serious</sub>

> PII (national ID / Iqama / passport, mobile numbers) and identity-document images encrypted at rest with application-level AES-GCM (a 32-byte operator-supplied key via Storage:UserIdDocumentEncryptionKey; IPiiEncryptor).

The parenthetical names one key and one component for two different mechanisms, and it is wrong for the images. The code uses two separate operator-supplied keys. The PII columns go through IPiiEncryptor, implemented by AesGcmPiiEncryptor, which reads Storage:UserIdDocumentEncryptionKey (src/Backend/SIMF.Infrastructure/Identity/AesGcmPiiEncryptor.cs line 35; registered at src/Backend/SIMF.Infrastructure/DependencyInjection.cs line 803); it is a flat AES-256-GCM column cipher. Identity-document images do not use it. FileService.IdDocument carries EncryptAtRest: true (src/Shared/SIMF.Common/Files/FileServicePolicy.cs line 127) and its bytes are encrypted by AesGcmEnvelopeCipher, which reads a different key and a key version, FileStorage:EncryptionKey and FileStorage:KekVersion (src/Backend/SIMF.Infrastructure/Files/AesGcmEnvelopeCipher.cs lines 41 to 70; src/Backend/SIMF.Api/appsettings.json lines 55 to 59); it is envelope encryption, a key-encryption key wrapping a per-file data key. Line 518 makes the mirror-image error, describing the column encryption as "application-level AES-GCM envelope encryption". Two keys have to be provisioned, guarded and rotated, and this paragraph tells the operator there is one.

**Fix.** "Encryption: TLS 1.2+ in transit. PII columns (national ID, Iqama, passport and mobile numbers) are encrypted at rest with application-level AES-256-GCM under an operator-supplied 32-byte key, Storage:UserIdDocumentEncryptionKey. Identity-document images and the other confidential stored files are encrypted at rest with AES-GCM envelope encryption under a second operator-supplied key, FileStorage:EncryptionKey, which wraps a per-file data key. There is no dependency on databas"

### 31. HLD paragraph 112  <sub>serious</sub>

> (the administrator is the "Backend user" in the SITE reference topology of section 2.1)

Section 2.1 runs from line 125 to line 215 and contains no reference topology and no "Backend user". The strings "Backend user" and "reference topology" each occur exactly once across all four delivered documents, in this parenthetical, so the cross-reference points at a term the reviewer cannot resolve anywhere. Section 2.1's own names for this actor are "the administrator browser" (line 158) and "Admin tier, SIMF.ControlPanel" (line 181).

**Fix.** "use the Control Panel, reached on a separate hostname. Each administrator holds a named role"

### 32. HLD paragraph 133  <sub>serious</sub>

> Transport. Every connection uses HTTPS with TLS 1.2 or higher.

The document's own Communication Requirements Matrix contradicts this twice: "SIMF.Api / Mail server / SMTP / STARTTLS / 587" (lines 467 to 472) and "SIMF.Api / SQL Server (AG listener) / TCP / TDS / 1433" (lines 473 to 478). The sizing table in this same section says the same at line 205, "On-site SMTP relay, STARTTLS on 587". Neither is HTTPS, so "every connection" is disprovable from page one of section 2.8.

**Fix.** "Transport. Every HTTP connection, client to server and server to server, uses TLS 1.2 or higher. TLS terminates at the load balancer and is re-encrypted to the backend. The mail and database paths run on their own protocols and ports, listed in the Communication Requirements Matrix in section 2.8."

### 33. HLD paragraph 136  <sub>serious</sub>

> Reads are served by the readable secondary and a per-node in-memory cache; the primary handles writes and cache misses.

The delivered software cannot route reads to a secondary. src/Backend/SIMF.Infrastructure/DependencyInjection.cs reads exactly two connection strings, "SimfIdentityDb" (line 118) and "SimfAppDb" (line 124), and each DbContext uses its one string for reads and writes; there is no ApplicationIntent=ReadOnly anywhere in src or deploy, and AlwaysOn read-only routing only happens when the client asks for it. The same unsupported claim drives line 132, "a listener that sends writes to the primary and read-only queries to a secondary", and recurs outside this section at lines 390, 540 and 559. The per-node cache half is supported (AddMemoryCache in src/Backend/SIMF.Api/Program.cs line 143, plus read-through caches such as GateConfigCache).

**Fix.** "The workload is read-heavy and cacheable. A per-node in-memory cache absorbs repeated reads; the database serves the rest."

### 34. HLD paragraph 138  <sub>serious</sub>

> rate limits and per-dependency circuit breakers return a clean 429 or 503 rather than failing

There is no per-dependency circuit breaker in the build. A case-insensitive search of src for "circuit" returns only the per-gate failure-rate control (src/Backend/SIMF.Application/AccessControl/Abstractions/IGateFailureCircuit.cs, a scan-denial guard, not a dependency breaker) and two unrelated comments; there is no Polly package reference and no breaker around the database, the LLM server, MinIO or SMTP. The 503s that exist are AI_FEATURE_DISABLED and AI_PROVIDER_NOT_CONFIGURED in src/Backend/SIMF.Infrastructure/Ai/AiService.cs, which are configuration states, not overload behaviour. Rate limiting is real: src/Backend/SIMF.Api/Program.cs line 181 onwards builds several partitioned limiters and answers 429 at line 351.

**Fix.** "Under overload the system degrades gracefully: the multi-dimensional rate limits return a clean 429 rather than failing."

### 35. HLD paragraph 221  <sub>minor</sub>

> Public Website (Blazor SSR), static informational content about the forum and its programme.

The website is not static. The same paragraph then says its assistant sends visitor questions to the AI service, and line 131 says it reaches data through the API, line 388 says the load balancer "routes public pages to SIMF.Web" which then calls SIMF.Api, and line 180 calls it "Blazor SSR, public and read-heavy". The code agrees: src/Website/SIMF.Web/Components/Pages holds Programme.razor, SessionDetail.razor, Speakers.razor, Archive.razor and MeetingConfirm.razor, plus Layout/LandingChatbot.razor. "Static" also undercuts the two 16 vCPU web hosts the sizing table asks for.

**Fix.** "Public Website (Blazor SSR), public information about the forum and its programme, rendered on the server from the API."

### 36. HLD paragraph 267  <sub>serious</sub>

> Every invocation is logged and rate-limited per administrator.

Only the two Control Panel AI endpoints are limited per administrator. In src/Backend/SIMF.Api/Program.cs the "ai-test" and "ai-assistant" policies partition on the JWT sub claim, but every app and website AI endpoint in src/Backend/SIMF.Api/Endpoints/Ai/AiFeatureEndpoints.cs and LiveAiEndpoints.cs uses RequireRateLimiting("auth"), and the "auth" policy partitions on httpContext.Connection.RemoteIpAddress. The document's own table contradicts the sentence too: paras 309 and 313 say the FAQ answer and Assistance features send "the visitor question", so a per-administrator limit cannot bound every invocation. Para 520 repeats the same overstatement ("per-administrator on AI calls").

**Fix.** "Every invocation is logged. Control Panel AI calls are rate-limited per administrator; app and website AI calls are rate-limited per source address."

### 37. HLD paragraph 343  <sub>serious</sub>

> over roughly 34 feature areas:

The number matches neither the list beside it nor the build. The list after the colon names 24 areas. The app has 43 directories under src/Mobile/simf_app/lib/features. The figure is carried over from the LLD paragraph 150 ('~34 feature folders under lib/features/'), which is wrong against the same folder count. A reviewer counts the list, gets 24, and stops trusting the section.

**Fix.** "over the main feature areas:"

### 38. HLD paragraph 344  <sub>serious</sub>

> roughly 12 navigation groups: Overview, People, Access Control, Programme, Scientific Committee, Exhibition, Engagement, Knowledge, Content, Public Relations, Gates and Reference Data.

The Control Panel has 14 navigation groups, not 12. src/ControlPanel/SIMF.ControlPanel/CpNavigation.cs declares, in display order: Nav.Overview, Nav.People, Nav.AccessControl, Nav.Programme, Nav.ScientificCommittee, Nav.Exhibition, Nav.Engagement, Nav.Knowledge, Nav.Content, Nav.PublicRelations, Nav.Gates, Nav.ReferenceData, Nav.System and Nav.Reports. The list omits System and Reports; Reports is the reporting module, which is one of the things a ministry team reads this section to find. The same 12-name list appears in the LLD at paragraph 143 and needs the same correction.

**Fix.** "14 navigation groups: Overview, People, Access Control, Programme, Scientific Committee, Exhibition, Engagement, Knowledge, Content, Public Relations, Gates, Reference Data, System and Reports."

### 39. HLD paragraph 367  <sub>serious</sub>

> by per-service scripts (deploy/set-env-api, set-env-cp, set-env-web); the committed templates carry empty values, and the real values are set on the server and never committed.

Two errors. The list omits the fourth site: deploy/set-env-edge.ps1 is tracked in git and carries the mobile edge configuration, and the edge is one of the four sites the pipeline deploys, so as written the document leaves one deployed site with no configuration procedure. And they are not templates: the .template.ps1 files were removed and the four tracked scripts now carry live non-secret values (62 of the 81 entries in set-env-api.ps1 hold a value). What is empty is the secrets: all 18 entries marked Secret in set-env-api.ps1 have an empty value, and set-env-cp.ps1, set-env-web.ps1 and set-env-edge.ps1 contain no secret entry at all.

**Fix.** "by one script per site (deploy/set-env-api.ps1, set-env-cp.ps1, set-env-web.ps1 and set-env-edge.ps1); the committed scripts carry the non-secret values, every secret entry is empty, and the real secrets are set on the server and never committed."

### 40. HLD paragraph 390  <sub>serious</sub>

> a read-write primary and one readable secondary behind a listener, giving both automatic failover and read scale-out.

The Availability Group gives failover. It does not give this system read scale-out, because nothing asks for a read-only connection. SQL Server sends a session to a readable secondary only when the connection carries ApplicationIntent=ReadOnly, and that string appears nowhere under src or deploy. The API resolves exactly two connection strings, SimfIdentityDb and SimfAppDb (src/Backend/SIMF.Infrastructure/DependencyInjection.cs lines 118 and 124), and there is no second read-only DbContext, so every query lands on the primary. The claim is repeated at paragraphs 132, 136, 539, 540, 559 and 577, and paragraph 136 goes further ('Reads are served by the readable secondary'); all of them need the same correction.

**Fix.** "a read-write primary and one secondary behind a listener, with synchronous commit and automatic failover."

### 41. HLD paragraph 400  <sub>minor</sub>

> The exact concurrent-session and throughput figures are confirmed during staging load testing and recorded in the deployment prerequisites.

They are not recorded there. The prerequisites are the three bullets at paragraphs 361 to 363, a TLS certificate, the outbound YouTube exception, and the shared store plus session affinity; none of them holds a concurrency or throughput figure, and paragraph 584 confirms that list is what 'the production prerequisites' means. The rest of the sentence repeats paragraph 567.

**Fix.** "Staging load testing confirms the exact concurrent-session and throughput figures before launch."

### 42. HLD paragraph 518  <sub>minor</sub>

> Personal data (national ID, Iqama, passport and mobile numbers) and identity-document images are encrypted with application-level AES-GCM envelope encryption, the key supplied through an environment variable.

Two different schemes under two different keys, described as one. File bytes use AesGcmEnvelopeCipher: a fresh 256-bit data key for each file, sealed under a key-encryption key from FileStorage:EncryptionKey. That is envelope encryption. The personal-data columns use AesGcmPiiEncryptor, which seals the value directly under Storage:UserIdDocumentEncryptionKey with no data key and no wrapping, so it is not envelope encryption. The keys are separate environment variables (SIMF_API_FileStorage__EncryptionKey and SIMF_API_Storage__UserIdDocumentEncryptionKey), not the one this sentence promises, and a reviewer auditing the key hierarchy for the national-ID column will find no data key to audit.

**Fix.** "Encryption at rest. Identity-document images and the other encrypted file categories use AES-256-GCM envelope encryption: a fresh data key for each file, sealed under a key-encryption key. Personal-data columns (national ID, Iqama, passport and mobile numbers) are encrypted with AES-256-GCM under their own key. Both keys are supplied through environment variables."

### 43. HLD paragraph 521  <sub>serious</sub>

> Audit: two append-only trails, OperationLog for security-relevant business events and RowAudit for row-level before / after images, each carrying an actor snapshot, source IP, user agent and correlation ID.

"each carrying" is false for one of the two. src/Backend/SIMF.Domain/Auditing/RowAudit.cs has no source-IP and no user-agent column: its fields are OccurredAt, TableName, EntityType, Operation, PrimaryKey, ActorUserId, ActorDisplayName, CorrelationId, OldValuesJson, NewValuesJson and AffectedColumns. Only OperationLogEntry carries SourceIp and UserAgent. A compliance reviewer asked to evidence the source IP of a row change would find the column absent. Line 590 repeats the same claim ("both trails are append-only and carry actor, source IP and correlation ID") and needs the same correction.

**Fix.** "Audit: two append-only trails. OperationLog records security-relevant business events with an actor snapshot, source IP, user agent and correlation ID. RowAudit records row-level before / after images with an actor snapshot and correlation ID."

### 44. HLD paragraph 561  <sub>minor</sub>

> list endpoints page at source (default page size 20, grid cap 200)

Neither number is the system's. In `src/Shared/SIMF.Common/Grids/GridColumns.cs` the page-size policy is `public int FallbackTop { get; private set; } = 25;` with `PageSize(int fallback = 25, int max = 200)`, so the default is 25, not 20. Across the backend the 59 declarations read: 38 at `fallback: 25, max: 200`, nine at `fallback: 50, max: 500`, seven at `fallback: 50, max: 200`, four at `fallback: 20, max: 200` and one at `fallback: 20, max: 50`. So the cap is not 200 everywhere either: `AdminAiPromptService`, `AdminArchiveService`, `BusinessMeetingService` and `AdminCountryService` allow 500. The only place 20 is a default for a public endpoint is `PublicNewsEndpoints.cs`, `public int PageSize { get; set; } = 20;`. The same two numbers appear in the LLD at paragraph 360 and are corrected there in the second edit.

**Fix.** "Queries are indexed and projected; list endpoints page at source rather than filtering in memory. Each list resource declares its own default page size and its own maximum, and a request for more rows than the maximum is clamped to it."

### 45. HLD paragraph 573  <sub>serious</sub>

> with build-time tests that fail the build if a surface is ungated

The tests exist but no build runs them. `tests/SIMF.ControlPanel.Tests/CpNavigationPermissionTests.cs` and `tests/SIMF.Api.Tests/PermissionEnforcementTests.cs` are both present, and both sit behind the same switch as everything else: `azure-pipelines.yml` declares `runTests` with `default: false` and conditions the fast suite (line 611) and the integration suite (line 662) on it. The only unconditional test step filters to `PipelineTestGateTests`, which does not include the permission gates. The pipeline's own comment names these exact tests as the casualty: 'Among them are the Control Panel permission and navigation gates, which are what stop an admin page shipping ungated.' So an ungated admin surface can reach production with a fully green pipeline. Paragraph 514 makes the same claim in the same words and needs the same correction.

**Fix.** "Mitigation Strategies: per-page / per-action permission gating enforced at runtime on both the API and the Control Panel, with tests that fail when a surface is ungated. Those tests sit behind the pipeline switch named under Schedule and Change-Freeze Risk, so they are run before a push rather than by the build."

### 46. LLD paragraph 68  <sub>serious</sub>

> `AllowAnonymous()` for the public read endpoints, for the authentication endpoints that run before the caller holds a bearer token, and for the single-use speaker action-token endpoint

Two anonymous endpoints fall outside all three categories. src/Backend/SIMF.Api/Endpoints/Ai/AiFeatureEndpoints.cs declares Post("/app/ai/faq") with AllowAnonymous() at line 61 and Post("/app/ai/translate") with AllowAnonymous() at line 150. Neither is a read, an authentication step, nor the speaker action token, and both reach the on-site LLM server through IAiService. A security reviewer reading this sentence would conclude the AI is unreachable without a token, and the AI endpoint was one of the customer's eight written review points.

**Fix.** "`AllowAnonymous()` for the public read endpoints, for the authentication endpoints that run before the caller holds a bearer token, for the single-use speaker action-token endpoint, and for the two AI endpoints `POST /app/ai/faq` and `POST /app/ai/translate`, which carry no credential and are rate-limited under the `auth` policy"

### 47. LLD paragraph 76  <sub>serious</sub>

> and surfaced in SIMF as links, so it requires no additional storage, encoding or bandwidth on ministry servers.

The platform does store and serve video from ministry servers. src/Backend/SIMF.Infrastructure/Programme/AdminSessionService.cs line 933 stores an uploaded session recording under FileService.SessionRecording, and src/Backend/SIMF.Api/Endpoints/Programme/SessionRecordingStreamEndpoints.cs mints a short-lived stream token and range-streams 'the recording's MP4 bytes' to the app. The HLD agrees, at its line 122: MinIO holds 'avatars, encrypted identity documents, VIP photos, session recordings, speaker presentations and media assets'. This document never states what the file store holds, so a reviewer sizing MinIO has only this sentence, and it tells them video costs nothing. File storage, its sizes and volume was one of the customer's eight written review points.

**Fix.** "and surfaced in SIMF as links, so the library itself needs no storage, encoding or bandwidth on ministry servers. Session recordings are the exception and are sized with the file store: an administrator uploads the recording file, the API stores it in MinIO and streams its bytes to the app against a short-lived stream token."

### 48. LLD paragraph 121  <sub>serious</sub>

> roughly 34 mobile screens

34 is a feature-folder figure carried over to a screen count, and it is wrong as both. src/Mobile/simf_app/lib/features/ holds 42 folders and 72 *_screen.dart files, and docs/pages/PAGE-INDEX.md lists 74 mobile screens as built. Paragraph 150 uses the same number for the other thing, 'There are ~34 feature folders under `lib/features/`', and paragraph 342 then prints 40 folder names. A count that is half the real one, in the sentence that claims the inventory is exhaustive, is the first thing a reviewer checks.

**Fix.** "the exhaustive per-page and per-action inventory covers the Control Panel, the Website and every mobile screen, together with the per-page E2E catalogue."

### 49. LLD paragraph 143  <sub>serious</sub>

> Navigation is organised into ~12 permission-gated groups (Overview, People, Access Control, Programme, Scientific Committee, Exhibition, Engagement, Knowledge, Content, Public Relations, Gates, Reference Data).

src/ControlPanel/SIMF.ControlPanel/CpNavigation.cs declares 14 groups, not about 12: the twelve named plus Nav.System (line 172) and Nav.Reports (line 189). Both hold real pages, including /admin/configuration, /admin/site-settings, /admin/logs, /admin/editions and nine reporting pages. The count is not approximate and the list is not complete. The omission carries into the table: paragraph 144 says 'The following table lists the Control Panel pages. The single stub route `/m/{module}` (ModulePlaceholder) is omitted', and the table then omits every /admin/reports page. Nine such routes exist under src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/Reports/ and docs/pages/PAGE-INDEX.md lines 126 to 134 mark all nine Real, each with its own permission.

**Fix.** "Navigation is organised into 14 permission-gated groups: Overview, People, Access Control, Programme, Scientific Committee, Exhibition, Engagement, Knowledge, Content, Public Relations, Gates, Reference Data, System and Reports."

### 50. LLD paragraph 147  <sub>serious</sub>

> There is no public navigation menu: every page is reached by direct URL.

The site has a navigation menu. src/Website/SIMF.Web/Components/Layout/LandingHeader.razor renders `<nav class="ln-nav" ...>` over the menu model, plus an off-canvas menu for small screens, and src/Website/SIMF.Web/Content/LandingChrome.cs defines five top-level entries: About (drop-down of /about, /about/objectives, /about/themes, /about/organizer, /partners, /about/venue), Programs (drop-down of /programme, /programme/opening, /programme/sessions, /programme/exhibition, /programme/gov-meetings, /visit), Speakers to /speakers, Discover to https://www.visitsaudi.com and Archive to /archive. A reviewer opening the site sees this immediately.

**Fix.** "The top navigation menu carries five entries: About and Programme as drop-downs, Speakers and Archive, and one external link to visitsaudi.com."

### 51. LLD paragraph 150  <sub>serious</sub>

> presents a persistent 5-tab bottom-nav shell (`StatefulShellRoute.indexedStack`), Home, Sessions, Badge, Map, Profile ... There are ~34 feature folders under `lib/features/`;

Two checkable numbers, both wrong. `StatefulShellRoute.indexedStack` is not used by the app: it appears in the source only as the thing that was removed. src/Mobile/simf_app/lib/app/router.dart says "Shell route - replaces StatefulShellRoute.indexedStack" and src/Mobile/simf_app/lib/app/widgets/simf_app_shell.dart says "Replaces `StatefulShellRoute.indexedStack`"; the shell is one GoRoute holding an IndexedStack of five tab widgets, which the comments say was done deliberately to keep a single page in the parent Navigator. And `ls src/Mobile/simf_app/lib/features/` returns 42 directories, not about 34. A reviewer who greps the repository for the named class finds it only in comments saying it is gone.

**Fix.** "presents a persistent 5-tab bottom-nav shell, Home, Sessions, Badge, Map, Profile, held in one `IndexedStack` under a single route so per-tab state is preserved, with auth/role gates (staff/moderator) redirecting appropriately. The theme is navy-always (dark pinned), bilingual with automatic RTL for Arabic, and every in-app page carries a global app-bar invariant (notifications bell + language toggle + dark-mode indicator + hamburger). There are 42 feature folders under `lib/"

### 52. LLD paragraph 170  <sub>serious</sub>

> 10 or more denials in 60 s opens a 5-minute circuit

Recorded denials never open the circuit. src/Backend/SIMF.Infrastructure/AccessControl/GateOperatorService.cs gates the call: "G-3 - only SYSTEM-fault denials count toward the failure-rate circuit. Benign POLICY denials (unknown QR, holder-not-approved, wrong profile type, ...) are the operator's normal traffic and must never trip a 5-minute gate outage for everyone. Every reason the engine emits today is a policy denial, so none feed the circuit here; genuine infrastructure faults feed it from the QR-resolver catch block above instead." As written the document tells an operations reader that ten unknown badges shut a venue gate for five minutes, which is exactly the behaviour the code was written to prevent. The 60-second window and 5-minute open are correct (GateFailureCircuit.cs).

**Fix.** "10 or more backend faults in 60 s opens a 5-minute circuit, and a recorded policy denial never feeds it"

### 53. LLD paragraph 205  <sub>minor</sub>

> the session-reminder job dedups on `Session.ReminderSentUtc`

There is no such column. src/Backend/SIMF.Domain/Programme/Session.cs declares `public DateTime? ReminderSentAt { get; set; }` with the comment "Stamped by the reminder worker; the null check is its once-only guard", and src/Backend/SIMF.Infrastructure/Operations/SessionReminderWorker.cs claims a session with a conditional update on `ReminderSentAt`. A reader checking the schema for `ReminderSentUtc` finds nothing.

**Fix.** "the session-reminder job dedups on `Session.ReminderSentAt`"

### 54. LLD paragraph 279  <sub>serious</sub>

> `StatisticSnapshot` stores computed dashboard figures; the context mostly reads from the others.

Nothing is stored. There is no `StatisticSnapshot` class in the source and no statistics table in either `InitialCreate` migration. `src/Backend/SIMF.Infrastructure/Statistics/StatisticsService.cs` computes every dashboard figure on each request with live `CountAsync` queries over the other contexts. `GpsPresence` does not exist either; the as-built telemetry table is `DevicePositionPings` (`src/Backend/SIMF.Domain/Programme/DevicePositionPing.cs`), and nothing in `SIMF.Infrastructure` or `SIMF.Application` deletes from it, so there is no retention purge to describe.

**Fix.** "Entities: `GpsPresence`, as built `DevicePositionPing`."

### 55. LLD paragraph 296  <sub>serious</sub>

> AspNetUsers (`SimfUser: IdentityUser<Guid>`), SIMF_Identity

There is no `AspNetUsers` table in `SIMF_Identity`. `src/Backend/SIMF.Infrastructure/Persistence/SimfIdentityDbContext.cs:131` maps the entity with `modelBuilder.Entity<SimfUser>().ToTable("Users")`, and the Identity `InitialCreate` migration creates `Users`, `Roles`, `UserRoles`, `UserClaims`, `UserLogins`, `UserTokens` and `RoleClaims`, with no `AspNet` prefix anywhere. The data dictionary is the section a DBA reads to find a table by name, and this heading and its note both give a name that is not in the database.

**Fix.** "Users (`SimfUser: IdentityUser<Guid>`), SIMF_Identity"

### 56. LLD paragraph 337  <sub>serious</sub>

> SIMF.Api/ ├─ App_Data/ ├─ Authentication/ # JWT/StreamToken scheme setup ├─ Authorization/ # PermissionAuthorization (policy provider + handler) ├─ Endpoints/ # feature-grouped endpoint classes (see below) ├─ HostedServices/ # in-process scheduled jobs (dormant sweep, reminders) ├─ Middleware/ # correlation, security headers, error handling, email-rate-key, swagger auth ├─ RateLimiting/ ├─ Request

The tree names two directories that are not in the source tree and omits three that are. `App_Data/` and `logs/` are runtime output, ignored by `.gitignore` lines 35, 36 and 121; `src/Backend/SIMF.Api/` contains no such folders. Missing are `HealthChecks/` (`WorkersHealthCheck.cs`), `Infrastructure/` (`HttpPublicApiOriginProvider.cs`) and `Serialization/` (`SaudiDateTimeOffsetJsonConverter.cs`). Two annotations are also wrong: the reminder workers are not in `HostedServices/`, which holds only `DormantAccountSweepService.cs` and `RetentionSweepWorker.cs`, while `SessionReminderWorker` and its siblings live in `src/Backend/SIMF.Infrastructure/Operations/`; and `email-rate-key` is not in `Middleware/` but is `RateLimiting/EmailRateLimitKeyMiddleware.cs`. `Middleware/` in turn holds `AppKeyMiddleware.cs`, the `X-App-Key` check on the mobile surface, which the annotation does not mention.

**Fix.** "SIMF.Api/ ├─ Authentication/ # JWT/StreamToken scheme setup ├─ Authorization/ # PermissionAuthorization (policy provider + handler) ├─ Endpoints/ # feature-grouped endpoint classes (see below) ├─ HealthChecks/ # WorkersHealthCheck ├─ HostedServices/ # in-process scheduled jobs (dormant-account sweep, retention sweep) ├─ Infrastructure/ # HttpPublicApiOriginProvider ├─ Middleware/ # app key, correlation, security headers, error handling, swagger auth ├─ RateLimiting/ # EmailRa"

### 57. LLD paragraph 339  <sub>serious</sub>

> Sponsors, Statistics, Support, Venue. (Full list; not summarised.)

The list is not full. `src/Backend/SIMF.Infrastructure/` holds 45 feature folders; the paragraph names 41, omitting `Editions`, `Notifications`, `Preferences` and `Reporting`. The endpoint list in paragraph 338 has the same gap: `src/Backend/SIMF.Api/Endpoints/` holds 27 folders and the paragraph names 26, omitting `Reporting`. A paragraph that promises completeness and misses four folders is worse than one that does not promise it, because a reviewer uses it to conclude a feature is absent.

**Fix.** "SIMF.Infrastructure internals (feature/service folders): AccessControl, Ai, Archive, Assets, Attendance, Auditing, BusinessMeetings, Cms, Common, Configuration, Contacts, Delegations, Editions, Email, Excel, Exhibition, Exhibitors, Faq, Feedback, Files, Identity, IdentityAccess, Logs, Media, MeetingRequests, MyArea, Networking, Notifications, Operations, Organisations, Persistence, Preferences, Programme, PublicRelations, Recommendations, Regions, Reporting, Requests, SeatRes"

### 58. LLD paragraph 342  <sub>serious</sub>

> └─ features/ # ~34 feature folders (per-screen)

`src/Mobile/simf_app/lib/features/` holds 42 folders, and the list printed in this same paragraph names 40 of them, so the count is wrong against both the source tree and the paragraph's own list. The two folders the list omits are `banners` and `visitor_profile`.

**Fix.** "└─ features/ # 42 feature folders (per-screen)  ├─ about, accessibility, account, ai_summary, archive, badge, banners,  ├─ booths, chatbot, contact_us, contacts, content, delegations,  ├─ exhibition, exhibitor, faq, feedback, forum_guide, gallery, gates,  ├─ guest, home, live, media_partners, meet, meetings, moderation, more,  ├─ myarea, news, notifications, onboarding, questions, registration,  ├─ requests, sessions, speakers, splash, sponsors, staff, venuemap,  └─ visitor_p"

### 59. LLD paragraph 360  <sub>serious</sub>

> Pagination: app GETs use `?page=&pageSize=&sort=&search=`

Almost no app GET accepts those parameters. The paging shape on the app surface is `skip` and `top`: `GET /app/media` binds `req.Skip` and `req.Top` (`src/Backend/SIMF.Api/Endpoints/Public/PublicMediaEndpoints.cs:30,42`), as do the business-meeting and session-attendance list requests. `GET /app/news` is the only GET in the API that binds `Page` and `PageSize` (`src/Backend/SIMF.Api/Endpoints/News/PublicNewsEndpoints.cs:14,23`). An integrator following this sentence sends `?page=2&pageSize=20`, FastEndpoints ignores the unbound parameters, and `GridQuery` returns the first page every time with no error. The `sort` and `search` half is right where the endpoint declares them.

**Fix.** "Pagination: app GETs page with `?skip=&top=`, plus `?sort=` and `?search=` on the endpoints that declare them; `GET /app/news` is the one exception and uses `?page=&pageSize=`; admin grids POST a `GridQuery` body returning `GridPage<T>`; default page size 20, grid cap 200."

### 60. LLD paragraph 363  <sub>serious</sub>

> The backend tables below list the exact NuGet packages and versions read from the repository (deduped across projects). Only packages present in the ground-truth inventory are listed.

The word "exact" is not supportable: the tables that follow omit three packages the repository references. `src/Backend/SIMF.Infrastructure/SIMF.Infrastructure.csproj` references `QuestPDF 2026.7.1`, used in `src/Backend/SIMF.Infrastructure/Identity/AdminAccountService.Bulk.cs`; QuestPDF is a commercially licensed library, so its absence from a ministry submission is not cosmetic. `src/Edge/SIMF.MobileEdge/SIMF.MobileEdge.csproj` references `Yarp.ReverseProxy 2.3.0`, which is what the mobile edge is built from (`AddReverseProxy()` and `MapReverseProxy()` in its `Program.cs`), and the mobile edge is a node on the normative deployment diagram. `src/Shared/SIMF.Components/SIMF.Components.csproj` references `Microsoft.AspNetCore.Components.Authorization 10.0.7`. A reviewer diffing the csproj files against these tables finds the gap in a minute.

**Fix.** "The backend tables below list the NuGet packages and versions read from the repository, deduped across projects. Three further packages are referenced: QuestPDF 2026.7.1 for badge PDF generation, Yarp.ReverseProxy 2.3.0 for the mobile edge, and Microsoft.AspNetCore.Components.Authorization 10.0.7 for the shared component library."

### 61. LLD paragraph 455  <sub>serious</sub>

> The mobile privilege model: None / Visitor / Staff / Moderator

The glossary defines `MobileAppRole` and lists four of its five values. src/Shared/SIMF.Common/Enums/MobileAppRole.cs declares None = 0, Visitor = 1, Staff = 2, Moderator = 3 and Exhibitor = 4. Exhibitor is not decorative: it is the value that gates the lead-capture endpoints `/app/exhibitor/visitors` and `/app/exhibitor/visitors/scan`, and it is read by AdminExhibitorService, ExhibitorVisitorService and StatisticsService. Its own summary says an Exhibitor "gets the full Visitor experience plus the lead-capture tools (scan a visitor's QR, My Visitors)". Two consequences inside this slice: the actor table at paragraphs 506 to 535 defines nine actors and no Exhibitor, yet UC-03 at paragraph 568 names "Visitor, Exhibitor" as its actors, so a reviewer meets an actor the table never defines. The document also names the same enum differently elsewhere: paragraph 117 reads "The mobile privilege enum is `Guest / Visitor / Staff / Moderator`", which is the Flutter-side enum, not the `MobileAppRole` this row puts in backticks.

**Fix.** "The mobile privilege model stored on `ProfileType.MobileAppRole` and carried in the `mobile_app_role` claim: None, Visitor, Staff, Moderator, Exhibitor. Exhibitor gates the lead-capture endpoints `/app/exhibitor/visitors` and `/app/exhibitor/visitors/scan`."

### 62. LLD paragraph 492  <sub>serious</sub>

> App DB, e-mail

The scheduled jobs write the Identity database as well. The same actor table lists the dormant-account sweep as a scheduled job at paragraph 535, and src/Backend/SIMF.Infrastructure/Identity/DormantAccountService.cs takes both contexts on its constructor, SimfIdentityDbContext and SimfAppDbContext. It selects from dbContext.Users, sets AccountState to Disabled, rolls SecurityStamp and calls dbContext.SaveChangesAsync on the Identity context, and separately updates UserProfiles on the App context. Its own comment says why both are touched: "Admission is decided on the attendee's profile, not on the account, so the sweep has to withdraw it there as well." A reviewer reading this cell would conclude no background job writes SIMF_Identity, which is wrong, and which matters because the two databases are physically separated.

**Fix.** "App DB, Identity DB, e-mail"

### 63. LLD paragraph 523  <sub>serious</sub>

> Scans badges at gates/hall doors, captures exhibitor leads, runs on-site registration desk.

Lead capture is not a Staff function, and the code was changed specifically to stop Staff doing it. src/Backend/SIMF.Api/Endpoints/Exhibitors/ExhibitorVisitorsEndpoints.cs carries the reason in its header: "the exhibitor check is enforced in the service. That check is the caller's profile type carries MobileAppRole.Exhibitor, not the old any non-visitor type, which let Staff / Moderator / Media / Sponsor tokens harvest visitor PII." ExhibitorVisitorService.cs line 447 tests "p.ProfileType.MobileAppRole == MobileAppRole.Exhibitor", and both endpoints are documented as "403 unless the caller is an exhibitor". A Staff token is refused. The same document contradicts this row twice over: paragraphs 1465 to 1470 attribute `myVisitors` and `scanVisitor` to "Exhibitor (approved)", while paragraph 646 splits the difference with "Staff (Exhibitor)". The other two duties on this row are real: gate scanning and Visitors.RegisterOnsite at paragraphs 1460 and 1463.

**Fix.** "Scans badges at gates and hall doors, and runs the on-site registration desk. Lead capture is not a Staff function. The exhibitor endpoints answer 403 to a Staff token."

### 64. LLD paragraph 585  <sub>serious</sub>

> Manual plus auto-close at last forum day.

There is no rule in the build that closes registration on the last forum day. Auto-close is a nullable date an administrator types. src/Backend/SIMF.Domain/Operations/OperationsToggles.cs declares "public DateTime? AutoClose" with the comment "An admin toggles it, and the background worker flips it false once AutoClose passes". The seeded row sets it to nothing: OperationsTogglesConfiguration.cs line 20, "AutoClose = null", and OperationsToggleService.cs line 118 creates the row the same way. UpdateRegistrationGateAsync writes whatever the Control Panel form posted, and OperationsToggles.razor.cs parses that value from a free-text field. Every reference to AutoClose in src was read; not one derives a date from the programme, the last forum day or EventEdition. RegistrationGateAutoCloseWorker only compares the typed date to the clock. So the gate does not close itself on the last forum day, and out of the box it does not close itself at all. The claim is repeated at paragraphs 164 and 224, which are outside this slice.

**Fix.** "Open or close the gate by hand. An administrator sets an optional close date and time, and a background job closes the gate once that time passes. The seeded gate carries no close date."

### 65. LLD paragraph 646  <sub>serious</sub>

> Staff (Exhibitor)

Staff cannot capture an exhibitor lead. The endpoint header in `src/Backend/SIMF.Api/Endpoints/Exhibitors/ExhibitorVisitorsEndpoints.cs` states "the exhibitor check is enforced in the service. That check is 'the caller's profile type carries MobileAppRole.Exhibitor', not the old 'any non-visitor type', which let Staff / Moderator / Media / Sponsor tokens harvest visitor PII." `ExhibitorVisitorService.EnsureExhibitorAsync` requires `p.ProfileType.MobileAppRole == MobileAppRole.Exhibitor` and then an active `ExhibitorMembership` of a live exhibitor, throwing 403 otherwise. `MobileAppRole` carries `Exhibitor = 4` as a value distinct from `Staff = 2`. Line 834 repeats the same wrong actor as "exhibitor lead scan (Staff)". A ministry integrator provisioning gate staff would find the scan answers 403.

**Fix.** "Exhibitor (booth officer)"

### 66. LLD paragraph 769  <sub>serious</sub>

> Unknown nationality code to `VISITOR_NATIONALITY_UNKNOWN`.

That wire code no longer exists. `src/Shared/SIMF.Common/ErrorCodes.cs` line 128 carries the rename in the source itself: "// User profile - nationality. Renamed from VISITOR_NATIONALITY_UNKNOWN so the wire code matches the new domain vocabulary." followed by `public const string ProfileNationalityUnknown = "PROFILE_NATIONALITY_UNKNOWN";`. `VISITOR_NATIONALITY_UNKNOWN` appears nowhere else in `src/` except inside that comment. An integrator branching on the documented string would never match the response.

**Fix.** "Missing mandatory field / no terms consent to `VALIDATION_FAILED` (submission blocked). Reject to mandatory 10-500 char reason, account to `Rejected`. Re-approve/re-reject a non-pending user to 409 `ADMIN_USER_NOT_PENDING`. Unknown nationality code to `PROFILE_NATIONALITY_UNKNOWN`. Registration closed to submission refused with a clear message."

### 67. LLD paragraph 787  <sub>serious</sub>

> Failure-rate circuit: 10 or more denials / 60 s, 429 `GATE_FAILURE_CIRCUIT_OPEN` for 5 min.

The sentence lists seven denial codes and then says ten denials in sixty seconds open the circuit, so a reviewer reads it as ten of those denials. The code excludes every one of them. `src/Backend/SIMF.Infrastructure/AccessControl/GateOperatorService.cs` guards the call with `if (IsSystemFaultDenial(reason)) { await failureCircuit.RecordDenialAsync(...); }` under the comment "only SYSTEM-fault denials count toward the failure-rate circuit. Benign POLICY denials (unknown QR, holder-not-approved, wrong profile type, ...) are the operator's normal traffic and must never trip a 5-minute gate outage for everyone. Every reason the engine emits today is a policy denial, so none feed the circuit here". The only site that feeds it is the `catch` around `qrResolver.ResolveAsync`. As written the document tells the operations team that ten refused badges close a gate for five minutes; they would build the wrong runbook.

**Fix.** "Recorded policy denials: `QR_UNKNOWN`, `GATE_INACTIVE_AT_SCAN`, `HOLDER_NOT_APPROVED`, `HOLDER_DISABLED`, `HOLDER_LOCKED`, `PROFILE_TYPE_INACTIVE`, `PROFILE_TYPE_NOT_ALLOWED`. `IDEMPOTENCY_KEY_CONFLICT` (409). Failure-rate circuit: 10 or more system faults at one gate within 60 s open the circuit for 5 minutes, and further scans answer 429 `GATE_FAILURE_CIRCUIT_OPEN`. A policy denial never feeds the circuit, so refused badges cannot close a gate."

### 68. LLD paragraph 933  <sub>serious</sub>

> `/admin/contacts`

The table lists a Control Panel page that was deleted thirteen months of decisions ago. The row is "`/admin/contacts` | Contacts | Administrator | Shared contact records" (paragraphs 933 to 936). No such page exists: `grep -rn "@page" src/ControlPanel` has no `/admin/contacts`, `find src -name Contact.cs` returns nothing, and `grep -n "class Contacts" src/Shared/SIMF.Common/PermissionCatalog.cs` returns nothing, so neither the page, the entity nor the permission that would gate it is in the build. `docs/decisions/DECISIONS_LOG.md` D-766 (2026-07-25) records the removal: "Deleted: the `Contact` class, the `Contacts` table, its five `FK_*_Contacts_ContactId` columns, the `/admin/contacts` CP page + `ContactsList/AddEdit/ViewDelete` + `ContactPicker`, `AdminContactService` + the contact endpoints + Excel export, the `Contacts.*` permission". `docs/SIMF-FDS-014-Contacts-and-Sharing.md` revision 0.2 says the same. A ministry reviewer following the permission-gating rule in paragraph 143 ("Every `/admin/*` page and action is gated by a `PermissionCatalog` code") would look for `Contacts.View` and find no such code.

**Fix.** Delete it.

### 69. LLD paragraph 1068  <sub>serious</sub>

> `/admin/companies`

The Exhibition group lists a retired route as a live page, next to the page that replaced it. The row is "`/admin/companies` | Companies | Administrator | Company records" (paragraphs 1068 to 1071), and four paragraphs later the same table lists "`/admin/exhibitors` | Exhibitors | Administrator | Manage exhibitors" (1072 to 1075). `grep -rn "@page" src/ControlPanel` has no `/admin/companies`, and `find src -name Company.cs` returns nothing, so neither the route nor the entity exists. `docs/pages/PAGE-INDEX.md` line 93 records it verbatim: "`/admin/companies` | Retired - renamed to `/admin/exhibitors` by `a05ef82d` (2026-06-04, Company to Exhibitor). The route does not exist". The same retired term leaks into the booths row at paragraph 1079, "Manage booths (officer, company, map coords)": `src/Backend/SIMF.Domain/Exhibition/Booth.cs` line 23 carries `public Guid? ExhibitorId` and a `Exhibitor?` navigation, and there is no company link.

**Fix.** Delete it.

### 70. LLD paragraph 1162  <sub>serious</sub>

> DB-backed override editor for the 6 transactional identity emails

The count is wrong and the word "identity" is wrong. `src/Shared/SIMF.Common/Enums/EmailTemplateType.cs` defines TEN values (SignInOtp, EmailVerification, AccountExists, PasswordReset, BadgeActivation, BiometricStepUp, BulkBadgeDelivery, EmailChangeVerification, EmailChangedNotice, ExhibitorLeadCapture), `src/Backend/SIMF.Application/Email/EmailTemplateCatalog.cs` defines ten matching entries and exposes them as `All` at line 191, and the page's own data source, `AdminEmailTemplateService.ListAsync`, builds its grid rows from `EmailTemplateCatalog.All`, so the editor shows ten rows and not six. Two of the ten are not identity e-mails at all: `BulkBadgeDelivery` is the cover note for a badge batch and `ExhibitorLeadCapture` is the lead card sent after a booth badge scan. A reviewer opening the page and counting the rows disproves this cell immediately.

**Fix.** "Bilingual override editor for the 10 transactional e-mail templates"

### 71. LLD paragraph 1356  <sub>serious</sub>

> Visitor+

The Audience column of the mobile screen catalogue does not match the app's own route gates, in five rows. The authority is `src/Mobile/simf_app/lib/app/router.dart`: a route is signed-in only if its number is in `_authenticatedRoutes`, and role-restricted only if it is in `_routeRoles`. (1) `sessions` (#16) is marked "Visitor+" at 1356, but 16 is in neither set. The router says "REVERSES D-576: the agenda (Sessions, 16) and session detail (17) are PUBLIC again, so a guest can browse the programme and open a session without signing in", and "They are intentionally NOT in this set." The table also contradicts itself: `sessionDetail` (#17) is "Guest+" at 1359, so a public detail page sits behind a list the same table says needs an account. Required value: Guest+. (2) `sessionPresentations` (#202) is marked "Approved" at 1380, but `GET /app/presentations` and `GET /app/presentations/{id}/file` both call `AllowAnonymous()` in `src/Backend/SIMF.Api/Endpoints/Public/PublicPresentationEndpoints.cs`, and the router says "202 (Session presentations) is PUBLIC (owner 2026-07-22): a guest opens it from the home Sessions tile, so it is intentionally NOT gated here." Required value: Guest+. Documenting an anonymous endpoint as approved-only is the wrong way round for a reviewer checking the anonymous surface. (3) `liveBroadcast` (#25) is marked "Login-only" at 1371. That label is used once in the four documents and defined nowhere, and it is wrong as a route audience: the router says "Live (25) likewise stays public with an in-screen need login prompt on the live screen itself (D-577), not a redirect." (4) `identityVerification` is marked "Visitor (approved)" at 1300, but route 103 is in `_authenticatedRoutes` precisely because approval gating broke sign-up: "Since D-666 a pending sign-up account presents as [AppRole.guest], so an attendee-gated 103 bounced EVERY sign-up user (all pending) to Home the moment they tapped capture face photo, sign-up was functionally broken." Required value: any signed-in account, including a pending one. (5) `sessionModerate` is marked "Moderator+ (per-session grant)" at 1457, but `_routeRoles` holds `104: <AppRole>{AppRole.moderator}` with "Moderator-EXCLUSIVE now (D-519): Staff no longer inherits it (the old isAtLeast made Staff >= Moderator)." The plus sign asserts the ladder D-519 removed.

**Fix.** "Guest+"

### 72. LLD paragraph 1449  <sub>serious</sub>

> `meetings` (#116)

The mobile catalogue runs to #116 and then to unnumbered staff rows, and never lists two shipped screens. `src/Mobile/simf_app/lib/app/router.dart` registers `number: 117, name: RouteNames.meetingConfirm, path: '/meeting-confirm'` and `number: 118, name: RouteNames.staffSeating, path: '/staff/seating/:sessionId'`; the screens exist at `lib/features/meetings/meeting_confirm_screen.dart` and `lib/features/staff/staff_seating_screen.dart`; 117 is role-gated to visitor and exhibitor, 118 to `AppRole.staff`, and the server gates the seating endpoints on `PermissionCatalog.Seating.Assist` (`src/Backend/SIMF.Api/Endpoints/Staff/StaffSeatingEndpoints.cs`). Paragraph 151 states that the only screens outside the catalogue are four removed ones (#4, #28, #8, #115), so these two are dropped in silence, which is the failure that paragraph exists to prevent. The mobile `meetingConfirm` is not the Website `/meeting/confirm` row at 1251: that one is the anonymous token page kept by D-774, this one is an in-app deep link from a notification.

**Fix.** "are recorded in the index but are not part of the shipped app and are therefore excluded from the catalogue above. Two further shipped screens are recorded here rather than in the table above: `meetingConfirm` (#117), the other-party meeting confirmation opened from a notification deep link at `/meeting-confirm`, and `staffSeating` (#118), the staff guest-seating desk at `/staff/seating/{sessionId}`, gated on the `Seating.Assist` permission."

---

## 3. Internal contradictions

### 73. HLD paragraph 59  <sub>serious</sub>

> Internal systems, all on site in the HSA zone: an SMTP mail relay, MinIO object storage, the GPT OSS 120B inference endpoint, and SQL Server 2022 (Standard in Development and Test, Enterprise in an AlwaysOn Availability Group in production). None is reached across the internet.

This enumerates the internal systems and asserts that all of them are on site in HSA. The document's other enumeration of the same set, at lines 118 to 123, has five entries: it adds "Syslog / SIEM, centralised structured logging" (line 120). Line 371 then places that entry outside SIMF and outside HSA: "the application hosts ship syslog over TLS on TCP 6514 to the ministry collector. It is an integration rather than a SIMF server, so Figure 1 does not draw it." The two lists therefore disagree on both membership and location. The log collector is the subject of the reviewers' own point 10, and Integration Requirements is the first place they will look for it.

**Fix.** "Internal systems on site in the HSA zone: an SMTP mail relay, MinIO object storage, the GPT OSS 120B inference endpoint, and SQL Server 2022 (Standard in Development and Test, Enterprise in an AlwaysOn Availability Group in production). One further internal integration sits outside SIMF: the application hosts ship syslog over TLS on TCP 6514 to the ministry log collector, which is a ministry service and not a SIMF server. None of these is reached across the internet."

### 74. HLD paragraph 76  <sub>serious</sub>

> Frontend (public website + Control Panel), Backend API, Database.

This names the front end as the public website and the Control Panel only. The mobile app, which is the platform's primary channel, is absent, and so is the mobile presentation tier. It contradicts line 91 ("the mobile app, the public website and the Control Panel"), line 147 ("Presentation tier: SIMF.Web on two web hosts, SIMF.ControlPanel on two admin hosts with session affinity, and SIMF.MobileEdge, all in SSA"), line 219 ("Mobile application (Flutter, iOS + Android), the attendee-facing application") and line 342 ("three front-ends share one design system"). The reviewers' comment about front ends lands on exactly this line, and it is the shortest, most quotable statement of the component set in the whole submission.

**Fix.** "Frontend: the mobile app, the public website, the Control Panel and the on-site badge desk. Backend: one API. Data: two SQL Server databases and MinIO object storage."

### 75. HLD paragraph 115  <sub>serious</sub>

> This is the only outbound call SIMF makes.

The Communication Requirements Matrix marks a second flow Outbound: "All application hosts / Ministry log collector / Syslog / TLS / 6514 / Outbound" (lines 497 to 502). The matrix row above it even says of the caption import "The only outbound flow" (line 496) immediately before that row. The intended distinction is internet egress, which TRUTH.md states as "One internet call remains and the Control Panel makes it", while the syslog flow stays inside the ministry network (line 372, "Neither component traverses the perimeter firewall"). As written the document contradicts its own matrix. Line 130 repeats the same wording.

**Fix.** "It is the only call SIMF makes to the internet."

### 76. HLD paragraph 121  <sub>minor</sub>

> It serves session minutes, question filtering and the assistants.

The list omits subtitle translation, which is the feature the customer asked about. Section 2.4 states the opposite: "the on-site GPT OSS 120B model then translates and summarises it" (line 288), and the field-level inventory carries a "Subtitle translation" row sending "the imported subtitle text and the target language" to the same server (lines 316 to 319). Line 231 also lists subtitle import among the features that "runs on the SITE-hosted GPT OSS 120B model".

**Fix.** "It serves subtitle translation, session minutes, question filtering and the assistants."

### 77. HLD paragraph 251  <sub>serious</sub>

> the Scientific Committee views the text, edits and approves the draft; an administrator publishes it after the session has started

Para 231 assigns the publish step to the Scientific Committee: "Each summary is an automatic draft that the Scientific Committee reviews, approves and publishes before attendees see it." This paragraph assigns it to an administrator. Both cannot be the design. The code sides with para 231: src/Shared/SIMF.Common/PermissionCatalog.cs gives SessionSummaries.Publish the baseline role ScientificCommittee, and PublishSessionSummaryEndpoint gates on that permission. The timing half is correct and should stay: AdminSessionSummaryService.SetPublishedAsync refuses publish when now is earlier than session.Start.

**Fix.** "the Scientific Committee views the text, edits and approves the draft, then publishes it once the session has started"

### 78. HLD paragraph 273  <sub>serious</sub>

> It depends on the same approved firewall exception.

The public video library is a set of links played by the attendee device, so no ministry firewall exception is involved. Para 272 says of that playback "the attendee device plays the configured stream URL directly. The platform stores only the URL, and this traffic does not traverse the SIMF backend", and para 278 says "no video, audio or segment traffic traverses any SIMF server". Para 115 closes it: the caption import "is the only outbound call SIMF makes", so a second server-side dependency on the exception cannot exist.

**Fix.** "and does not change the hardware specification."

### 79. HLD paragraph 530  <sub>serious</sub>

> which is set when an administrator rotates a credential or when a password reaches its maximum age

Line 524, in the same section, lists this as an owner action still open: "enable the identity-lifecycle controls (password expiry / history, dormant-account disable)". The code agrees with 524, not with 530. IdentityLifecycleOptions.PasswordMaxAgeDays defaults to 0, src/Backend/SIMF.Api/appsettings.json ships "PasswordMaxAgeDays": 0, and SignInService.IsPasswordExpired returns false immediately when maxAgeDays <= 0. No password reaches a maximum age today, so the second trigger described here never fires. The mechanism is present and admin-configurable; the paragraph states it as operating.

**Fix.** "Forced change is implemented and enforced. When an account carries the PasswordChangeRequired flag, every token-issuing path is blocked (initial password step, second-factor verify, recovery-code verify, one-time-code verify and refresh) until the user sets a new password through the reset flow. An administrator rotating a credential sets the flag. Password expiry sets it too, and that control ships switched off until the owner sets a maximum password age."

### 80. HLD paragraph 551  <sub>serious</sub>

> System health metrics (CPU, memory, response time, throughput) and database query performance are monitored to support proactive capacity planning, performance tuning and threshold-based alerting that detects degradation early.

Three paragraphs later, line 554 says "The monitoring and alerting toolchain is selected during deployment planning." A toolchain that is not yet selected is not monitoring anything today, so the present tense here cannot be true. Nothing in the build collects these metrics either: there is no OpenTelemetry, no Prometheus and no metrics exporter anywhere under src, and the only runtime signals the system produces are the Serilog files, the /health workers check and the Control Panel services page.

**Fix.** "Health and performance monitoring: the /health endpoint reports readiness. System health metrics (CPU, memory, response time, throughput) and database query performance are collected by the monitoring toolchain, which is selected during deployment planning. Thresholds and alerts are configured there."

### 81. HLD paragraph 574  <sub>serious</sub>

> Outstanding: the secrets present in development configuration history must be rotated, a CA-signed certificate must replace the self-signed certificate, and the mobile development TLS bypass must be removed. These are owner / operations actions and remain open.

This is a second, shorter copy of the open-actions list in section 2.9 and it drops two of the four items. Paragraph 524 reads: 'Owner and operations actions still open: rotate the secrets present in development configuration history; issue a CA-signed certificate and remove the mobile development TLS bypass; enable the identity-lifecycle controls (password expiry / history, dormant-account disable); and commission an independent penetration test.' A reviewer who reads only the risk table concludes three items are outstanding and that the penetration test is already arranged, because the preceding sentence in paragraph 573 offers it as a mitigation in place.

**Fix.** "Outstanding: the owner and operations actions listed in section 2.9 remain open."

### 82. LLD paragraph 117  <sub>serious</sub>

> The mobile privilege enum is `Guest / Visitor / Staff / Moderator`

src/Shared/SIMF.Common/Enums/MobileAppRole.cs declares five values: None = 0, Visitor = 1, Staff = 2, Moderator = 3, Exhibitor = 4. Guest is not one of them; that file states 'The Flutter app's own privilege enum prefixes Guest = 0 for the unauthenticated case'. The glossary of this same document, at line 455, gives a different four: 'The mobile privilege model: None / Visitor / Staff / Moderator'. So the two paragraphs disagree on the first value and both drop Exhibitor, which is live and enforced: src/Backend/SIMF.Infrastructure/Exhibitors/ExhibitorVisitorService.cs line 447 admits only MobileAppRole.Exhibitor, and the app routes /exhibitor/scan and /exhibitor/visitors gate on it. The actor list at lines 514 to 522 has no exhibitor either. In the same sentence, 'those docs' has no antecedent: no document is named in the paragraph.

**Fix.** "and the role/permission model in the Roles and Permissions document (reference 7). `MobileAppRole` has five values, `None / Visitor / Staff / Moderator / Exhibitor`; the Flutter app adds `Guest` for a caller holding no token. `Exhibitor` gates the booth lead-capture screens and the endpoints behind them;"

### 83. LLD paragraph 611  <sub>serious</sub>

> Pick seat, no-overlap rule, reserve seat then confirmed immediately (provisional hold until gate check-in).

One sentence says the reservation is "confirmed immediately" and, in the same breath, that it is a "provisional hold until gate check-in". Both cannot be true, and a reviewer looking for a gap will ask which. The code says the first half and not the second. src/Shared/SIMF.Common/Enums/BookingStatus.cs has four values, Pending, Approved, Rejected, Cancelled, and no checked-in state; its comment reads "a booking is confirmed the moment it is made" and "Approved -- written by EVERY create path. This is the only state a live, held reservation is ever in." Gate check-in writes nothing on the booking: SessionAttendanceService.cs line 209 and GateOperatorService.cs line 165 both read SeatReservations with AsNoTracking(), and check-in is recorded on HallAttendance. Check-in has exactly one effect on the reservation, which is to exempt it from the sweep: ReservationNoShowReleaseWorker.cs releases a held seat whose NoShowReleaseAt ("the session's Start minus 3min") has passed and for which no HallAttendance row exists. So the seat is not held until check-in; it is released three minutes before the session starts if the holder has not arrived. The document repeats the wrong mechanism at paragraphs 97, 102, 103, 134, 180, 182, 258, 795, 801 and 803, and the HLD repeats it at paragraph 248.

**Fix.** "Pick a seat, no-overlap rule, the reservation is confirmed on create. A sweep releases the seat three minutes before the session starts if the holder has not checked in at the hall gate."

### 84. LLD paragraph 619  <sub>serious</sub>

> attendee reservations auto-confirm on create and confirm at gate check-in

A reservation cannot be confirmed on creation and then confirmed again at check-in, and the code has only the first. `src/Backend/SIMF.Domain/SeatReservations/SeatReservation.cs` carries no confirmed or checked-in field, and its `Status` comment reads "a live row is Approved, a released row Cancelled, and nothing writes the other two". Every create path writes `Status = BookingStatus.Approved` and nothing writes it again; check-in changes no field on the reservation at all. What check-in does is written in `SeatReservationService.ReleaseDueNoShowsAsync`, whose candidate filter excludes any reservation for which a `HallAttendance` row exists, with `NoShowReleaseAt` stamped at `ctx.Start - NoShowReleaseGrace` where `NoShowReleaseGrace = TimeSpan.FromMinutes(3)`. Line 795 repeats the two-way reading as "auto-confirm; provisional hold until gate check-in", and line 801 asserts the non-existent transition outright: "marking the held seat confirmed".

**Fix.** "Legacy approve/reject/bulk-approve retained but dormant. An attendee reservation is created in `Approved` and stays in `Approved` until it is cancelled or released."

### 85. LLD paragraph 711  <sub>serious</sub>

> One abstraction, channels in-app/email/SMS/WhatsApp; inbox; reminders (see section 5).

Four channels are stated as delivered. Two are not. `src/Backend/SIMF.Application/Notifications/INotificationChannel.cs` states "The two shipped implementations are InAppNotificationChannel and EmailNotificationChannel. SMS and WhatsApp are deliberately NOT here: both need a procured gateway (owner-action), and shipping a stub that silently drops messages would be worse than shipping none." No SMS or WhatsApp sender exists under `src/Backend/`. The deployment diagram carries no SMS or WhatsApp gateway. This document says so twice elsewhere, at line 207 "the (deferred) SMS/WhatsApp adapters" and at line 849 "providers deferred", so this cell is the drifted copy.

**Fix.** "One abstraction over the two delivered channels, in-app and e-mail, plus the inbox and reminders. SMS and WhatsApp need a procured gateway and are not delivered (see section 5)."

### 86. LLD paragraph 716  <sub>serious</sub>

> Per-day, overall, live attendance, GPS presence (see section 5).

GPS presence is listed as a delivered dashboard figure in two rows of this section while the module section defers it. LLD line 217 reads "The GPS-presence movement and dwell-time views are a deferred enhancement (open item G-OI-2)", and lines 28, 132, 169, 170, 255 and 785 all mark the geofence chain that would feed it as deferred. `src/Backend/SIMF.Infrastructure/Statistics/StatisticsService.cs` contains no GPS, geofence or presence code at all. Line 852 repeats the claim as "GPS-presence dashboards"; its parenthetical "figure list open" qualifies the list of figures, not the deferral, so neither row tells a reviewer that this view does not exist.

**Fix.** "Per-day figures, overall figures and live attendance (see section 5). The GPS-presence views are deferred with the geofence chain, open item G-OI-2."

---

## 4. Undefined terms, and sentences that read two ways

### 87. BRD paragraph 95  <sub>minor</sub>

> The design is frozen after publication.

A one-line constraint that reads two ways. "The design" can be the visual design of the app, which paragraphs 67 and 100 say an external designer supplies, or the solution design that the HLD and LLD carry; and "publication" can be the publication of the app to the stores, of the platform for the event, or of this document. The HLD states the intended constraint at its paragraph 77 as "Hard event deadline with a post-publish change freeze", which is about the delivered platform, not about a visual design.

**Fix.** "The delivered solution is frozen against change once it is published for the event."

### 88. BRD paragraph 98  <sub>serious</sub>

> the call is made from the Control Panel in SSA

SSA is used as if it were defined and is defined nowhere. It appears twice in this document (here and in the revision row at paragraph 128, plus HSA at paragraphs 546 to 580) and the Glossary, which states at paragraph 28 that "The terms and abbreviations used are listed below", has no entry for it; neither does the HLD or the LLD expand it anywhere. A business reader cannot tell whether SSA is a network zone, a security area, a server or a site, and the sentence is a constraint the ministry is being asked to accept. The placement of the Control Panel is already stated as a requirement at paragraph 552, so the abbreviation carries nothing here.

**Fix.** "and the call is made from the Control Panel. The AI runs on site and needs no external access."

### 89. HLD paragraph 79  <sub>minor</sub>

> SQL Server 2022, Standard edition in Development / Test (decision O-3), Enterprise edition in an AlwaysOn Availability Group for the production event topology (owner decision).

"decision O-3" is offered as a source and is defined nowhere in the four delivered documents. It appears here and, as "(O-3)", at line 123, and in no other paragraph of the HLD, the LLD or either BRD edition, so a ministry reviewer cannot resolve what O-3 says or who took it. The same sentence already labels the other half of the choice "(owner decision)", which is the only information the citation carries to this audience.

**Fix.** "Standard edition in Development / Test, Enterprise edition"

### 90. HLD paragraph 131  <sub>serious</sub>

> SSA zone. A web application firewall and load balancer terminates TLS, filters traffic against the OWASP rule set and balances requests across healthy nodes. The presentation tier sits here and nothing else

Under a heading that says SSA, "here" cannot mean SSA: line 128 states "SSA holds the access, perimeter and presentation zones", line 158 places the three client devices and the WAF in SSA, and the sentence immediately before this one puts the WAF itself here. So the paragraph says the WAF is in SSA and that nothing but the presentation tier is in SSA. The final sentence, "The Control Panel makes the single outbound call to YouTube", is also the third statement of that fact in three consecutive paragraphs (lines 115, 130, 131).

**Fix.** "SSA zone. A web application firewall and load balancer in the perimeter zone terminates TLS, filters traffic against the OWASP rule set and balances requests across healthy nodes. The presentation zone holds the presentation tier and nothing else: the website (SIMF.Web, two nodes), the Control Panel (SIMF.ControlPanel, two nodes with session affinity) and the mobile edge (SIMF.MobileEdge). All three reach data only through the API, over HTTPS 443 across the internal firewall."

### 91. HLD paragraph 195  <sub>serious</sub>

> Stateless REST; runs the scheduled jobs in-process. No separate worker

The same table row says Instances 4, so the reader is told four nodes each run the scheduled jobs in-process and is told nothing about how a reminder is not sent four times. This is the question the customer already raised in writing about the background worker. The build has an answer the documents never state: src/Backend/SIMF.Infrastructure/Operations/WorkerLease.cs "Elects exactly one API instance to run the background workers" using a SQL Server application lock on resource "SIMF.BackgroundWorkers", re-checked every 30 seconds, and 13 workers are registered through AddLeasedHostedService (DependencyInjection.cs lines 420 to 451 plus Program.cs lines 124 and 129). Nothing in the HLD or the LLD mentions the election; HLD lines 240, 366 and 553 describe the jobs without it.

**Fix.** "Stateless REST; runs the scheduled jobs in-process. A SQL Server application lock elects one node to run the timed sweeps and reminders, so four nodes do not repeat them. No separate worker"

### 92. HLD paragraph 231  <sub>minor</sub>

> Every feature runs on the SITE-hosted GPT OSS 120B model on the on-site LLM server.

"SITE-hosted" and "on-site" sit in one sentence and read as one claim made twice, because the document never expands SITE and has no abbreviations section. It is used seven times (paras 72, 102, 112, 121, 231, 267, 285, 583) and the surrounding text uses "on site" as ordinary words, so a reviewer cannot tell whether SITE names a hosting organisation or means "on the site". Where the model runs is already stated at para 121.

**Fix.** "Every feature runs on the GPT OSS 120B model on the on-site LLM server."

### 93. HLD paragraph 250  <sub>serious</sub>

> Once the session is live, a hall that carries a geofence also requires a hall-arrival record; the geofence is a deferred enhancement (open item G-OI-2).

The sentence gates a delivered feature on a deferred one and omits the ordinary case, so a reviewer cannot tell whether an attendee can ask a question during a live session. Two facts are missing, and the LLD carries both. LLD para 194: "a hall with no geofence accepts the question because presence cannot be verified". LLD para 170: "Hall arrival is recorded by QR scan at the hall door." The code agrees: src/Backend/SIMF.Infrastructure/SessionQuestions/SessionQuestionService.cs computes atVenue = !isLive || !session.HasGeofence || HallAttendances.AnyAsync(...), so a hall with no geofence configured accepts the question, and HallAttendanceService.RecordQrArrivalAsync / RecordGateDoorScanAsync write the same HallAttendance row the gate reads.

**Fix.** "Once the session is live, a hall that carries a geofence also requires a hall-arrival record; the QR scan at the hall door writes that record. A hall with no geofence accepts the question, because presence cannot be verified. Automatic arrival by GPS geofence is a deferred enhancement (open item G-OI-2)."

### 94. HLD paragraph 274  <sub>serious</sub>

> No inbound interface is exposed to any external entity.

Read literally this is disprovable, and a security reviewer will read it literally. Figure 6 permits inbound HTTPS 443 through the perimeter firewall from the attendee mobile device and the public website browser, and para 57 states that the forum is open to the general public and that registration is not restricted to Ministry of Defense personnel, so external people do reach an inbound interface. What is true, and what the section is about, is that no external system integrates inbound.

**Fix.** "No external system integrates inbound with SIMF."

### 95. HLD paragraph 342  <sub>serious</sub>

> UI Overview: three front-ends share one design system and are fully bilingual (Arabic primary / RTL, English secondary / LTR).

The UI design section describes three interfaces; the system has four client applications. Paragraph 222 lists 'Badge desk (SIMF.BadgeDesk, WinForms), an on-site desk tool used by staff to print and issue badges at the venue', paragraph 256 says the component view shows 'four clients', and the LLD paragraph 53 names the same four. The project is real: src/Tools/SIMF.BadgeDesk with its own test project. Section 2.6 never mentions it, so a reviewer who has already raised four user groups against three front ends cannot tell whether the badge desk has a user interface, was forgotten, or is out of scope.

**Fix.** "UI Overview: three front-ends share one design system and are fully bilingual (Arabic primary / RTL, English secondary / LTR). The badge desk of section 2.2 is a fourth client with its own WinForms interface."

### 96. HLD paragraph 363  <sub>serious</sub>

> Provision the shared file store and enable session affinity for the website and Control Panel tiers.

'The shared file store' is defined nowhere in the document, and the two things it can mean sit in different security areas. The only file store the document defines is MinIO, which is in the HSA data zone and is reached only by the API (paragraph 358), so no website or Control Panel node can use it, and the deployment diagram draws no shared storage in SSA at all. The prerequisite those two tiers do have is a shared data-protection key ring: src/ControlPanel/SIMF.ControlPanel/Program.cs and src/Website/SIMF.Web/Program.cs persist keys to DataProtection:KeyRingPath and both refuse to start outside Development without it ('without a shared key ring antiforgery tokens are valid on one instance only'), and deploy/set-env-cp.ps1 notes 'both must share ONE ring'. As written, the site team cannot tell what to provision, and the one item that stops the two-node tiers starting is not named anywhere in the document.

**Fix.** "Provision MinIO object storage in the HSA zone. Provision one shared data-protection key ring folder that both website nodes and both Control Panel nodes can read and write. Enable session affinity for those two tiers. Neither site starts in production without the key ring."

### 97. HLD paragraph 405  <sub>minor</sub>

> The share is not reachable from any client, from the website or from the Control Panel.

There is no share. The store is MinIO object storage reached over the S3 API, as the same answer says three sentences earlier at paragraph 402, and the LLD records that the file store moved 'from a directory on a file share to MinIO object storage in HSA' (LLD paragraph 413). The deployment diagram draws no file share. Calling it 'the share' here, and 'the shared file store' at paragraph 363, leaves the reader with a file server the design removed.

**Fix.** "MinIO is not reachable from any client, from the website or from the Control Panel."

### 98. HLD paragraph 523  <sub>serious</sub>

> No data of any kind is sent to a cloud artificial-intelligence service; what may be sent is session content to summarise and question text to filter, carrying no attendee identity.

The second clause names no destination, so "what may be sent" reads as a qualification of the cloud service the first clause has just ruled out. The technology team's first written comment was about the AI model and its endpoint, so this is the sentence they read hardest, and it is the one that reads two ways. "carrying no attendee identity" is also wider than the code supports: AiService.ExecuteAsync substitutes the caller's raw inputs into the system and user prompts and calls the provider before any redaction, the redaction running afterwards on the persisted audit record, so a name typed into a question is sent as typed. What the call carries no identifier of is the account: AiProviderCall holds model, system prompt, user prompt, temperature and output-token cap, and nothing else.

**Fix.** "No data is sent to a cloud artificial-intelligence service. The on-site model receives session content to summarise and question text to filter, and the call carries no account identifier."

### 99. HLD paragraph 566  <sub>serious</sub>

> Monitoring and auto-scaling: peak-shaped load tests are planned for staging, covering registration surge, live-session concurrency, scan bursts, notification fan-out and mixed steady state. Performance dashboards track system metrics so thresholds can trigger scaling action during peak usage.

'Auto-scaling' and 'thresholds can trigger scaling action' read two ways, and only one of them is buildable here. Nothing in the estate can add a node by itself: Figure 1 and the sizing table fix the counts (four API, two web, two Control Panel, two database hosts, paragraph 86), the hosts are physical Windows Server 2022 machines, and no orchestrator, container platform or autoscaler is named anywhere in the HLD or the LLD. Paragraph 556 says the opposite in the same section: 'scaling out is a deployment change rather than a redesign'. A capacity reviewer reading this row cannot tell whether the event day has automatic headroom or needs an operator. 'are planned for' also states an intention rather than a design fact.

**Fix.** "Monitoring: peak-shaped load tests run in staging before the event, covering the peak-day load described in section 2.8 and a mixed steady state. Performance dashboards track system metrics and alert when a threshold is crossed. No tier scales itself; capacity is added by deploying another node."

### 100. LLD paragraph 30  <sub>serious</sub>

> run on site in HSA

HSA is used here for the first time in the document and is never expanded. The Definitions and Acronyms table (paragraphs 420 to 465) defines API, CP, BFF, DDD, JWT, TOTP, OTP, RBAC, PII, RTL/LTR, NCA, SSR, SoT, ApiResult, PermissionCatalog, GridQuery, MobileAppRole, EF Core, soft delete, logical FK, RSNF and SIMF. It defines neither HSA nor SSA. Neither does the HLD, and neither does the BRD, which puts both terms in front of a business audience at its lines 128 and 546 to 552. The two security areas are the whole of the phase-one hosting answer and the reader is never told what the letters mean or which one holds what.

**Fix.** "The AI model, the mail relay and the object store run on site in HSA, the security area that carries internal traffic only and has no internet path, and they are described in this document like any other server."

### 101. LLD paragraph 102  <sub>serious</sub>

> a pre-start sweep auto-releases any hold not checked in shortly before the session starts.

The rule is exact in the code and vague here. src/Backend/SIMF.Infrastructure/SeatReservations/SeatReservationService.cs line 45 sets NoShowReleaseGrace = TimeSpan.FromMinutes(3) and stamps NoShowReleaseAt at the session start less that grace; ReservationNoShowReleaseWorker polls once a minute. Two exemptions are also unstated: a reservation created after the deadline is skipped (CreatedAt < NoShowReleaseAt, line 1451) and a walk-in hold carries a null deadline so it is never released. An attendee who can lose a seat needs the number, and paragraphs 182 and 801 repeat the same vague phrase.

**Fix.** "a sweep runs every minute and releases any hold whose holder has not checked in by three minutes before the session starts. A reservation made after that deadline is exempt, and so is a walk-in hold recorded at the door."

### 102. LLD paragraph 303  <sub>minor</sub>

> UserProfile, SIMF_App. Bilingual `AttendeeProfile` as built.

The sentence reads two ways: that `AttendeeProfile` is the as-built name, or that `UserProfile` is what `AttendeeProfile` became. The first reading contradicts paragraph 283, "`AttendeeProfile` to `UserProfile`", and the delivered table, which the App `InitialCreate` migration creates as `UserProfiles`. Every neighbouring heading uses the opposite order, as-built first and logical in brackets, for example paragraph 305 "SeatReservation, SIMF_App (`Booking`)", so a reader carrying that pattern across takes `AttendeeProfile` for the delivered name.

**Fix.** "UserProfile, SIMF_App (`AttendeeProfile`). Bilingual."

### 103. LLD paragraph 331  <sub>minor</sub>

> Where the ground-truth inventory does not carry an explicit version for a technology, it is marked accordingly.

"ground-truth inventory" is used as if it were a defined artefact, here and again at paragraph 363 ("Only packages present in the ground-truth inventory are listed"), and it is defined nowhere in the document. A reviewer cannot tell whether it is a file they should have been given, a tool output, or another name for the repository itself. Both sentences are also authoring method rather than design.

**Fix.** "Versions below are read from the repository. A technology with no pinned version is marked as such."

### 104. LLD paragraph 631  <sub>minor</sub>

> Scan then verify then record `GateScan`/`VenueEntry`.

`VenueEntry` is presented in backticks beside `GateScan`, which is a real table, so a reader takes both for as-built stores and looks for the second one. It is not in the solution: `VenueEntry` returns no match anywhere under `src/` or `tests/`. `GateScan` is the venue-entry record, and this same use case's Postconditions at line 789 name only "Append-only `GateScan` row". The name survives from the logical model at lines 254 and 255, but this section describes as-built behaviour with as-built identifiers (`GateScan`, `HallAttendance`, `GateProfileTypeAllow`), so mixing one logical-only name in gives a reviewer a table to hunt for that no migration creates.

**Fix.** "Scan then verify then record a `GateScan` row."

### 105. LLD paragraph 706  <sub>serious</sub>

> Attendee request to admin review against hall availability to speaker double-opt-in (build-ready).

"build-ready" is used three times in this document (lines 229, 706, 846) and defined nowhere. A reviewer looking for a gap reads it as "not built", and here that reading is wrong: every leg of the flow is shipped. The attendee submits through `POST /app/speakers/{speakerId:guid}/meeting-requests` (`SpeakerMeetingRequestEndpoints.cs`). The admin binds it to a free hall slot through `PUT /admin/speaker-meeting-requests/{id:guid}/respond`, which injects `IHallAvailabilityService`. The speaker double-opt-in runs on `MeetingActionTokenService` tokens and the website page `@page "/meeting/confirm"`, with `MeetingAwaitingSpeakerExpiryWorker` timing it out. Line 846 carries the same undefined term.

**Fix.** "Attendee requests a meeting with a speaker. An admin reviews it and binds it to a free hall slot. The speaker then confirms through an e-mailed link."

### 106. LLD paragraph 896  <sub>minor</sub>

> Register VVIP/VIP (Mawj fields, VIP photo); creates pending account

"Mawj fields" is used as if the reader knows what Mawj is. The word appears exactly once in all four delivered documents (`grep -n -i mawj` over the HLD, LLD, BRD EN and BRD AR returns this line only) and it is absent from section 1.3 Definitions and Acronyms. It is an external system: `src/Backend/SIMF.Api/Endpoints/Admin/AdminVipRosterEndpoints.cs` describes `GET /admin/visitors/vip/roster` as "The JSON feed of the VVIP/VIP welcome roster the Mawj integration / technical teams consume" and names the fields as "Mawj id, honorific, preferred language, the welcome-photo flag". A ministry reviewer reading a design document cannot tell whether Mawj is a SIMF module, a data format or a third-party system the platform talks to, and the second reading would be a network integration that appears on no diagram.

**Fix.** "Register a VVIP or VIP, capturing the welcome-roster fields the external Mawj welcome system consumes (Mawj id, honorific, preferred language, welcome-photo flag) plus the VIP photo. Creates a pending account."

### 107. LLD paragraph 1017  <sub>minor</sub>

> Team-defined availability windows to VIP free slots

The cell is not a sentence and reads two ways. The source row in `docs/pages/PAGE-INDEX.md` line 77 is "team-defined availability windows to VIP free slots" with an arrow, and the arrow has been flattened to the word "to", which destroys the meaning: a reviewer cannot tell whether the windows are copied to the VIP free slots, or generate them, or are restricted to them. The same flattening damaged paragraph 1021, "Team-defined hall meeting-time windows to free slots" (PAGE-INDEX line 78 uses the same arrow). Both cells describe pages a ministry reviewer would test, so neither can be left as a fragment.

**Fix.** "Team-defined speaker availability windows that generate the free slots offered for VIP meeting requests"

---

## 5. Repetition and filler

### 108. BRD paragraph 28  <sub>minor</sub>

> Defining terms improves the precision of communication and understanding across the document.

A sentence that carries no fact about SIMF, its scope or its terms. It tells the reader why glossaries exist before the glossary that follows. The second sentence does the whole job.

**Fix.** "The terms and abbreviations used in this document are listed below."

### 109. BRD paragraph 37  <sub>serious</sub>

> What is needed is one dynamic, configurable system and app, built on modern technology, embedding AI and emerging technologies, and compliant with the National Cybersecurity Authority (NCA) standards, that can serve the current edition and every future edition without being rebuilt, with all editions' data held in one central location.

Section 2.1 Problem Description contains the current situation and the proposed solution as well as the problem, so a reader meets all three twice. Its second sentence, "Each previous edition ran on its own separately built systems, website and app, produced from scratch for that edition and then set aside", is paragraph 39 in section 2.2 reworded: "Each edition of the forum has been delivered on its own bespoke systems, website and app, built specifically for that edition". Its fourth sentence, quoted above, is paragraph 41 in section 2.3 plus objectives 43 to 46. Three consecutive sections open by saying the same thing.

**Fix.** "Rebuilding the systems, the website and the app from scratch for every edition is costly and slow, scatters each edition's data in a different place, leaves no single reusable platform and no consolidated record across editions, and forces the cybersecurity controls, the technology choices and any intelligent features to be worked out again every time."

### 110. BRD paragraph 41  <sub>serious</sub>

> Everything that varies between editions is configured from the Control Panel: the event name, the colours, the logos and visual identity, the content and data, the categories, the start and end dates, and the archiving of past editions.

Three of this paragraph's six sentences are repeated as the four bullets that immediately follow it. Sentences 2 and 3 become paragraph 43 ("with everything that changes between editions (name, colours, logos, content, categories, start and end dates, archiving) set from the Control Panel") and paragraph 44 ("Hold all editions' data in a single central location"); sentence 4 becomes paragraph 45 (the AI list) and paragraph 46 (NCA). The configuration list then appears a third time in the scope bullet at paragraph 53 and a fourth as a constraint at paragraph 92. The customer's review said there is too much text; this is the clearest instance of it in section 2.

**Fix.** "The platform covers"

### 111. BRD paragraph 62  <sub>minor</sub>

> the platform stores links for the library and holds no library video files. The public library is published on the forum’s YouTube channel and surfaced in the platform as links

One fact, links rather than files, is stated twice inside one bullet: "stores links for the library and holds no library video files", then "surfaced in the platform as links, so it needs no additional storage". The consequence is worth keeping; the second statement of the premise is not.

**Fix.** "The video library: the platform holds no library video files. The library is published on the forum’s YouTube channel and carried in the platform as links, so it needs no storage, encoding or bandwidth on the owner’s servers."

### 112. BRD paragraph 101  <sub>serious</sub>

> The WhatsApp provider is deferred and reached through an abstraction. The AI model is GPT OSS 120B, hosted by SITE on an on-site LLM server, and the live-broadcast platform is YouTube. The model needs no network exception because it runs on site; the caption import does.

Section 5.6 Assumptions restates section 2.3.3 Out of Scope item for item. Paragraph 100 is paragraph 67 ("The visual design of the mobile app; supplied by the external UI/UX designer") in reverse word order. Paragraph 101 is paragraph 64 ("Procuring the WhatsApp provider; it is reached through an abstraction ... The AI model (GPT OSS 120B, hosted by SITE on site) and the live-broadcast platform (YouTube) are decided") plus paragraph 98. Paragraph 102 is paragraph 65 ("Authoring the legal text (Terms and Conditions and Policies); it is supplied by the owner") with the party renamed from owner to client, which is a drift, not a distinction. The AI-runs-on-site fact is now stated four times in this document, at paragraphs 64, 98, 101 and 588. Nothing in these three paragraphs is an assumption; they are decisions already recorded elsewhere.

**Fix.** Delete it.

### 113. HLD paragraph 82  <sub>minor</sub>

> •  The one external integration, the YouTube caption import made from the Control Panel, depends on an approved outbound firewall exception. Until it is granted an administrator pastes or uploads the transcript. The AI runs on site and needs no external access.

The YouTube caption import is described in full four times before section 2 begins: line 1, line 60, line 82 and line 115, then twice more at lines 130 and 131. Line 60 already carries every fact this bullet carries, including the fallback: "it needs an approved outbound HTTPS 443 exception. Without that exception an administrator pastes or uploads the transcript, and no feature is blocked." The trailing sentence "The AI runs on site and needs no external access" is stated at line 59, line 121, line 130 and line 132. Only the dependency on the firewall exception belongs in a constraints list; the rest is a second copy of the integration description. Line 115 is a third copy of the same paragraph.

**Fix.** "•  The one external integration, the YouTube caption import, depends on an approved outbound HTTPS 443 exception."

### 114. HLD paragraph 103  <sub>serious</sub>

> •  The system must comply with the NCA Secure Application Development Standard.

The "High-level assumptions and constraints" list at lines 100 to 106 repeats the "Architecture Constraints" list at lines 75 to 82, item for item, two pages later. Line 103 repeats line 78 ("Mandatory NCA security compliance and source-code handover") and line 69 ("Compliance: aligned to the NCA Secure Application Development Standard"), so the NCA obligation is stated three times inside forty paragraphs. Line 104 repeats line 81 ("Arabic primary (RTL) and English secondary throughout"). Line 105 repeats line 80 ("Two physically separate databases with no cross-database foreign keys, transactions or duplicated live data"). Line 106 repeats the second half of line 78. Four of the six bullets carry no fact the reader has not already been given. The reviewers named the volume of text as one of their complaints, and this is the clearest instance in the opening sections. Lines 101 and 102 are left in place because each adds a fact the earlier list does not carry.

**Fix.** Delete it.

### 115. HLD paragraph 111  <sub>minor</sub>

> The website carries no registration; visitors register in the mobile app.

The bullet immediately above, line 110, already says this and says more: "Registration and every authenticated journey are in the app; the website carries no sign-in, no registration and no personal data." Line 221 states it a third time. The sentence adds nothing to the anonymous-public bullet, whose subject is what that user can read. Two adjacent bullets carrying the same negative claim is the pattern the reviewers described as too much text.

**Fix.** "(home, programme, visit information)."

### 116. HLD paragraph 139  <sub>minor</sub>

> Development and test environments run a single SQL Server Standard node; the Enterprise Availability Group is the production topology.

Both halves are already stated. Line 123 says "Standard edition, single node, in Development / Test (O-3); Enterprise edition in an AlwaysOn Availability Group for the production event topology", line 132 describes the production group in detail, the sizing table repeats it at lines 206 to 210, and line 149 states the dev and test footprint again ten lines later in the same section.

**Fix.** Delete it.

### 117. HLD paragraph 143  <sub>minor</sub>

> A stateless API tier, so the platform scales out by adding nodes.

Line 137, six lines earlier in the same section, says "The application tier is elastic. The API is stateless, so capacity is added by adding nodes"; line 132 already says "four stateless nodes" and line 195 says "Stateless REST". The bullet adds no fact the section has not made twice.

**Fix.** Delete it.

### 118. HLD paragraph 253  <sub>minor</sub>

> These are the primary system workflows.

The sentence carries no fact. The workflows are the seven paragraphs immediately above it and the section heading at para 244 already names them.

**Fix.** Delete it.

### 119. HLD paragraph 345  <sub>minor</sub>

> It has no sign-in, registration or account page and stores no personal data.

A word-for-word copy of the same sentence in paragraph 221, where it is stated with its scope ('registration and all visitor data live in the mobile app and the Control Panel') and its two exceptions. Repeating it in the UI section adds nothing, and the sentence that follows here already names the same two exceptions.

**Fix.** "for speed. The only interactive elements"

### 120. HLD paragraph 352  <sub>minor</sub>

> A single node runs the three application sites and one SQL Server Standard instance, with OpenAPI enabled. This is the right footprint for these environments.

'This is the right footprint for these environments' asserts nothing a reader can act on or check, and it is a second copy of paragraph 149's 'which is the intended footprint for those environments'. The site count also repeats the error corrected at paragraph 365: the delivery has four sites, not three.

**Fix.** "A single node runs the application sites and one SQL Server Standard instance, with OpenAPI enabled."

### 121. HLD paragraph 369  <sub>serious</sub>

> SIMF response. They differ, and Figure 1 shows the difference. The mail relay is an on-site server in the HSA zone and is drawn as its own box: SIMF.Api reaches it over SMTP with STARTTLS on TCP 587, and nothing else calls it. The log collector is an integration rather than a SIMF server, so it is not drawn; the application hosts ship syslog over TLS on TCP 6514 to the ministry collector. Both car

The mail relay and the log collector are described three times across four paragraphs. 369 gives the relay's zone, protocol, port and sole caller and the collector's protocol, port and non-drawn status; 370 repeats the relay's zone, protocol and port; 371 repeats the collector's protocol, port and non-drawn status word for word ('the application hosts ship syslog over TLS on TCP 6514 to the ministry collector', 'so Figure 1 does not draw it'); 372 then repeats 'does not use the perimeter firewall' from 370 and the section 2.8 pointer from 369. Paragraphs 370 and 371 carry the detail and the facts that are stated once. This is the repetition the technology team named.

**Fix.** "SIMF response. They differ, and Figure 1 shows the difference. The mail relay is a SIMF server and is drawn as its own box. The log collector is a ministry integration, so it is not drawn. Both carry their protocol and port in the communication requirements matrix in section 2.8."

### 122. HLD paragraph 396  <sub>minor</sub>

> Audit trails: every insert, update and delete is captured in RowAudit with before / after images, an actor snapshot and a correlation ID; security-relevant business events are captured in OperationLog. Both are append-only.

A third statement of what paragraphs 336, 337 and 339 already say in section 2.5: OperationLog for security-relevant business events, RowAudit for row-level changes with before and after images, actor snapshot and correlation ID, both append-only. It adds nothing, and a data-integrity bullet only needs the pointer.

**Fix.** "Audit trails: the RowAudit and OperationLog trails described in section 2.5."

### 123. HLD paragraph 402  <sub>minor</sub>

> Database rows hold object keys, never blobs.

Paragraph 391, in the same section, already carries both facts: 'Database rows hold object keys, not blobs' and 'Four categories are encrypted at rest with application-level AES-GCM: avatars, identity documents, VIP photos and speaker presentations'. Paragraphs 402 and 404 restate them in slightly different words a few lines later, which makes a careful reader stop and check whether the two versions actually differ. The one fact 404 adds and 391 does not carry is why session recordings are excluded, and that should stay.

**Fix.** "another node wrote."

### 124. HLD paragraph 540  <sub>minor</sub>

> Development and test use a single Standard-edition node.

The same fact appears six other times in this document: line 59 ("Standard in Development and Test"), line 79, line 123, line 139 ("Development and test environments run a single SQL Server Standard node"), line 352 and line 390 ("Development and test use a single Standard-edition instance"). Line 559 carries a seventh copy, "Development and test use a single Standard node." It is neither an availability fact nor a sizing fact, and sections 2.1 and 2.7 already state it where the environments are described.

**Fix.** "A read-write primary and one readable secondary run with synchronous commit and automatic failover. Both databases run in the group; the listener routes writes to the primary and read-only queries to a secondary. The cluster quorum uses a witness configured with the site."

### 125. HLD paragraph 543  <sub>serious</sub>

> Load Balancer. Distributes traffic across the nodes to prevent overload and keep service continuous.

Four paragraphs, 543 to 546, that carry no fact the reader has not already been given. Line 516 in the section above: "Load Balancer. Distributes traffic across healthy nodes, centralises TLS termination, and health-checks each node." Line 542, immediately above these bullets: "Adding nodes adds capacity with no redesign." Line 547, immediately below: the probe is "used by the load balancer and monitors to pull unhealthy instances out of rotation". Line 557 says the web tier "scales horizontally behind the load balancer by adding nodes". Distribution, health checks, failover rerouting and add-a-node scalability are each stated at least twice elsewhere. This is the repetition the technology team objected to by name.

**Fix.** Delete it.

### 126. HLD paragraph 580  <sub>serious</sub>

> Mitigation Strategies: a stateless API tier for horizontal scale-out; client polling on a bounded interval for live data, with no server-push transport in this build; indexed, projected and server-paged queries; asynchronous e-mail dispatch; and application-layer caching. Load-test to peak shapes in staging and monitor via dashboards, scaling on usage thresholds.

The paragraph restates the Solution Sizing and Performance View item by item, twenty lines after it. 'a stateless API tier for horizontal scale-out' repeats 558 and 562; 'client polling on a bounded interval ... no server-push transport' repeats 564 word for word; 'indexed, projected and server-paged queries' repeats 561; 'asynchronous e-mail dispatch' repeats 563; 'application-layer caching' repeats 565; 'Load-test to peak shapes in staging and monitor via dashboards' repeats 566. The preceding line does the same to its neighbour: 579's 'Registration surge, live-session concurrency, scan bursts and notification fan-out coincide' is 576's sentence again, and the fourth appearance of that list in the document after 399 and 566. This is the repetition the customer named in the meeting.

**Fix.** "Mitigation Strategies: the scaling, query, e-mail and caching measures set out in the Solution Sizing and Performance View above, and the staging load test that runs before the event."

### 127. HLD paragraph 583  <sub>minor</sub>

> The provider sits behind one abstraction, so the endpoint is a configuration value. The only outbound route in the estate belongs to the Control Panel, for the YouTube caption import.

'The AI needs no egress' is stated three times in three consecutive paragraphs: 582 'inference runs on the on-site model inside HSA and needs no egress at all', 583 'no inference egress', 584 'The AI features require no egress at all'. The Control-Panel-is-the-only-outbound sentence is likewise the second copy in two paragraphs, after 582, and is said again in the annex at 641. 'the estate' is also used as if defined and is defined nowhere in the document.

**Fix.** "The endpoint sits behind one abstraction, so it is a configuration value."

### 128. LLD paragraph 8  <sub>minor</sub>

> Right-click and “Update Field” to build the table of contents.

An authoring instruction shipped to the customer in place of a table of contents. It tells the ministry reviewer to operate the authors' word processor, and it is the second line they read after the contents heading.

**Fix.** Delete it.

### 129. LLD paragraph 12  <sub>minor</sub>

> The document is the reference baseline against which development is carried out and against which the delivered system is reviewed, it is detailed enough to implement against and to review against.

The second clause repeats the first in different words, joined by a comma splice, and paragraph 6 has already said it a third time: 'it is a reference baseline for development, review and QA'.

**Fix.** "The document is the reference baseline against which development is carried out and against which the delivered system is reviewed."

### 130. LLD paragraph 63  <sub>minor</sub>

> Real-time note (as-built): live updates are delivered over REST, polled by the client on a bounded interval. This section describes that REST behaviour. No server-push transport ships in this build.

This repeats paragraph 25 almost word for word: 'Real-time push. Live notifications and Q&A are delivered over REST, polled by the client on a bounded interval. No server-push transport ships in this build.' The only added words are 'This section describes that REST behaviour', and section 2.1.3 describes the two databases and the scheduled jobs, so it does not.

**Fix.** Delete it.

### 131. LLD paragraph 104  <sub>minor</sub>

> Gate check-in confirms the seat (checked-in); no admin approval transition and no booking notification are raised on the reservation path.

The sentence carries no fact the paragraphs either side have not already carried. Paragraph 103 has 'The attendee checks in at the hall gate (staff scans their QR), which marks the held seat confirmed (checked-in)', and paragraph 105 has 'no booking notification is queued or delivered'. It also sits unnumbered between steps 6 and 7 of a numbered flow, so a reader counts eight steps in a seven-step list.

**Fix.** Delete it.

### 132. LLD paragraph 139  <sub>minor</sub>

> The exhaustive per-page and per-endpoint realisation of every use case above (routes, permissions, buttons, bilingual states) is maintained in the per-page reference documentation and the Gherkin end-to-end test catalogue.

The same section already says this at paragraph 121: 'the exhaustive per-page and per-action inventory covers the Control Panel, Website, and roughly 34 mobile screens, together with the per-page E2E catalogue'. Paragraph 141 then says it a third time for section 4, and paragraphs 6 and 22 say it twice more in section 1. The customer named repetition as a complaint.

**Fix.** Delete it.

### 133. LLD paragraph 169  <sub>minor</sub>

> hall-arrival capture by QR-at-door (automatic GPS-geofence arrival is a deferred enhancement, open item G-OI-2)

The same deferral is stated twice inside section 5.3, once here and again in the next paragraph at 170: "Hall arrival is recorded by QR scan at the hall door. Automatic GPS-geofence arrival is a deferred enhancement (open item G-OI-2)." Both carry the same fact and the same open-item reference, one paragraph apart.

**Fix.** Delete it.

### 134. LLD paragraph 181  <sub>minor</sub>

> (Legacy) Control-Panel booking approval/rejection queue, retained but dormant; attendee reservations auto-confirm;

Section 5.5 says the approval queue is dormant and reservations auto-confirm three times in four paragraphs: here, at 182 ("A booking is created `Approved` (reservation-only auto-confirm)") and in full at 183 ("The admin `/admin/bookings/{id}/approve|reject` endpoints (and BookingConfirmed/BookingRejected kinds) are retained but dormant; attendee reservations auto-confirm and raise no booking notification"). The copy at 183 is the only one that carries extra facts, the endpoint names and the absence of a notification.

**Fix.** Delete it.

### 135. LLD paragraph 199  <sub>minor</sub>

> raising a recommendation plus push at greater than or equal to 80%

The 80% threshold is stated three times: here, in the business rules at 200 ("a recommendation triggers at a score greater than or equal to 80%"), and again in the data model at 267 ("a score of 80 or more triggers a recommendation + push"). A threshold is a business rule and belongs in one place; the two copies add no fact and are two more places to update when the number changes.

**Fix.** "matchmaking with a match score and reason from shared interests/sessions, raising a recommendation plus push;"

### 136. LLD paragraph 214  <sub>minor</sub>

> an uploaded content-block image is type- and size-restricted

The identical sentence closes the error handling of module 5.12 at 226, "An uploaded content-block image is type- and size-restricted." Content blocks belong to Control Panel Configuration, which is where 5.12 sits and where 223 lists them as its own functionality, so the copy here in the Media, News and Archive module is the drifted one.

**Fix.** "Create, edit, deactivate and visibility changes are written to the operation log."

### 137. LLD paragraph 315  <sub>serious</sub>

> All facts are grounded in the current source tree and the deterministic project/package inventory read from the repository. Where a source marks something as reserved or not-yet-implemented, this section says so rather than inventing detail.

Three sentences carrying no fact about the system. The first restates the headings that follow immediately (7.1 Logical View, 7.2 Solution Structure, 7.2.5 Patterns, 7.2.6 Packages Inventory). The second and third are the authors assuring the reader of their own method, and the second is disprovable on this evidence: paragraphs 337, 343, 355 and 358 in this same section each name a folder, path, interface or environment variable that is not in the source tree. This is the padding the customer named.

**Fix.** "This section describes the SIMF technical architecture: the logical view, the solution structure, the patterns applied throughout, and the third-party package inventory."

### 138. LLD paragraph 319  <sub>serious</sub>

> Phase one places every service that holds or processes SIMF data inside HSA: the API servers, the databases, the AI model, the mail relay and the file store.

The whole paragraph is said again in paragraph 320, which follows it immediately: HSA holding the API, databases, AI and mail; GPT OSS 120B on the on-site LLM server; the on-site mail relay; MinIO over the S3 API; "HSA has no internet path" against "No host in HSA has a route to the internet"; and the YouTube caption fetch from the Control Panel, twice. The normative truth table for Figure 3 states the phase-one summary once and says the prose describes it exactly once. Paragraph 320 is the complete description, so 319 is the copy to delete. The only fact 319 carries that 320 does not is that the LLM is reached over an OpenAI-compatible API, which belongs in 320.

**Fix.** Delete it.

### 139. LLD paragraph 332  <sub>minor</sub>

> the repository's package inventory does not pin a MudBlazor package (a single stray razor reference aside), so the UI foundation is recorded here as `SIMF.Components` plus `Cropper.Blazor` rather than MudBlazor

This is the authors explaining to the customer how they decided what to write, rather than stating the design. The parenthetical is also unsupportable: no project file references MudBlazor, and the four files that mention the name do so only in comments describing a library that was removed (`SimfImageCropperModal.razor:4`, `Plenary.razor:2`, `Visit.razor:2`, `landing.css:13`). There is no stray razor reference to set aside.

**Fix.** "Note on the component library: the shared `Simf*` Blazor component library (`SIMF.Components`) and `theme.tokens.css` form the Control Panel and Website UI foundation. `Cropper.Blazor` is the only third-party UI package. No project references MudBlazor."

### 140. LLD paragraph 617  <sub>minor</sub>

> Approve a booking (legacy, retained but dormant; no attendee booking enters Pending)

The dormant legacy booking approval is stated four times inside this section and a fifth time at line 532. Line 617 says "legacy, retained but dormant; no attendee booking enters Pending"; line 619 says "Legacy approve/reject/bulk-approve retained but dormant"; line 797 says "(Legacy PR/reviewer approval retained but dormant.)"; line 803 says "The legacy Reject / Bulk approve actions are retained but dormant (no attendee booking enters Pending to act on)". One fact, repeated. Line 803 then contradicts itself in its own next sentence by offering "Cancel (UC-10) a Pending/Approved booking", a state it has just said no attendee booking reaches, and which `BookingStatus` confirms has "NO production writer ... nothing persists it". Keep the statement once, at line 619.

**Fix.** "Approve a booking (legacy, dormant)"

### 141. LLD paragraph 619  <sub>minor</sub>

> Legacy approve/reject/bulk-approve retained but dormant; attendee reservations auto-confirm on create and confirm at gate check-in.

The UC-22 row says the same thing twice and then adds a claim the code does not support. The name cell at paragraph 617 already reads "Approve a booking (legacy, retained but dormant; no attendee booking enters Pending)", so "retained but dormant" and the empty-queue fact are both stated a second time in the description cell one line later. The tail, "confirm at gate check-in", is the same wrong mechanism reported at paragraph 611: gate check-in reads SeatReservations with AsNoTracking() in both SessionAttendanceService.cs and GateOperatorService.cs and writes nothing to the booking. The document already carries the dormant-queue fact four more times, at paragraphs 181, 183, 532 and 1009.

**Fix.** "Approve a booking (legacy, dormant)"

### 142. LLD paragraph 699  <sub>minor</sub>

> Assistant, summaries, question filtering.

This row repeats the two rows immediately above it. Line 688 to 690 is "Use the AI assistant / Visitor / Assistant over two-level FAQ" and line 694 to 696 is "AI session summary / Visitor / Generated per-session summary", so "Assistant, summaries, question filtering" adds nothing but the filter, which line 819 already describes as stage 1 of the question pipeline. The row's name is worse than redundant: "Accessibility AI" appears exactly once in the whole document, at line 697, and is defined nowhere, so a reviewer cannot tell what it covers. It is not the accessibility feature the product has. `src/Mobile/simf_app/lib/features/accessibility/accessibility_screen.dart` offers text size, high contrast, reduced motion, screen-reader assist and captions, none of which are AI.

**Fix.** "Accessibility settings"

### 143. LLD paragraph 843  <sub>minor</sub>

> UC-17 AI assistant over two-level FAQ; meet-people-like-you matchmaking (>=80% recommend/push); AI session summary; AI assistant and summaries.

The cell lists the AI assistant and the session summary and then lists them again as "AI assistant and summaries" in the same sentence. Nothing is added by the second mention.

**Fix.** "UC-16 Request one-to-one meeting (Visitor to PR approval); UC-17 AI assistant over two-level FAQ; meet-people-like-you matchmaking (>=80% recommend/push); AI session summary."

---

## 6. Features named but not described

### 144. BRD paragraph 45  <sub>serious</sub>

> cognitive assistant, AI summaries, AI moderation, AI translation and accessibility, and smart recommendations

Two of these five name nothing the reader can find. "AI moderation" is the comment moderation deleted at DECISIONS_LOG D-589 and D-605; the only AI screening left in src is the advisory question filter, which is listed separately elsewhere as "AI filtering of attendee questions". "AI translation and accessibility" gives no feature, no place it runs and nothing it talks to: the one AI accessibility feature this document ever carried is withdrawn at paragraph 466, "Withdrawn in v1.3. Sign language and speech conversion are not delivered", and the only translation left is post-session subtitle translation (paragraph 554). Paragraph 49 repeats the same undefined "accessibility" and the same removed comments.

**Fix.** "Build on a modern technology stack and embed AI and emerging technologies (cognitive assistant, AI session summaries, AI filtering of attendee questions, post-session subtitle translation, and smart recommendations)."

### 145. HLD paragraph 553  <sub>serious</sub>

> Background-services monitor: the scheduled background jobs report a heartbeat to an in-process registry. The Control Panel "Background services" page lists every worker with its state (up, starting, stale or faulted), last run, run and failure counts and last error, refreshing on a short interval; the same registry feeds a "workers" check on /health, so a stalled or faulted job pulls the readiness

The document never says how many of the four API nodes run a scheduled job, which is one of the eight questions the technology team asked in writing. The code answers it: src/Backend/SIMF.Infrastructure/Operations/WorkerLease.cs "Elects exactly one API instance to run the background workers", taking a SQL Server application lock named SIMF.BackgroundWorkers, re-checked every 30 seconds, standing the workers down when it is lost. WorkerLeaseRegistration.AddLeasedHostedService wraps thirteen workers and deliberately excludes EmailBackgroundService, which drains a per-process in-memory channel and so runs on every node. Two further statements here do not hold. The page cannot list "every worker": the registry is a per-process singleton, GET /admin/ops/workers returns registry.Snapshot() from whichever node the API load balancer picked, and a worker calls heartbeat.Register inside ExecuteAsync, which never runs on a node that does not hold the lease. And a faulted worker returns Unhealthy, not Degraded (WorkersHealthCheck returns HealthCheckResult.Unhealthy for FaultedCount and Degraded for StaleCount).

**Fix.** "Background-services monitor: the database-driven scheduled jobs run on one API node at a time. That node holds a SQL Server application lock for the estate. If it stops, another node takes the lock within one 30-second poll and starts the jobs. The e-mail dispatcher is not elected: it runs on every node and drains that node's own in-process queue. Each running job reports a heartbeat to the registry in its own process, so the Control Panel "Background services" page lists the"

### 146. LLD paragraph 1353  <sub>serious</sub>

> AI assistant over the two-level FAQ

The app assistant is not an FAQ browser, and the cell names neither what it talks to nor where it runs, which is the first item on the customer's written review list. `lib/features/chatbot/data/chatbot_responder.dart` posts free text to `POST /app/ai/assistance` and renders the `outputText` field; `lib/features/chatbot/data/chatbot_endpoints.dart` holds `/app/ai/assistance` and `/app/ai/assistance/history`. There is no narrowing from group to entry in the screen; the chips in `widgets/quick_replies.dart` just send their label as the next prompt. Server side, `src/Backend/SIMF.Infrastructure/Ai/AssistanceContextBuilder.cs` grounds the prompt on three sources, "the WHOLE programme, the WHOLE FAQ and the WHOLE booth list", and then the model answers. So the FAQ is one of three grounding inputs, not the mechanism. Paragraphs 198 to 200 carry the same FAQ-only description ("a conversational AI assistant backed by a two-level FAQ (groups to entries)", "The AI assistant narrows to a FAQ group then an entry"); they are in another slice and need the same correction.

**Fix.** "AI assistant. Calls `POST /app/ai/assistance`, which asks the on-site LLM server; grounded on the programme, the FAQ and the booth list"

---

## Two things found outside the read

- `CLAUDE.md` is itself stale on one point. D-217 records the reminder dedup column as `Session.ReminderSentUtc`; `src/Backend/SIMF.Domain/Programme/Session.cs:48` declares `ReminderSentAt`. The LLD copied the wrong name from the same source.
- `SIMF-HLD-004-Response-to-Technical-Review-v1.0.docx` was never reissued for phase one. It still says "egress point" nine times and names neither MinIO nor GPT OSS 120B, so it contradicts the HLD annex it accompanies.
