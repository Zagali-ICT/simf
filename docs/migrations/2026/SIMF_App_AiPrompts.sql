/* =====================================================================
   SIMF_App — default AI PROMPT catalogue seed  (8 prompts)

   Target database : SIMF_App   (NOT SIMF_Identity)
   Safe to re-run  : YES — every insert is guarded on the prompt Key.
   Transactional   : one transaction with SET XACT_ABORT ON.

   Provenance      : ported verbatim from IdentitySeeder —
                     EnsureDefaultAiPromptsAsync. The owner rule is that
                     IdentitySeeder keeps ONLY the identity bootstrap; every
                     other seed lives in this SQL lane, in one location, behind
                     one runner (Run_All_App_Seeds.sql).

   One prompt per AI feature. Every row ships on the ECHO provider
   (AiProvider.Echo = 0, model 'echo'), the deterministic offline provider that
   never makes an outbound call, so a fresh install and the test suite both run
   with no key configured. An admin switches a prompt's Provider and edits both
   templates from the Control Panel without a redeploy.

   Feature is the integer value of SIMF.Common.Enums.AiFeature:
       0 QuestionFilter · 1 Faq · 2 Assistance · 3 Translate
       4 LiveTranslation · 5 LiveSignLanguage · 6 SessionSummary · 7 CpAssistant

   The templates contain {placeholder} spans the service substitutes at call
   time. Braces are safe here: SqlContentSeeder runs these files over a raw
   DbCommand precisely so a brace is never read as a parameter placeholder.

   Multi-line user templates are built with NCHAR(10) concatenation rather than
   literal line breaks, so a Windows checkout cannot smuggle a carriage return
   into a template the model receives.
   ===================================================================== */

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

DECLARE @now datetimeoffset   = SYSDATETIMEOFFSET();
DECLARE @sys uniqueidentifier = '00000000-0000-0000-0000-000000000000'; -- system/seeder actor (matches the app seeders)
DECLARE @lf  nchar(1)         = NCHAR(10);

-- Audience question safety filter.
IF NOT EXISTS (SELECT 1 FROM dbo.AiPrompts WHERE [Key] = N'question-filter')
    INSERT INTO dbo.AiPrompts (Id, [Key], Feature, DisplayName, DisplayNameArabic,
        Provider, Model, SystemPrompt, UserPromptTemplate, Temperature,
        MaxOutputTokens, IsActive, Version, CreatedAt, UpdatedByUserId)
    VALUES (NEWID(), N'question-filter', 0,
        N'Audience Question Safety Filter', N'مصفّاة أمان أسئلة الجمهور',
        0, N'echo',
        N'You are a moderation assistant for a public maritime forum. Given an audience question, decide whether it is appropriate for a live Q&A: reject hate speech, personal attacks, off-topic content, advertising, or spam. Reply in JSON: {"allowed": bool, "reason": string}.',
        N'Question: {text}',
        0.2, 512, 1, 1, @now, @sys);

-- Event FAQ assistant.
IF NOT EXISTS (SELECT 1 FROM dbo.AiPrompts WHERE [Key] = N'faq-answer')
    INSERT INTO dbo.AiPrompts (Id, [Key], Feature, DisplayName, DisplayNameArabic,
        Provider, Model, SystemPrompt, UserPromptTemplate, Temperature,
        MaxOutputTokens, IsActive, Version, CreatedAt, UpdatedByUserId)
    VALUES (NEWID(), N'faq-answer', 1,
        N'Event FAQ Assistant', N'مساعد الأسئلة الشائعة للفعّالية',
        0, N'echo',
        N'You are the SIMF (Saudi International Maritime Forum) FAQ assistant. Answer concisely (1–3 sentences). Use Arabic if the question is in Arabic, English otherwise. If you do not know, say so and recommend asking the help desk.',
        N'Question: {question}',
        0.2, 512, 1, 1, @now, @sys);

-- Visitor concierge. Grounded on the live event context ({context}: programme
-- sessions, FAQ, booths — built server-side) so it answers from the real agenda
-- and not from model priors. {locale} is the visitor's UI language.
IF NOT EXISTS (SELECT 1 FROM dbo.AiPrompts WHERE [Key] = N'assistance')
    INSERT INTO dbo.AiPrompts (Id, [Key], Feature, DisplayName, DisplayNameArabic,
        Provider, Model, SystemPrompt, UserPromptTemplate, Temperature,
        MaxOutputTokens, IsActive, Version, CreatedAt, UpdatedByUserId)
    VALUES (NEWID(), N'assistance', 2,
        N'Visitor Concierge', N'خدمة الزوّار',
        0, N'echo',
        N'You are a friendly concierge for SIMF (Saudi International Maritime Forum) visitors. Help with directions, the agenda, sessions, speakers, FAQ, and exhibition booths. Use ONLY the live event context provided with the question — never invent a session, time, hall, or booth. If the answer is not in that context, say you do not have that information and suggest asking the help desk. Be brief (1–3 sentences), polite, and culturally aware. Reply in Arabic when the visitor''s language is ''ar'', otherwise in English.',
        N'Visitor language: {locale}' + @lf +
        N'Visitor question: {message}' + @lf + @lf +
        N'Conversation so far (may be empty):' + @lf +
        N'{history}' + @lf + @lf +
        N'Live event context (programme sessions, FAQ, booths):' + @lf +
        N'{context}',
        0.2, 512, 1, 1, @now, @sys);

-- Text translator.
IF NOT EXISTS (SELECT 1 FROM dbo.AiPrompts WHERE [Key] = N'translate')
    INSERT INTO dbo.AiPrompts (Id, [Key], Feature, DisplayName, DisplayNameArabic,
        Provider, Model, SystemPrompt, UserPromptTemplate, Temperature,
        MaxOutputTokens, IsActive, Version, CreatedAt, UpdatedByUserId)
    VALUES (NEWID(), N'translate', 3,
        N'Text Translator', N'مترجم النصوص',
        0, N'echo',
        N'Translate the text from {sourceLang} to {targetLang}. Reply with only the translation — no commentary, no quotes.',
        N'{text}',
        0.2, 512, 1, 1, @now, @sys);

-- Live speech translator.
IF NOT EXISTS (SELECT 1 FROM dbo.AiPrompts WHERE [Key] = N'live-translation')
    INSERT INTO dbo.AiPrompts (Id, [Key], Feature, DisplayName, DisplayNameArabic,
        Provider, Model, SystemPrompt, UserPromptTemplate, Temperature,
        MaxOutputTokens, IsActive, Version, CreatedAt, UpdatedByUserId)
    VALUES (NEWID(), N'live-translation', 4,
        N'Live Speech Translator', N'المترجم الحيّ للكلام',
        0, N'echo',
        N'Translate this in-progress transcript chunk from {sourceLang} to {targetLang}. Reply with only the translated chunk — keep punctuation light because chunks are concatenated client-side.',
        N'{text}',
        0.2, 512, 1, 1, @now, @sys);

-- Live sign-language gloss.
IF NOT EXISTS (SELECT 1 FROM dbo.AiPrompts WHERE [Key] = N'live-sign-language')
    INSERT INTO dbo.AiPrompts (Id, [Key], Feature, DisplayName, DisplayNameArabic,
        Provider, Model, SystemPrompt, UserPromptTemplate, Temperature,
        MaxOutputTokens, IsActive, Version, CreatedAt, UpdatedByUserId)
    VALUES (NEWID(), N'live-sign-language', 5,
        N'Live Sign-Language Gloss', N'ترجمة الإشارة الحيّة',
        0, N'echo',
        N'Convert this in-progress transcript chunk into a glossed sign-language sequence suitable for a downstream avatar renderer. Keep glosses uppercase and space-separated.',
        N'{text}',
        0.2, 512, 1, 1, @now, @sys);

-- AI session-summary / محضر drafting.
IF NOT EXISTS (SELECT 1 FROM dbo.AiPrompts WHERE [Key] = N'session-summary')
    INSERT INTO dbo.AiPrompts (Id, [Key], Feature, DisplayName, DisplayNameArabic,
        Provider, Model, SystemPrompt, UserPromptTemplate, Temperature,
        MaxOutputTokens, IsActive, Version, CreatedAt, UpdatedByUserId)
    VALUES (NEWID(), N'session-summary', 6,
        N'Session Minutes (محضر) Drafter', N'مُسوّد محضر الجلسة',
        0, N'echo',
        N'You are the rapporteur for the SIMF (Saudi International Maritime Forum). Draft concise, formal minutes (محضر) in Arabic covering the key points discussed, the recommendations, and who took part. Base the minutes primarily on the verbatim session transcript (subtitle) when one is provided; use the abstract only to fill gaps or when no transcript was captured. The Scientific Committee reviews and edits your draft before it is published.',
        N'Session: {sessionTitle}' + @lf +
        N'Speakers: {speakers}' + @lf +
        N'Abstract: {sessionAbstract}' + @lf +
        N'Transcript (subtitle): {transcript}' + @lf +
        N'Transcript (Arabic): {transcriptArabic}',
        0.2, 512, 1, 1, @now, @sys);

-- Control Panel operator assistant. Grounded on the CP page catalogue ({pages},
-- one line per page the caller can access) so it can only ever cite a real route
-- the operator is allowed to open.
IF NOT EXISTS (SELECT 1 FROM dbo.AiPrompts WHERE [Key] = N'cp-assistant')
    INSERT INTO dbo.AiPrompts (Id, [Key], Feature, DisplayName, DisplayNameArabic,
        Provider, Model, SystemPrompt, UserPromptTemplate, Temperature,
        MaxOutputTokens, IsActive, Version, CreatedAt, UpdatedByUserId)
    VALUES (NEWID(), N'cp-assistant', 7,
        N'Control Panel Assistant', N'مساعد لوحة التحكم',
        0, N'echo',
        N'You are the assistant for the SIMF (Saudi International Maritime Forum) Control Panel — an administrator''s help guide. The operator asks where to find a screen or how to configure something. You are given a directory of the Control Panel pages this operator can access, each with its exact route path. Answer briefly and practically, and ALWAYS cite the exact route path from the directory (for example /admin/sessions) so the operator can open it. Use ONLY routes that appear in the directory — never invent a path. If no listed page matches, say the operator may not have permission for it or it does not exist, and suggest asking an administrator. Reply in Arabic if the question is in Arabic, otherwise in English.',
        N'Question: {question}' + @lf +
        N'Operator interface language: {locale}' + @lf +
        N'Control Panel pages available to this operator (name -> route):' + @lf +
        N'{pages}',
        0.2, 512, 1, 1, @now, @sys);

COMMIT TRANSACTION;
