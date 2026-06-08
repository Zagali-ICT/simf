# SIMF — Remediation & QA Plan

Last updated: 2026-06-08 · Owner-requested (a real development plan with
**testing / checking / validating / review** built in as gates, not after-thoughts).

This plan governs the remaining "finish + verify everything" push. It is a
**process**, not a feature list: nothing is "done" until it passes the gate
below, and every delivery leads with the gate's **evidence**.

---

## The gate — Definition of Done for EVERY change

Run in order; skip nothing (if a step is genuinely N/A, say so):

1. **Unit tests** written + green.
2. **Integration tests** green.
3. **Build clean** — `dotnet build -c Release` 0 warnings / 0 errors (or
   `flutter analyze` + `flutter test` clean for the app).
4. **LIVE DOM / browser verification** — open the *running* page and look:
   full-page **screenshot** + **console** (zero errors) + **network** (zero
   failed/broken assets — 404s, broken `<img>`) + **DOM** check
   (`scrollWidth == clientWidth` → no horizontal overflow; no broken images;
   layout matches the mockup). **Compiling ≠ rendering** — only a live render
   proves a page works.
5. **E2E** — run that page's catalogue scenarios under `docs/tests/e2e/`.
6. **Review** — review agents + the `simplify` skill; address findings.
7. **Docs** — PAGE-INDEX + per-page reference + E2E catalogue updated in the
   **same** changeset.

(Mirrors global `~/.claude/CLAUDE.md` §17 and the `delivery-verification-gate`
memory.)

---

## Phases

| # | Phase | Scope | Gate focus |
|---|-------|-------|-----------|
| 0 | **QA baseline** | This doc + the defect register below. No feature code. | Owner approval of the plan. |
| 1 | **Web remediation** | Fix visible web defects; validate **all 12 landing sections** (desktop + mobile + RTL) against the mockup; confirm each renders real CMS data. | Per section: live screenshot + console-clean + network-clean; run `web-landing.md`; design diff. |
| 2 | **Backend image pipelines** | Per entity (speaker, sponsor, booth, media-partner, news, archive): upload endpoint + out-of-row byte storage + anonymous serving endpoint + sample seed. | API tests + live `curl` returns bytes; CP upload works. |
| 3 | **App image rendering** | Wire speaker photos/flags + logos + covers (reuse the proven gallery pattern, unblocked by Phase 2). | `flutter analyze` + `test` + run app vs backend + per-screen screenshots. |
| 4 | **Full regression** | Drive **every** CP + Web + App page via the E2E catalogue. | Review agents + reality-check sign-off; defect register at zero. |

---

## Defect register

Live, evidence-based. `OPEN` until fixed **and** re-verified through the gate.

| ID | Surface | Defect | Found by | Status |
|----|---------|--------|----------|--------|
| WEB-001 | Website landing — About section | `.about-img` referenced `assets/about-image.png`, which was never added → **404**, empty box. | Live browser check 2026-06-08 (console 404 + network) | **FIXED + re-verified** (D-343) — repointed to the on-brand `assets/neom.avif` + a `--navy` fallback; live gate shows `neom.avif` 200, About renders the image. |
| WEB-003 | Website landing — `<head>` | No favicon link → browser fell back to `/favicon.ico` → **404**. | Live network check 2026-06-08 | **FIXED + re-verified** (D-343) — added `<link rel="icon" href="assets/logo-simf.png">`; live gate shows `logo-simf.png` 200, no `/favicon.ico` request, **console clean, zero failed requests**. |
| WEB-002 | Website landing — sessions / speakers / partners / news | Cards render **without images** (text-only / placeholder tiles). | Live DOM check 2026-06-08 | **OPEN — backend-blocked** (no image pipeline for these entities; see D-342). Tracked under Phase 2. |
| APP-001 | Flutter — speaker photos, country flags, sponsor/booth/partner logos, news/archive covers | No images render (vestigial `*RelativePath` fields, no upload/storage/serving). | Backend audit 2026-06-07/08 (D-342) | **OPEN — backend-blocked.** Tracked under Phase 2 → 3. |
| WEB-004 | Website landing — Partners strip | (a) marquee scrolled the wrong way on RTL; (b) "design not good" — name-only text cards + a stray `↖` arrow (logos `not publicly servable`). | Owner report + live check 2026-06-08 | **FIXED + re-verified** (D-344 direction; D-348 design) — reversed the marquee; seeded sponsors + media partners (9 total); each card now shows a **branded logo** (server-generated data-URI white card with the name) on a clean white slot, stray arrow removed. Live: 18/18 logos render, 0 broken, console clean, no overflow. |
| WEB-005 | Website landing — Sessions (`الجلسات الرئيسية`) | Cards rendered an empty tag line (session with no theme) + risked `undefined` text; no-image area was unstyled. Owner: "not as in design". | Owner report + live DOM 2026-06-08 | **FIXED** (D-348) — render branded panel when no bitmap, **drop empty tag**, guard every optional field (no `undefined`); a session added via the CP now shows clean. |
| WEB-006 | Website landing — Archive (`الأرشيف`) | Auto-rotating year-tab timeline that swaps inline text; reads `/app/archive`. Owner: "on click must open real Archive, as in docs/mockup — per year a separate page". | Owner report 2026-06-08 | **FIXED + re-verified** (D-347) — seeded 4 editions (2022–2025) with full detail; clicking a year opens a full-screen **per-year page** (mockup 24-01: year cover, title, summary, place/time boxes, 3 counters) at a shareable `#archive-{year}` URL; browser-back / the back button closes it. Live gate: 4 tabs, page opens with real data (سيمف 2023 → 32/1000/35 + place/time), back closes + clears hash, console clean, no overflow. |
| WEB-007 | Website landing — Speakers (`أبرز المتحدثين`) | Backend had **1 speaker**, so the live section looked sparse/wrong. Owner: "not as in Backend, fill with demo data". | Owner report + live `/app/speakers` check 2026-06-08 | **FIXED + re-verified** (D-345) — seeded 7 idempotent `DEMO-SPK-*` speakers in `IdentitySeeder`; `/app/speakers` now returns **8**; the live Website strip renders all 8 (Arabic names from the seed), console clean, no overflow. |

(Gallery tile bitmaps — Page 030 — shipped D-342 and are not a defect.)
