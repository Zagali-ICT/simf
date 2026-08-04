/* =====================================================================
   SIMF_App — assistance AI-prompt conversation-memory update
             ->  POST /app/ai/assistance  (the app AI assistant, Page 036)

   Target database : SIMF_App   (NOT SIMF_Identity)
   Safe to re-run  : YES — guarded by "UserPromptTemplate NOT LIKE '%{history}%'",
                     so it rewrites the row only while it still lacks the
                     conversation-memory slot; a second run updates 0 rows.
   Transactional   : one transaction with SET XACT_ABORT ON.

   Why             : the app AI assistant now persists the visitor's conversation
                     (AiChatMessages table, migration AddAiChatHistory) and feeds
                     the recent turns back so the assistant remembers earlier
                     messages. The endpoint passes a new {history} input, but the
                     C# prompt seeder (IdentitySeeder.EnsureDefaultAiPromptsAsync)
                     is INSERT-ONLY — it never updates an existing key — so on any
                     ALREADY-SEEDED database the 'assistance' AiPrompt row keeps
                     its old template without the {history} placeholder, and the
                     memory input is silently dropped (Substitute only replaces
                     placeholders that exist). This script adds the {history} slot
                     to an existing row so memory actually takes effect without a
                     fresh re-seed. A freshly-seeded database already has this exact
                     template (kept in sync with the seeder).

   ORDER           : run AFTER the EF migration AddAiChatHistory (which creates the
                     dbo.AiChatMessages table) and, if not yet applied, alongside
                     SIMF_App_AssistancePromptGrounding.sql (grounding {context}).
   ===================================================================== */
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
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
    UserPromptTemplate =
        N'Visitor language: {locale}' + CHAR(10) +
        N'Visitor question: {message}' + CHAR(10) + CHAR(10) +
        N'Conversation so far (may be empty):' + CHAR(10) +
        N'{history}' + CHAR(10) + CHAR(10) +
        N'Live event context (programme sessions, FAQ, booths):' + CHAR(10) +
        N'{context}',
    Version   = Version + 1,
    UpdatedAt = SYSUTCDATETIME()
WHERE [Key] = N'assistance'
  AND UserPromptTemplate NOT LIKE N'%{history}%';

PRINT CONCAT(N'assistance prompt memory update: ', @@ROWCOUNT, N' row(s) updated.');

COMMIT TRAN;
