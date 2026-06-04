# Mobile App Screen Design Specification (Flutter)

| Field | Value |
|-------|-------|
| Document ID | SIMF-MOB-SDS-001 *(provisional — confirm numbering against SIMF-DMP-001)* |
| Title | Mobile App Screen Design Specification (Flutter) |
| Version | 0.1 (DRAFT — skeleton; Screen 14 filled) |
| Status | Draft |
| Classification | Confidential |
| Prepared by | SIMF Engineering Team |
| Owner | SIMF Programme Owner |
| Date issued | 2026-06-02 |
| Related documents | `Mockup.html` (authoritative visual), `SIMF_Screen_Guide_and_User_Journey` (screen narratives), SIMF-MOB-API-001 (App API contract), SIMF-MAA-001 (mobile architecture), `src/Mobile/simf_app/lib/app/route_names.dart` (41-screen route table) |

### Revision history

| Version | Date | Author | Summary of change |
|---------|------|--------|-------------------|
| 0.1 | 2026-06-02 | SIMF Engineering Team | First draft skeleton. **Screen 14 (My Area dashboard)** fully specified — layout, data binding to SIMF-MOB-API-001 §6, actions/share intents, states, gating, localization. Screens 1–13, 15–41 are placeholders to be filled screen-by-screen. |

---

## 1. Purpose

This document is the **build-ready design spec** the Flutter developer/designer works
from, one screen at a time. For each mockup screen it states: the visual layout (from
`Mockup.html`), the **data binding** of every UI element to a specific
SIMF-MOB-API-001 field, the user actions and navigation, the loading/empty/error
states, the app-privilege gating, and the Arabic/English localization rules.

It is the bridge between three sources: the **mockup** (how it looks), the **screen
guide** (what it means), and the **App API** (where the data comes from). Where the
three disagree, the screen guide governs intent and the API governs data shape; raise
conflicts with the owner.

> This is a v0.1 skeleton: only **Screen 14** is filled. The remaining screens carry
> their identity + the section template, to be completed in subsequent waves. Nothing
> but Screen 14 is binding yet.

## 2. App privilege gating (the only four)

Every screen states which of the four app roles may reach it — `Guest` (not-signed-in
or pending), `Visitor`, `Moderator` (محاور), `Staff`. See SIMF-MOB-API-001 §4 and the
mobile state machine (SIMF-MAA-001 §8).

## 3. Localization & direction

Arabic is primary (RTL); English secondary (LTR). Bilingual data arrives as paired
fields from the API (`titleAr`/`titleEn`, `tierNameAr`/`tierNameEn`, `fullNameAr`/
`fullNameEn`); the app picks per the active locale. No hardcoded display strings for
data — only static chrome labels are localized in-app.

## 4. Per-screen template

Each screen below is specified as: **Identity · Gating · Layout · Data binding ·
Actions & navigation · States · Notes/dependencies.**

---

## Screen 14 — منطقتي · My Area (dashboard)

**Full design moved to the page folder.** Screen 14's complete design (layout, data
binding, actions, states, localization) now lives in
**[`docs/App/Page_014/Page_014_Design.md`](App/Page_014/Page_014_Design.md)**, alongside
its Function / Logic / API docs in [`docs/App/Page_014/`](App/Page_014/README.md). This
section is an index only.

In brief: a personal **dashboard** (identity card + two counters + today's merged
schedule + share), Visitor-and-above, bound to one `GET /account/dashboard` aggregate,
with `.ics` calendar + vCard share via the native intent.

---

## Screens 1–13, 15–41 — *to be specified (per-screen waves)*

Each remaining screen is listed in `route_names.dart` (numbered 1–41) and will be
filled using the §4 template. Priority order to be set with the owner; Section 1
(auth, Screens 1–12) pairs with SIMF-MOB-API-001 §5.
