# SIMF — Old Draft vs Current Controlled Docs — Conflict Decision Worksheet

| Field | Value |
|-------|-------|
| Prepared | 2026-05-20 |
| Purpose | Decide how to resolve each conflict between the superseded `15-04-2024` prompt drafts and the current controlled documents |
| Old source | `D:\SIMF\System\15-04-2024\` — `final-prompt.md`, `my-style (1).md`, `professional-coding-agent-prompt.md` |
| Current source | `docs/` — `SIMF-SES-001`, `SIMF-API-001`, `SIMF-SAD-001`, `SIMF-MAA-001` (all issued 2026-05-20) |
| Status | Awaiting owner / Solution Architect decision |

This file is a working worksheet, not a controlled document. It does not change any
rule. Decisions made here are applied by editing the controlled docs, which is a
separate, reviewed task.

---

## 1. Conflict comparison

```
┌───────────────────────────────┬─────────────────────────────────┬────────────────────────┬────────────────────────┐
│ Old draft (15-04-2024)        │ Current SIMF controlled doc     │ Source                 │ Recommendation         │
├───────────────────────────────┼─────────────────────────────────┼────────────────────────┼────────────────────────┤
│ Always return HTTP 200        │ Real HTTP statuses 200/4xx/5xx  │ API-001 §8             │ Adopt current doc      │
│ status/errorCode/errorMessage │ success/data/error/meta         │ API-001 §6             │ Adopt current doc      │
│ Error message as AR+EN pair   │ One language per Accept-Language │ API-001 §11            │ Adopt current doc      │
│ Phone OTP at registration     │ 6-digit email verify code       │ API-001 §12 / SAD §8.1 │ Adopt current doc      │
│ Magic-link password reset     │ Reset code by email (open)      │ API-001 §12.7          │ Owner decision (OI-3)  │
│ Flutter on Web + mobile       │ Flutter mobile only; web=Blazor │ MAA-001 §2 / SAD §4    │ Adopt current doc      │
│ X-Language header             │ Accept-Language header          │ API-001 §5             │ Adopt current doc      │
│ Smif* component library       │ MudBlazor + designer comps      │ SES-001 §6.1 / MAA §12 │ Current doc — see note │
│ dd-MM-yyyy + English digits   │ Formatted for active locale     │ MAA-001 §10            │ Owner decision — note  │
└───────────────────────────────┴─────────────────────────────────┴────────────────────────┴────────────────────────┘
```

---

## 2. Recommendation detail (best practice + current docs)

1. **HTTP 200 always → Adopt current doc.** Real HTTP status codes are REST best
   practice. "Always 200" hides failures from caches, reverse proxies, monitoring
   and client HTTP libraries. API-001 §8 already pairs the transport status with
   the `ApiResult<T>` body, so a client still parses errors one way. No reason to
   revert.

2. **Response envelope → Adopt current doc.** `ApiResult<T>` (`success/data/error/
   meta`) is richer than the old flat shape: `meta` carries pagination, `error` is
   a structured object with a `details[]` array for field-level validation errors.
   The old `errorCode/errorMessage` cannot express field errors cleanly.

3. **AR/EN message pair → Adopt current doc.** Returning one language per
   `Accept-Language` is standard HTTP content negotiation — smaller payloads, and
   the client already states its language. Returning both languages doubles
   payload and needlessly moves the language choice to the client.

4. **Phone OTP → Adopt current doc.** Email verification (6-digit code) is fully
   specified in API-001 §12 and confirmed in SAD-001 §8.1 ("confirmed 2026-05-20").
   Phone OTP adds an SMS dependency and cost. If wanted later it is an additive
   change — it does not belong in the build now.

5. **Password reset → Owner decision (open item OI-3).** The reset flow is not yet
   specified. *Best-practice recommendation:* use a 6-digit **code by email**, to
   match `verify-email`, rather than a magic link — magic links carry their own
   risks (link interception, email forwarding). Confirm with the owner, then
   document the flow in API-001 §12.7.

6. **Flutter on Web → Adopt current doc.** The public website is Blazor (SAD-001
   §4); Flutter is Android + iOS only (MAA-001 §2). Flutter Web for a marketing and
   registration site adds bundle weight and weak SEO. Keep the current split.

7. **`X-Language` header → Adopt current doc.** `Accept-Language` is the standard
   HTTP language-negotiation header understood by frameworks and proxies. A custom
   `X-Language` reinvents it for no gain.

8. **`Smif*` component library → Follow current docs; may propose separately.**
   Current docs: the Control Panel uses MudBlazor directly (SES-001 §6.1); mobile
   components come from the external designer (MAA-001 §12). A shared wrapper
   component set has real merit (consistency, swappable underlying library), but it
   is a design decision absent from every controlled doc. *Recommendation:* do NOT
   introduce `Smif*` components from the old draft. If a wrapper layer is wanted,
   raise it as a proposal for the Control Panel design document (SIMF-CPD-001).

9. **Date `dd-MM-yyyy` + always-English digits → Owner decision.** MAA-001 §10 says
   numbers and dates are "formatted for the active locale" — which for Arabic could
   mean Arabic-Indic digits (٠١٢٣). The old draft's rule (always Latin digits,
   `dd-MM-yyyy`) is a common, defensible choice for Saudi government/enterprise
   systems and helps data consistency. *Recommendation:* this is a genuine UX
   decision — confirm with the owner, then make MAA-001 §10 **explicit** either
   way; today it is ambiguous.

---

## 3. Gaps (in the old draft, in NO current doc)

Not conflicts — items the old draft has that the controlled docs neither confirm
nor contradict. They are document decisions, not agent-memory rules:

- **STS** (Security Token Service) — API-001 issues the JWT directly; no separate
  STS is described.
- **Lookup endpoint** convention (a full unpaged list for dropdowns).
- **Enums mirrored in DB + API + UI** with a `GetEnumList` endpoint.
- **Named base abstractions** (`BaseEndpoint<T>`, `BaseService<T>`, …).
- **CRUD page shape** (popup add/edit, select-all, 20-row grid) — would belong in
  `SIMF-CPD-001`, which does not exist yet.
- **`/simplified` post-change cleanup step.**
- **Arabic translation quality** rule (standard Arabic; synonyms that keep the true
  meaning).

*Recommendation:* raise these with the Solution Architect for inclusion in
`SIMF-SES-001`, `SIMF-API-001` or a future `SIMF-CPD-001`. They are out of scope
for the agent memory work.

---

## 4. Decision log

Fill `Accept` / `Reject` / `Defer` per row, then the controlled docs are updated
in a separate reviewed task.

| # | Conflict | Recommendation | Decision | By | Date |
|---|----------|----------------|----------|----|----|
| 1 | HTTP 200 always | Adopt current doc | | | |
| 2 | Response envelope | Adopt current doc | | | |
| 3 | AR/EN message pair | Adopt current doc | | | |
| 4 | Phone OTP | Adopt current doc | | | |
| 5 | Password reset | Code by email (OI-3) | | | |
| 6 | Flutter on Web | Adopt current doc | | | |
| 7 | X-Language header | Adopt current doc | | | |
| 8 | Smif* components | Follow current docs | | | |
| 9 | Date / number format | Owner decision | | | |

---

End of worksheet.
