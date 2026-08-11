/* =====================================================================
   QA-ONLY — fixture data for the reporting catalogue (docs/tests/e2e/cp-reports.md).

   WHY. Six catalogue scenarios assert real figures — meetings 8/3/4, ratings
   4 with a 4.3 average, questions 12/2/5 — and a dev database with no
   meetings, ratings or questions can only verify columns and labels. This
   seeds exactly the numbers those scenarios name, so E2E-RPT-024, 025, 026,
   028, 029 and 030 can be driven for real instead of structurally.

   REMOVING IT. Every row uses the 'eeee' GUID prefix, so the whole fixture
   comes out with the four deletes at the top of this script. Re-running it is
   idempotent: it deletes its own previous rows first.

   SAFETY — READ THIS. Production and local dev BOTH use the database name
   SIMF_App, so a name guard cannot tell them apart the way
   seed-restricted-admin.sql can. Instead this script refuses to run unless you
   pass an explicit opt-in:

       sqlcmd -S . -E -d SIMF_App -I -v AllowSeed=YES -i tools/qa/seed-report-fixtures.sql

   Running it against production would inject fabricated meeting requests,
   ratings and audience questions into live event data, where they would be
   indistinguishable from real attendee input in every report and export.
   ===================================================================== */

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
SET XACT_ABORT ON;

/* No `:setvar AllowSeed` default here on purpose. An in-script :setvar OVERRIDES
   the -v value sqlcmd was launched with, so a default would make the opt-in
   unreachable — the flag could never win. Without -v, sqlcmd stops at
   "'AllowSeed' scripting variable not defined" and never reaches the inserts,
   which is the refusal we want; with -v AllowSeed=YES the check below passes. */
IF N'$(AllowSeed)' <> N'YES'
BEGIN
    RAISERROR('seed-report-fixtures.sql refuses to run without -v AllowSeed=YES. It writes fabricated meetings, ratings and questions; against production those are indistinguishable from real attendee data.', 16, 1);
    SET NOEXEC ON;
END;

-- Idempotent: drop any previous run of this fixture first.
-- SessionQuestions before Sessions: the questions point at a session.
DELETE FROM SessionQuestions          WHERE Id LIKE 'eeee%';
DELETE FROM RatingResponses           WHERE Id LIKE 'eeee%';
DELETE FROM SpeakerMeetingRequests    WHERE Id LIKE 'eeee%';
DELETE FROM DelegationMeetingRequests WHERE Id LIKE 'eeee%';
DELETE FROM Sessions                  WHERE Id LIKE 'eeee0005%';

DECLARE @actor uniqueidentifier = '00000000-0000-0000-0000-0000000000aa';

-------------------------------------------------------------------------------
-- MEETINGS — target totals: 8 requests, 3 Pending, 4 checked in
-- 5 speaker (2 Pending, 3 checked in) + 3 delegation (1 Pending, 1 checked in)
-------------------------------------------------------------------------------
DECLARE @s1 uniqueidentifier, @s2 uniqueidentifier, @s3 uniqueidentifier,
        @s4 uniqueidentifier, @s5 uniqueidentifier;
SELECT @s1=MIN(Id) FROM Speakers;
SELECT @s2=MIN(Id) FROM Speakers WHERE Id > @s1;
SELECT @s3=MIN(Id) FROM Speakers WHERE Id > @s2;
SELECT @s4=MIN(Id) FROM Speakers WHERE Id > @s3;
SELECT @s5=MIN(Id) FROM Speakers WHERE Id > @s4;

INSERT INTO SpeakerMeetingRequests
    (Id, SpeakerId, RequestedByUserId, RequesterName, Subject,
     SlotStart, SlotEnd, Status, CheckedInAt, CreatedAt)
VALUES
    ('eeee0001-0000-0000-0000-000000000001', @s1, @actor, 'Ahmed Al-Otaibi',
     'Naval logistics briefing', '2026-11-23 10:00', '2026-11-23 10:30', 0, NULL,          '2026-08-09 09:15'),
    ('eeee0001-0000-0000-0000-000000000002', @s2, @actor, 'Noura Al-Harbi',
     'Port automation follow-up',  '2026-11-23 11:00', '2026-11-23 11:30', 0, NULL,          '2026-08-09 09:20'),
    ('eeee0001-0000-0000-0000-000000000003', @s3, @actor, 'Faisal Al-Dossary',
     'Shipbuilding partnership',   '2026-11-23 12:00', '2026-11-23 12:30', 1, '2026-11-23 11:58', '2026-08-09 09:25'),
    ('eeee0001-0000-0000-0000-000000000004', @s4, @actor, 'Layla Al-Qahtani',
     'Maritime cyber briefing',    '2026-11-24 09:00', '2026-11-24 09:30', 1, '2026-11-24 08:55', '2026-08-09 09:30'),
    ('eeee0001-0000-0000-0000-000000000005', @s5, @actor, 'Omar Al-Shehri',
     'Offshore energy roundtable', NULL,               NULL,               1, '2026-11-24 10:05', '2026-08-09 09:35');
     -- ^ deliberately unscheduled: the Slot cell must render BLANK, not a
     --   fabricated time (ToRow guards this).

DECLARE @c1 int, @c2 int;
SELECT @c1=MIN(Id) FROM Countries;
SELECT @c2=MIN(Id) FROM Countries WHERE Id > @c1;

INSERT INTO DelegationMeetingRequests
    (Id, RequestedByUserId, RequestingCountryId, TargetCountryId, AttendeeCount,
     Subject, SlotStart, SlotEnd, Status, CheckedInAt, CreatedAt)
VALUES
    ('eeee0002-0000-0000-0000-000000000001', @actor, @c1, @c2, 4,
     'Bilateral naval cooperation', '2026-11-23 14:00', '2026-11-23 15:00', 0, NULL,               '2026-08-09 10:00'),
    ('eeee0002-0000-0000-0000-000000000002', @actor, @c2, @c1, 6,
     'Joint exercise planning',     '2026-11-24 14:00', '2026-11-24 15:00', 1, '2026-11-24 13:52', '2026-08-09 10:05'),
    ('eeee0002-0000-0000-0000-000000000003', @actor, @c1, @c2, 3,
     'Coast guard training',        '2026-11-25 09:00', '2026-11-25 10:00', 2, NULL,               '2026-08-09 10:10');

-------------------------------------------------------------------------------
-- RATINGS — target totals: 4 ratings, average 4.3, 2 with a comment
-- 5 + 4 + 4 = 13 / 3 = 4.333 -> "4.3". The null-star row must NOT move it.
-------------------------------------------------------------------------------
DECLARE @sessionType uniqueidentifier = '11111111-1111-1111-1111-000000000002';
DECLARE @target uniqueidentifier;
SELECT @target = MIN(Id) FROM Sessions;

INSERT INTO RatingResponses
    (Id, UserId, RatingTypeId, TargetId, OverallStars, Comment,
     CreatedAt, CreatedBy, IsActive)
-- FOUR DISTINCT RATERS. RatingResponses carries a unique index on
-- (UserId, RatingTypeId, TargetId) - one person rates a given target once per
-- rating type - so a fixture that reuses one user id is rejected outright.
VALUES
    ('eeee0003-0000-0000-0000-000000000001', '00000000-0000-0000-0000-0000000000b1', @sessionType, @target, 5,
     'Excellent organisation',  '2026-08-09 12:00', @actor, 1),
    ('eeee0003-0000-0000-0000-000000000002', '00000000-0000-0000-0000-0000000000b2', @sessionType, @target, 4,
     'Well paced, good Q&A',    '2026-08-09 12:05', @actor, 1),
    ('eeee0003-0000-0000-0000-000000000003', '00000000-0000-0000-0000-0000000000b3', @sessionType, @target, 4,
     NULL,                      '2026-08-09 12:10', @actor, 1),
    ('eeee0003-0000-0000-0000-000000000004', '00000000-0000-0000-0000-0000000000b4', @sessionType, @target, NULL,
     NULL,                      '2026-08-09 12:15', @actor, 1);
     -- ^ no stars AND no comment: must render as empty cells, must not be
     --   counted in "With a comment", must not drag the average down.

-------------------------------------------------------------------------------
-- ENGAGEMENT — target totals: 12 questions, 2 hidden, 5 pushed to speaker
-------------------------------------------------------------------------------
DECLARE @sess uniqueidentifier;
SELECT @sess = MIN(Id) FROM Sessions;

INSERT INTO SessionQuestions
    (Id, SessionId, SubmittedByUserId, QuestionText, Recipient, [Order],
     IsHidden, IsPushed, PushedAt, CreatedAt, Phase, Status)
VALUES
 ('eeee0004-0000-0000-0000-000000000001', @sess, @actor, 'How is the fleet maintained in winter?', 0, 1, 0, 1, '2026-08-09 13:01', '2026-08-09 13:00', 1, 1),
 ('eeee0004-0000-0000-0000-000000000002', @sess, @actor, 'What is the refit cycle for patrol vessels?', 0, 2, 0, 1, '2026-08-09 13:06', '2026-08-09 13:05', 1, 1),
 ('eeee0004-0000-0000-0000-000000000003', @sess, @actor, 'How are crews rotated on long deployments?', 0, 3, 0, 1, '2026-08-09 13:11', '2026-08-09 13:10', 1, 1),
 ('eeee0004-0000-0000-0000-000000000004', @sess, @actor, 'Which ports support deep maintenance?', 1, 4, 0, 1, '2026-08-09 13:16', '2026-08-09 13:15', 1, 1),
 ('eeee0004-0000-0000-0000-000000000005', @sess, @actor, 'What share of parts is sourced locally?', 0, 5, 0, 1, '2026-08-09 13:21', '2026-08-09 13:20', 0, 1),
 ('eeee0004-0000-0000-0000-000000000006', @sess, @actor, 'Hidden question one', 0, 6, 1, 0, NULL, '2026-08-09 13:25', 1, 2),
 ('eeee0004-0000-0000-0000-000000000007', @sess, @actor, 'Hidden question two', 1, 7, 1, 0, NULL, '2026-08-09 13:30', 1, 2),
 ('eeee0004-0000-0000-0000-000000000008', @sess, @actor, 'How is sonar data archived?', 0, 8, 0, 0, NULL, '2026-08-09 13:35', 0, 0),
 ('eeee0004-0000-0000-0000-000000000009', @sess, @actor, 'Are simulators used for certification?', 0, 9, 0, 0, NULL, '2026-08-09 13:40', 0, 0),
 ('eeee0004-0000-0000-0000-00000000000a', @sess, @actor, 'What is the fuel efficiency programme?', 1, 10, 0, 0, NULL, '2026-08-09 13:45', 1, 1),
 ('eeee0004-0000-0000-0000-00000000000b', @sess, @actor, 'How are contractors vetted?', 0, 11, 0, 0, NULL, '2026-08-09 13:50', 1, 1),
 ('eeee0004-0000-0000-0000-00000000000c', @sess, @actor, 'Is there a joint training curriculum?', 1, 12, 0, 0, NULL, '2026-08-09 13:55', 0, 3);

-------------------------------------------------------------------------------
-- SAUDI DAY BOUNDARY (E2E-RPT-005) — three sessions that pin the inclusive To.
--
-- Storage is ALREADY the Saudi wall clock (owner decision 2026-07-31, recorded
-- in SaudiTime), so these values mean exactly what they read. Filtering
-- attendance to 23-11 must keep A and B and exclude C. If the exclusive bound
-- were "To 00:00" rather than "To + 1 day", B would vanish - and with it the
-- last half-hour of every event day.
-------------------------------------------------------------------------------
DECLARE @hall uniqueidentifier;
SELECT @hall = MIN(Id) FROM Halls;

INSERT INTO Sessions
    (Id, Code, Title, TitleArabic, HallId, Start, [End], Status,
     CreatedAt, CreatedBy, IsActive)
VALUES
 ('eeee0005-0000-0000-0000-000000000001','BND-A','Boundary midday','حد الظهيرة',
  @hall,'2026-11-23 12:00','2026-11-23 13:00',1,'2026-08-09 08:00',@actor,1),
 ('eeee0005-0000-0000-0000-000000000002','BND-B','Boundary late evening','حد المساء',
  @hall,'2026-11-23 23:30','2026-11-24 00:30',1,'2026-08-09 08:00',@actor,1),
 ('eeee0005-0000-0000-0000-000000000003','BND-C','Boundary next morning','حد الصباح',
  @hall,'2026-11-24 00:30','2026-11-24 01:30',1,'2026-08-09 08:00',@actor,1);

-------------------------------------------------------------------------------
-- Confirm what landed, so the run is checked against intent, not assumed.
-------------------------------------------------------------------------------
SELECT 'meetings_total=' + CAST(
    (SELECT COUNT(*) FROM SpeakerMeetingRequests WHERE Id LIKE 'eeee%')
  + (SELECT COUNT(*) FROM DelegationMeetingRequests WHERE Id LIKE 'eeee%') AS varchar(9));
SELECT 'meetings_pending=' + CAST(
    (SELECT COUNT(*) FROM SpeakerMeetingRequests WHERE Id LIKE 'eeee%' AND Status = 0)
  + (SELECT COUNT(*) FROM DelegationMeetingRequests WHERE Id LIKE 'eeee%' AND Status = 0) AS varchar(9));
SELECT 'meetings_checkedin=' + CAST(
    (SELECT COUNT(*) FROM SpeakerMeetingRequests WHERE Id LIKE 'eeee%' AND CheckedInAt IS NOT NULL)
  + (SELECT COUNT(*) FROM DelegationMeetingRequests WHERE Id LIKE 'eeee%' AND CheckedInAt IS NOT NULL) AS varchar(9));
SELECT 'ratings_total=' + CAST((SELECT COUNT(*) FROM RatingResponses WHERE Id LIKE 'eeee%') AS varchar(9));
SELECT 'ratings_avg=' + CAST((SELECT CAST(AVG(CAST(OverallStars AS float)) AS decimal(4,2)) FROM RatingResponses WHERE Id LIKE 'eeee%' AND OverallStars IS NOT NULL) AS varchar(9));
SELECT 'ratings_withcomment=' + CAST((SELECT COUNT(*) FROM RatingResponses WHERE Id LIKE 'eeee%' AND Comment IS NOT NULL AND Comment <> '') AS varchar(9));
SELECT 'questions_total=' + CAST((SELECT COUNT(*) FROM SessionQuestions WHERE Id LIKE 'eeee%') AS varchar(9));
SELECT 'questions_hidden=' + CAST((SELECT COUNT(*) FROM SessionQuestions WHERE Id LIKE 'eeee%' AND IsHidden = 1) AS varchar(9));
SELECT 'questions_pushed=' + CAST((SELECT COUNT(*) FROM SessionQuestions WHERE Id LIKE 'eeee%' AND IsPushed = 1) AS varchar(9));
SELECT 'boundary_sessions=' + CAST((SELECT COUNT(*) FROM Sessions WHERE Id LIKE 'eeee0005%') AS varchar(9));

SET NOEXEC OFF;
