/* =====================================================================
   SIMF_App — assistance AI-prompt grounding update
             ->  POST /app/ai/assistance  (the app AI assistant, Page 036)

   Target database : SIMF_App   (NOT SIMF_Identity)
   Safe to re-run  : YES — guarded by "UserPromptTemplate NOT LIKE '%{context}%'",
                     so it rewrites the row only while it still carries the old
                     message-only template; a second run updates 0 rows.
   Transactional   : one transaction with SET XACT_ABORT ON.

   Why             : the app AI assistant was wired to the centralised AI and
                     grounded on the live event context (programme sessions, FAQ,
                     booths). The C# prompt seeder (IdentitySeeder
                     .EnsureDefaultAiPromptsAsync) is INSERT-ONLY — it never
                     updates an existing key — so on any ALREADY-SEEDED database
                     the 'assistance' AiPrompt row keeps its old "{message}"-only
                     template, and the {context}/{locale} inputs the endpoint now
                     passes are silently dropped (Substitute only replaces
                     placeholders that exist) => ungrounded answers plus wasted
                     per-call context queries. This script brings an existing row
                     up to the grounded template so grounding actually takes
                     effect without a fresh re-seed. A freshly-seeded database
                     already has this exact template (kept in sync with the seeder).
   ===================================================================== */
SET XACT_ABORT ON;
SET NOCOUNT ON;

IF OBJECT_ID(N'dbo.AiPrompts', N'U') IS NULL
BEGIN
    PRINT N'dbo.AiPrompts not found — skipping (wrong database?).';
    RETURN;
END;

BEGIN TRAN;

UPDATE dbo.AiPrompts
SET
    SystemPrompt = N'You are a friendly concierge for SIMF (Saudi International Maritime Forum) visitors. Help with directions, the agenda, sessions, speakers, FAQ, and exhibition booths. Use ONLY the live event context provided with the question — never invent a session, time, hall, or booth. If the answer is not in that context, say you do not have that information and suggest asking the help desk. Be brief (1–3 sentences), polite, and culturally aware. Reply in Arabic when the visitor''s language is ''ar'', otherwise in English.',
    UserPromptTemplate =
        N'Visitor language: {locale}' + CHAR(10) +
        N'Visitor question: {message}' + CHAR(10) + CHAR(10) +
        N'Live event context (programme sessions, FAQ, booths):' + CHAR(10) +
        N'{context}',
    Version   = Version + 1,
    UpdatedAt = SYSUTCDATETIME()
WHERE [Key] = N'assistance'
  AND UserPromptTemplate NOT LIKE N'%{context}%';

PRINT CONCAT(N'assistance prompt grounding update: ', @@ROWCOUNT, N' row(s) updated.');

COMMIT TRAN;
