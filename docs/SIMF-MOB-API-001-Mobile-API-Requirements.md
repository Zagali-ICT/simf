# Mobile API Requirements (App API)

| Field | Value |
|-------|-------|
| Document ID | SIMF-MOB-API-001 |
| Title | Mobile API Requirements (App API) |
| Version | 0.1 (DRAFT — skeleton, pending owner approval) |
| Status | Draft |
| Classification | Confidential |
| Prepared by | SIMF Engineering Team |
| Owner | SIMF Programme Owner |
| Approver | SIMF Programme Owner |
| Date issued | 2026-06-02 |
| Related documents | SIMF-API-001 (envelope, headers, error model, auth flows), SIMF-MAA-001 (mobile app architecture), SIMF-API-GATES-001 (gate/scan operator API), `src/Mobile/simf_app/lib/app/route_names.dart` (the 41-screen route table), `Mockup.html` (authoritative screen numbering) |

### Revision history

| Version | Date | Author | Summary of change |
|---------|------|--------|-------------------|
| 0.1 | 2026-06-02 | SIMF Engineering Team | First draft **skeleton**. Authentication section (§5) enumerated against the shipped `Endpoints/Auth/*` surface. All screen-keyed App sections (§6–§13) are placeholders to be filled wave-by-wave after Auth is locked. Not yet a controlled document. |

---

## 1. Purpose

This document is the **App API catalogue** — the single, curated contract that an
**independent Flutter developer** works from to build the SIMF mobile app. It lists
**only** the endpoints the mobile app calls, grouped and keyed to the mockup screens,
and deliberately excludes the Control-Panel / admin API surface.

It is a **companion** to SIMF-API-001, not a replacement. It does **not** restate the
`ApiResult<T>` envelope, the standard headers, the authentication header, the device
header, the error model, or the pagination contract — those live in SIMF-API-001 and
apply here unchanged. This document specifies **which** endpoints are in the App
surface, **which mockup screen** each one backs, and **which of the four app
privilege levels** may call it.

> **Status note.** This is a v0.1 skeleton. The Authentication section (§5) is
> populated against the shipped backend. Sections §6–§13 are structural placeholders
> — each carries the mockup screens it covers and the candidate backend endpoints to
> be confirmed and detailed in the corresponding *prepare wave*. No section below §5
> is binding until filled in and the document is approved.

## 2. Scope

In scope:

- The Authentication endpoints the app uses for sign-up, sign-in, the second-factor
  step, password recovery, token refresh, and device-key (biometric) sign-in.
- The read/write endpoints behind each of the 41 mockup screens, grouped by the
  eight mockup sections.
- For each endpoint: the mockup-screen reference, HTTP verb + route, and the minimum
  app privilege level required.

Out of scope:

- The Control-Panel / admin API (`/api/v1/admin/*` and all `Tags("Admin")`
  endpoints). The Flutter developer does not consume these.
- The `ApiResult<T>` envelope, headers, error catalogue, and pagination — see
  SIMF-API-001.
- The gate-operator scan wire contract — that has its own document, SIMF-API-GATES-001;
  this document references it from the Staff screens rather than restating it.
- Request/response field-level schemas for §6–§13 — added per wave when each section
  is filled in.

## 3. Conventions inherited from SIMF-API-001

- **Base path:** all routes are under `/api/v1` (e.g. `POST /api/v1/auth/sign-in`).
  Routes in this document are written without the `/api/v1` prefix.
- **Envelope:** every response is `ApiResult<T>` (`{ success, data, error }`).
- **Headers:** the device header (`X-App-Key`) and the bearer token
  (`Authorization: Bearer …`) are as defined in SIMF-API-001. The app is the
  `mobile` audience; sign-in behaviour follows SIMF-API-001 §"audience".
- **Errors:** the error codes and HTTP status mapping are inherited unchanged.

## 4. App privilege model (the only four)

The app authorises every screen and endpoint call against exactly **four** privilege
levels — its own `AppRole`, distinct from the CP `UserType` and from the per-page
permission catalogue. The level is carried on the JWT (`mobile_app_role` claim) and
mirrored in the sign-in / `users/me` response.

| Level | Wire | Meaning | Typical screens |
|-------|------|---------|-----------------|
| **Guest** | `0` | Not signed in **or** signed-in-but-pending/rejected. Public browsing only. | Public content, own profile + notifications when signed-in. No badge QR. |
| **Visitor** | `1` | Approved attendee. | Full attendee shell + badge QR. |
| **Moderator** (محاور) | `2` | Session moderator (`Other` user, Moderator profile type). | Visitor screens + moderator session screens. |
| **Staff** | `3` | Venue gate operator. | Scan-QR home (see SIMF-API-GATES-001). |

Resolution rules (data-driven via `ProfileType.MobileAppRole`, default `None` =
Visitor-tier) are owned by SIMF-FDS-002 Amendment B and mirrored in SIMF-MAA-001 §8.5.
This document only states the **minimum level** required per endpoint.

## 5. Authentication API (Auth-first — populated)

These back the mockup **Section 1 — Start & entry** screens (`splash`, `onboarding`,
`signIn`, `signUpForm`, `emailOtp`, `signUpVisitor`, `signUpInterests`,
`terms`, `registrationSuccess`, `registrationStatus`, `guestMode`; `signUpType` removed
— D-332, `signUpExhibitor` removed — D-276) plus the auxiliary
auth routes (`forgotPassword`, `resetPassword`, `verifyTotp`). Exact mockup screen
numbers are per `Mockup.html` — reconcile the screen column before approval.

Tag note: the shipped endpoints are tagged `"Authentication"`, **except** the
device-key endpoints which are tagged `"Auth"`. The App-API grouping should unify
these under one App-auth tag (tracked as a §5 cleanup item, no behaviour change).

### 5.1 Registration

| Screen (route) | Verb + route | Access | Purpose |
|----------------|--------------|--------|---------|
| `signUpForm` | `POST /auth/sign-up` | Anonymous | Create account (email + password + confirm). |
| `emailOtp` | `POST /auth/verify-email` | Anonymous | Verify the email with the emailed code. |
| `emailOtp` | `POST /auth/resend-code` | Anonymous | Resend the verification / OTP code. |
| `registrationStatus` | `GET /app/users/me` **(BUILT — D-249; +`profileComplete` D-374)** | Signed-in (incl. pending) | `{id,email,displayName,appRole,preferredLanguage,registrationStatus,avatarUrl,profileComplete}`; pending/approved/rejected drives routing — full contract in [`Page_011_API.md`](App/Page_011/Page_011_API.md). `profileComplete` is server-computed (names + ≥1 interest + male→ID-photo) and drives the app's add-profile-first route after sign-in / the 2FA OTP step (D-374). |

> Sign-up's profile + interests steps (mockup `signUpVisitor` / `signUpExhibitor`)
> call **Account** endpoints (`/account/*`), catalogued in §6, not §5.

### 5.2 Sign-in and second factor

| Screen (route) | Verb + route | Access | Purpose |
|----------------|--------------|--------|---------|
| `signIn` | `POST /auth/sign-in` | Anonymous | Password step; returns the second-factor challenge. |
| `emailOtp` | `POST /auth/verify-otp` | Anonymous | Email-OTP second factor (Visitor path). |
| `verifyTotp` | `POST /auth/verify-totp` | Anonymous | TOTP second factor (admin/staff path). |
| `verifyTotp` | `POST /auth/verify-recovery-code` | Anonymous | Recovery-code second factor. |
| *(silent)* | `POST /auth/refresh` | Anonymous (refresh token in body) | Exchange refresh token for a new access token. |
| `more` / settings | `POST /auth/sign-out` | Authenticated | End every session for the account. |

### 5.3 Password recovery & change

| Screen (route) | Verb + route | Access | Purpose |
|----------------|--------------|--------|---------|
| `forgotPassword` | `POST /auth/forgot-password` | Anonymous | Request a reset code by email. |
| `resetPassword` | `POST /auth/reset-password` | Anonymous | Set a new password using the emailed code. |
| settings | `POST /auth/change-password` | Authenticated | Change own password. |
| forced-change | `POST /auth/complete-password-change` | Anonymous | Complete a forced password change. |

### 5.4 TOTP enrolment (admin/staff app users)

| Verb + route | Access | Purpose |
|--------------|--------|---------|
| `POST /auth/totp/setup` | Authenticated | Begin TOTP enrolment. |
| `GET /auth/totp/pairing` | Authenticated | Fetch the pairing secret / QR. |
| `POST /auth/totp/pairing/verify` | Authenticated | Verify the pairing code. |
| `POST /auth/totp/confirm` | Authenticated | Confirm and activate TOTP. |
| `POST /auth/totp/disable` | Authenticated | Disable TOTP. |

### 5.5 Device-key (biometric) sign-in

| Verb + route | Access | Purpose |
|--------------|--------|---------|
| `POST /auth/device-keys` | Approved account | Register a device key. |
| `GET /auth/device-keys` | Approved account | List my device keys. |
| `DELETE /auth/device-keys/{id}` | Approved account | Revoke my device key. |
| `POST /auth/device-keys/{id}/challenge` | Anonymous | Issue a sign-in challenge. |
| `POST /auth/sign-in-with-device-key` | Anonymous | Complete biometric/device sign-in. |

*(`DELETE /admin/device-keys/{id}` is an admin/CP endpoint and is **out of scope** here.)*

---

> **Sections 6–13 below are skeletons.** Each lists the mockup screens it covers and
> the candidate backend endpoints to be confirmed and detailed (verb, route, access,
> request/response) in that section's prepare wave. Nothing below is binding yet.

## 6. Account & My-Area dashboard API  *(mockup Section 2 — Screen 14 `myArea`)*

> **Per-page docs are the home for screen detail.** Screen 14's full API contract —
> the `GET /account/dashboard` aggregate plus `GET /account/calendar.ics` and
> `GET /account/contact-card.vcf`, with the counter/schedule rules — lives in the
> page folder **[`docs/App/Page_014/`](App/Page_014/README.md)**
> (see [`Page_014_API.md`](App/Page_014/Page_014_API.md),
> [`Page_014_Logic.md`](App/Page_014/Page_014_Logic.md)). This section is an index only.

Summary: one read-only aggregate `GET /account/dashboard` (identity card + two counters
+ today's merged schedule), plus `calendar.ics` (full schedule, RFC 5545) and
`contact-card.vcf` (vCard) for the native share intent. All **additive, no schema
change**. App routes are under **`/api/v1/app/*`** (App↔CP split shipped, D-247) — e.g. `GET /api/v1/app/account/dashboard`.
**BUILT (D-249):** all three routes shipped; the meeting counter unions accepted
speaker meetings with confirmed business meetings (D-248). Full contract +
counter/schedule rules in [`Page_014_API.md`](App/Page_014/Page_014_API.md).

Other Section-2 account endpoints (sign-up profile steps, countries, interests,
ProfileType picker, ID-document upload — `Endpoints/Account/*`) are detailed in a later wave.

## 7. Programme & sessions API  *(mockup Section 2 — `agenda`, `sessionDetail`, `mySeat`, `speakers`, `speakerProfile`, `venueMap`)*

Covers: public agenda/sessions, session detail, seat reservation/booking, public
speakers + speaker profile, 2D venue map nodes.
*Candidate endpoints (to confirm): `Endpoints/Programme/*`, `Endpoints/Sessions/SeatReservationEndpoints`, `Endpoints/Public/PublicSpeakerEndpoints`, `Endpoints/Public/PublicVenueMapEndpoints`. Detail TBD.*

## 8. Content & activities API  *(mockup Section 3 — `delegations`, `booths`, `sponsors`, `archive`)*

Covers: delegations, exhibition booths + companies, sponsors, archive editions.
*Candidate endpoints (to confirm): `Endpoints/Delegations/*`, `Endpoints/Public/PublicBoothEndpoints`, `Endpoints/Sponsors/*`, `Endpoints/Archive/*`. Detail TBD.*

## 9. Live & Q&A API  *(mockup Section 4 — `liveBroadcast`, `sendQuestion`, `requestInterview`, `audienceComments`)*

Covers: live broadcast/stream, submit session question (advisory AI filter),
meeting/interview request, audience comments + likes.
*Candidate endpoints (to confirm): `Endpoints/Ai/LiveAiEndpoints`, `Endpoints/Programme/SessionRecordingStreamEndpoints`, `Endpoints/Sessions/SessionQuestionEndpoints`, `Endpoints/Sessions/MeetingRequestEndpoints`, `Endpoints/Sessions/SessionCommentEndpoints`. Detail TBD.*

## 10. Media coverage API  *(mockup Section 5 — `news`, `gallery`, `mediaPartners`)*

Covers: public news feed + article, media gallery, media partners.
*Candidate endpoints (to confirm): `Endpoints/News/PublicNewsEndpoints`, `Endpoints/Public/PublicMediaEndpoints`, `Endpoints/PublicRelations/MediaPartnerEndpoints`. Detail TBD.*

## 11. Badge & notifications API  *(mockup Section 6 — `badge`, `notifications`)*

Covers: visitor badge / QR, notification list + read state.
*Candidate endpoints (to confirm): `Endpoints/Account/NotificationEndpoints`; badge/QR source TBD. Detail TBD.*

## 12. Smart features API  *(mockup Section 7 — `aiSummary`, `meetPeople`, `chatbot`, `aboutForum`)*

Covers: AI session summary (محضر), "meet people like you" recommendations, chatbot,
about-forum CMS.
*Candidate endpoints (to confirm): `Endpoints/Ai/AiFeatureEndpoints`, `Endpoints/Recommendations/MeetPeopleLikeYouEndpoint`, `Endpoints/Public/PublicCmsEndpoints`. Detail TBD.*

## 13. Settings, legal & feedback API  *(mockup Section 8 — `accessibility`, `cybersecurity`, `rate`, `more`)*

Covers: accessibility/cybersecurity CMS pages, rating/feedback, settings (sign-out,
language, password — cross-referenced to §5).
*Candidate endpoints (to confirm): `Endpoints/Public/PublicCmsEndpoints`, `Endpoints/Feedback/FeedbackEndpoints`. Detail TBD.*

## 14. Staff / gate-operator API  *(Staff privilege — `scanQr` home)*

The gate-operator scan surface is **already specified** in SIMF-API-GATES-001
(`/api/v1/gates/*`). This document references it for the Staff app rather than
restating it. *Cross-reference only; no new contract here.*

---

## Appendix A — Open items before this becomes controlled

1. **Reconcile screen numbers.** Fill the "Screen" columns with the explicit
   `Mockup.html` numbers (the route table maps names → numbers; numbers not yet
   transcribed here).
2. **Separation approach — Approach B (physical split). Phase 1 SHIPPED (D-247,
   2026-06-03).** App + public routes are now under `/api/v1/app/*` and the CP/admin
   surface under `/api/v1/admin/*`, with two OpenAPI documents (`SIMF App API`,
   `SIMF CP API`). `SIMF-API-001` was amended to v1.3 (§3/§4/§13). **Phase 2** — the
   physical directory reorg (`Endpoints/App` + `Endpoints/Cp`, splitting the mixed
   files) for a future project extraction — is the remaining, reorganisation-only step.
3. **App-auth tag unification.** Decide whether to unify the `"Authentication"` /
   `"Auth"` tags under one App-auth grouping (folds into the Approach-B split).
4. **Fill §6–§13** wave-by-wave after Auth is locked, each with verb/route/access and
   request/response schemas.
5. **Build the three new Screen-14 reads** (§6.2–§6.4): `GET /account/dashboard`,
   `GET /account/calendar.ics`, `GET /account/contact-card.vcf` — additive read
   aggregates, no schema change. Pending the §11 build plan + owner approval.
6. **DEPENDENCY — B2B/B2C meeting entity (CP-created) does not exist yet.** The
   Screen-14 meetings counter (§6.2.2) unions speaker `MeetingRequest`s (built) with
   admin-arranged B2B/B2C meetings (**not built** — needs a new entity + CP module +
   additive migration). Until it ships, the counter/schedule reflect speaker meetings
   only. Scope + design TBD with the owner.
