# Test-Case Sheet — `Sign Up — profile data` (app screen #7)

> Author sections 1–4 once; the tester fills the **bold** columns during the run.
> **A new build is a new run** — copy this sheet rather than overwriting a result.

---

## 1. Identification and run context

| | | | |
|---|---|---|---|
| **Screen name** | بيانات الملف الشخصي · Sign Up — profile data | **Doc id** | `TC-MOB-SUV` |
| **Route / screen id** | `/sign-up/visitor` (`RouteNames.signUpVisitor`) — app screen **#7**. **Auth-gated** (Page_007 L-1) | **Surface** | Mobile app (Flutter) |
| **APIs under test** | `GET /app/account/user-profile` (pre-fill) · `GET …/user-profile/countries` · `GET …/profile-types?isVisitor=` · `GET /app/organisations?search=&top=` — **no POST on this screen** | **Audience** | Visitor — signed in, profile incomplete |
| **Build / version under test** | _(fill)_ | **Environment** | Local / Test / Staging / Production |
| **Device + OS** | _(fill — a real device is **required**; the face capture cannot be driven on an emulator)_ | **Language run** | Arabic (RTL) / English (LTR) |
| **Tester name** | _(fill)_ | **Test date** | _(fill)_ |
| **Reviewed by (QA Lead)** | _(fill)_ | **Review date** | _(fill)_ |
| **Reference docs** | [pages/mobile/sign-up-visitor/](../../pages/mobile/sign-up-visitor/README.md) · [e2e/mobile-sign-up-visitor.md](../../tests/e2e/mobile-sign-up-visitor.md) `E2E-MOB007-001…025` · Figma `168:2972` · D-332 / D-368 / D-371 / D-434 / D-437 / D-459 / D-469 / D-471 / D-546 / D-662 / D-694 / D-698 / D-722 / D-723 | | |

### The rules this screen enforces — all read from source

| Area | Rule |
|---|---|
| **Registration type** | Two segmented tabs — **Visitor** / **Other**. The choice filters the ProfileType lookup via `?isVisitor=`. |
| **Visitor tab** | Shows **no** ProfileType picker. The draft auto-carries the seeded **"Normal" (عادي)** id. The server rejects any other audience-tier self-pick with **400** (C5, D-371). |
| **Other tab** | Shows a filtered ProfileType **dropdown** (D-722 — a simple dropdown, not the full-screen search sheet). A pick is **required**. |
| **Arabic name** | Arabic letters and spaces only, filtered at the keystroke. **2 to 4 parts.** |
| **English name** | Latin letters and spaces only, filtered at the keystroke. **2 to 4 parts.** |
| **Job title (English)** | **Required.** Latin letters and spaces only. Label "المسمى الوظيفي (بالإنجليزية)", LTR. |
| **Job title (Arabic)** | **Optional.** Arabic letters and spaces only. Label "المسمى الوظيفي (بالعربية)", RTL. |
| **Date of birth** | **Required.** Registrant must be **18 or over** — the picker caps at today − 18 (D-197). |
| **Saudi national ID** | `^1\d{9}$` **plus Luhn**, re-checked server-side. |
| **Non-Saudi document** | Iqama `^2\d{9}$` plus Luhn, **or** passport 6–9 characters. **One is required.** |
| **Place of birth** | **Required.** Saudi → searchable picker over the **13 official Saudi regions**, code-keyed. Non-Saudi → free text with an "as in passport" hint. |
| **Mobile number** | **Required.** Saudi `0501234567` / `+966501234567` with separators. International **E.164** `+447700900123`. Field cap **17**. |
| **Plate number** | **Optional.** Three searchable pickers over the **official 17 Saudi plate letters** (Arabic or Latin) plus a **1–4 digit** field. Either order. Stored as the canonical Latin code; the response returns both `plateNumberAr` and `plateNumberEn`. |
| **ID image** | **Mandatory for everyone.** Gallery pick of the ID **document**. **No face check** on this one — it is a document. |
| **Face photo** | **Mandatory for men, optional for women.** Captured only through the guided face-detection / liveness screen — **live only, no gallery fallback** (D-662). |
| **Organisation** | Debounced typeahead; selecting sets the id; Clear unlinks. |
| **Next** | Carries the form state to the interests screen. **Nothing is saved here** — the single save happens on the interests screen. |

> **Nothing on this screen writes to the server.** Every row in §D therefore
> tests either a **read** endpoint or the **server-side mirror** of a client rule,
> exercised against the profile-upsert endpoint directly.

## 2. Pre-conditions, fixtures and test data

| Item | Value / how to obtain |
|---|---|
| **FX-1** Signed-in visitor, profile incomplete | reached by completing sign-up and signing in |
| **FX-2** Saudi registrant | for the national-ID and region-picker branches |
| **FX-3** Non-Saudi registrant | for the Iqama / passport and free-text branches |
| **FX-4** Male registrant · **FX-5** Female registrant | for the face-photo gate |
| **FX-6** Returning registrant with a stored profile | for the pre-fill rows |
| **FX-7** Legacy stored place of birth that is **not** a region | for the region-picker preservation row |
| **Valid Saudi national ID** | any `1`-prefixed 10-digit value that **passes Luhn** — derive one at run time; do not reuse a real citizen's number |
| **Valid Iqama** | any `2`-prefixed 10-digit value that passes Luhn |
| **Valid plates** | `ABJ1234` · `abj 1234` · `1234-ABJ` · `ابح1234` · `ابح١٢٣٤` |
| **Invalid plates** | 2 letters · 4 letters · 5 digits · digits only · symbols · out-of-set letters `C` and `ج` |
| Device | A **real handset** — the face capture needs a camera and the on-device liveness check. Include the Huawei / no-GMS device. |
| Cleanup | Accounts and uploaded images tagged `QA-`; added to the cleanup register. |

> **Use synthetic identity data only.** Never enter a real person's national ID,
> Iqama, passport number or photograph into a test environment.

## 3. Inherited common cases

| Block | Applies | **Status** | **Notes** |
|---|---|---|---|
| CB-01 Render, console and overflow | yes | | |
| CB-02 Arabic RTL and English LTR | yes | | |
| CB-03 Loading, empty and error states | yes — four lookups load here | | |
| CB-04 Auth gate and account state | yes — **auth-gated** screen | | |
| CB-05 Session expiry and token refresh | yes — a long form can outlive an access token | | |
| CB-06 Network failure and retry | yes | | |
| CB-07 Server 500 and malformed payload | yes | | |
| CB-08 Accessibility baseline | yes | | |
| CB-09 Pull-to-refresh | **N-A** — form | | |
| CB-10 Audit trail | partial — no write on this screen | | |

## 4. Test cases

**Status:** `PASS` · `FAIL` · `BLOCKED` · `N-A` · `NOT-RUN` · `PROD-SKIP`

### A. Render and layout

| ID | Area | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-SUV-A01` | Screen chrome | P1 | Open the screen as **FX-1**. | Navy header with logo, forum name and the wired **globe** toggle; the form in a beige card; visitor/other and document-type as **segmented tabs**; gender as **two radio pills** with the gold ring on the leading edge then the label; the bordered "إرفاق ملف" attach control; an underlined terms link. | Figma `168:2972`, D-368, D-698 | | | | |
| `TC-MOB-SUV-A02` | Info banner on entry | P0 | Open the screen after being routed here for an incomplete profile. | An info banner explains **why** the user is here. A user bounced into a long form with no explanation is a defect. | `E2E-MOB007-019` (D-434) | | | | |
| `TC-MOB-SUV-A03` | Placeholder avatar | P1 | Open the screen before capturing a face. | A placeholder person icon sits at the top of the card. | `E2E-MOB007-020` | | | | |
| `TC-MOB-SUV-A04` | Avatar swaps after capture | P1 | Capture the face photo. | The placeholder is replaced by the **captured face**. | `E2E-MOB007-020` (D-437) | | | | |
| `TC-MOB-SUV-A05` | Job-title labels are unambiguous | P1 | Read both job-title fields. | They are language-labelled — "المسمى الوظيفي (بالإنجليزية)" (LTR) and "المسمى الوظيفي (بالعربية)" (RTL) — so which is which is never in doubt. | `E2E-MOB007-025` | | | | |
| `TC-MOB-SUV-A06` | Plate control shape | P1 | Open the plate control. | **Three** searchable letter pickers plus a **1–4 digit** field — the same beige search sheet the country and region pickers use. | `E2E-MOB007-016` (D-459 / D-471) | | | | |
| `TC-MOB-SUV-A07` | Tablet width | P1 | Open on a tablet in portrait. | The card fills the frame; pickers and sheets are proportionate; no dead side gutters. | responsive rule §13.7 | | | | |
| `TC-MOB-SUV-A08` | Long form and the keyboard | P0 | Focus each field in turn on a small phone. | Every field scrolls clear of the keyboard; no field is permanently hidden behind it; the Next action stays reachable. | CB-01 | | | | |
| `TC-MOB-SUV-A09` | No horizontal overflow | P1 | Scroll the whole form in both languages. | Nothing is clipped at any edge at any scroll position. | CB-01.3 | | | | |

### B. Field validation (client)

| ID | Field | Pri | Test data | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-SUV-B01` | All-empty submit | P0 | Tap **Next** on an empty form. | Required errors appear on the Arabic name, English name, nationality and date of birth; **Next is blocked**; **no** request fires; and the bilingual "complete the required fields" **toast** appears — not a silent no-op. | `E2E-MOB007-004`, `-019` | | | | |
| `TC-MOB-SUV-B02` | Arabic name — script filter | P0 | Type Latin letters, then digits, into the Arabic name. | Latin characters and digits are **filtered at the keystroke** and never appear. | `E2E-MOB007-021` | | | | |
| `TC-MOB-SUV-B03` | English name — script filter | P0 | Type Arabic letters, then digits, into the English name. | Arabic characters and digits are filtered at the keystroke. | `E2E-MOB007-021` | | | | |
| `TC-MOB-SUV-B04` | Name — part-count boundary | P0 | Enter **1**, then 2, 3, 4, then **5** parts in each name field. | 1 part and 5 parts are **blocked** with "enter your full name (2 to 4 parts)". 2, 3 and 4 parts are accepted. | `E2E-MOB007-021` | | | | |
| `TC-MOB-SUV-B05` | Name — extra whitespace | P1 | `"  محمد   عبدالله  "` with double spaces. | Extra whitespace does not create phantom parts that trip the 2–4 rule. Record the exact behaviour. | `E2E-MOB007-021` | | | | |
| `TC-MOB-SUV-B06` | Job title (English) — required | P0 | Leave it empty and tap Next. | Blocked with "job title is required". | `E2E-MOB007-023` (D-723) | | | | |
| `TC-MOB-SUV-B07` | Job title (English) — script filter | P1 | Type Arabic letters and digits into it. | Filtered at the keystroke — it can never hold Arabic. | `E2E-MOB007-025` | | | | |
| `TC-MOB-SUV-B08` | Job title (Arabic) — optional | P0 | Leave it empty and tap Next. | **Next still advances.** This field and the plate are the only optional ones. | `E2E-MOB007-024` (#37) | | | | |
| `TC-MOB-SUV-B09` | Job title (Arabic) — script filter | P1 | Type Latin letters into it. | Filtered at the keystroke — it can never hold Latin. | `E2E-MOB007-025` | | | | |
| `TC-MOB-SUV-B10` | Date of birth — required | P0 | Leave it empty and tap Next. | Blocked with its required error. | `E2E-MOB007-007` | | | | |
| `TC-MOB-SUV-B11` | Date of birth — **age 18 boundary** | P0 | Open the picker; attempt to select today − 17, exactly today − 18, and today − 19. | The picker **caps at today − 18**: an under-18 date cannot be selected. Exactly 18 and older are accepted. | `E2E-MOB007-007` (D-197) | | | | |
| `TC-MOB-SUV-B12` | Saudi national ID — format | P0 | `1234567890` (wrong prefix) · a `1`-prefixed 9-digit · a `1`-prefixed 11-digit · a valid `1`-prefixed 10-digit passing Luhn | Only the `^1\d{9}$` value that also **passes Luhn** is accepted. | `E2E-MOB007-005` | | | | |
| `TC-MOB-SUV-B13` | Saudi national ID — **Luhn** | P0 | A `1`-prefixed 10-digit value that **fails** the Luhn check. | Rejected inline. A format-only check that accepts it is a defect. | `E2E-MOB007-005` | | | | |
| `TC-MOB-SUV-B14` | Non-Saudi — document picker appears | P0 | Switch nationality to non-Saudi. | The Iqama / Passport picker appears and the Saudi national-ID field is replaced. | `E2E-MOB007-006` | | | | |
| `TC-MOB-SUV-B15` | Iqama — format and Luhn | P0 | A `1`-prefixed value; a `2`-prefixed 9-digit; a `2`-prefixed 10-digit failing Luhn; a valid one. | Only `^2\d{9}$` **passing Luhn** is accepted. | `E2E-MOB007-006` | | | | |
| `TC-MOB-SUV-B16` | Passport — length boundary | P0 | 5, 6, 9 and 10 characters. | 6 through 9 accepted; 5 and 10 rejected. | `E2E-MOB007-006` | | | | |
| `TC-MOB-SUV-B17` | Non-Saudi — one document required | P0 | Leave both Iqama and passport empty and tap Next. | Blocked — **one** of the two is required. | `E2E-MOB007-006` | | | | |
| `TC-MOB-SUV-B18` | Place of birth — required | P0 | Leave it empty and tap Next. | Blocked with "place of birth is required". | `E2E-MOB007-023` (D-723) | | | | |
| `TC-MOB-SUV-B19` | Place of birth — Saudi region picker | P0 | As **FX-2**, open the place-of-birth control. | A **searchable picker over the 13 official Saudi regions** opens — the same beige search sheet the country picker uses. Free text is not offered. | `E2E-MOB007-022` (D-469 / D-471) | | | | |
| `TC-MOB-SUV-B20` | Region picker — cross-locale preselect | P1 | Store a region in one language, then reopen the screen in the **other** language. | The picker is **code-keyed**, so the stored region still preselects correctly. | `E2E-MOB007-022` | | | | |
| `TC-MOB-SUV-B21` | Region picker — legacy value preserved | P1 | Open as **FX-7**, whose stored place of birth is legacy free text that is not a region. | The stored value is **kept, not erased**. Silently wiping a user's data is a defect. | `E2E-MOB007-022` | | | | |
| `TC-MOB-SUV-B22` | Place of birth — non-Saudi free text | P0 | As **FX-3**, open the control. | A free-text field with an "as in passport" hint — not the region picker. | `E2E-MOB007-022` | | | | |
| `TC-MOB-SUV-B23` | Nationality switch reconciles the field | P0 | Pick a Saudi region, then switch nationality to non-Saudi, then back. | The place-of-birth field reconciles sensibly in both directions without leaving an invalid or orphaned value. | `E2E-MOB007-022` | | | | |
| `TC-MOB-SUV-B24` | Mobile — required | P0 | Leave it empty and tap Next. | Blocked with "mobile number is required". | `E2E-MOB007-023` (D-723) | | | | |
| `TC-MOB-SUV-B25` | Mobile — **Saudi accepted forms** | P0 | `0501234567` · `+966501234567` · the same with separators | All accepted. | `E2E-MOB007-012` (C4 / D-371) | | | | |
| `TC-MOB-SUV-B26` | Mobile — **Saudi rejected forms** | P0 | `0412345678` (wrong prefix) · a 9-digit · an 11-digit · `+9664…` | All rejected inline **and** by the server with a 400. | `E2E-MOB007-012` | | | | |
| `TC-MOB-SUV-B27` | Mobile — **international accepted** | P0 | `+447700900123`, and the same with a dash | Accepted as E.164. | `E2E-MOB007-013` (D-371) | | | | |
| `TC-MOB-SUV-B28` | Mobile — **international rejected** | P0 | `0044…` · `+0…` · a too-short number | All rejected inline and server-side. | `E2E-MOB007-013` | | | | |
| `TC-MOB-SUV-B29` | Mobile — length cap | P1 | Attempt an 18-character value. | The field caps at **17**. | `mobile_field.dart maxLength = 17` | | | | |
| `TC-MOB-SUV-B30` | Plate — optional | P0 | Leave the plate empty and tap Next. | **Next advances.** The plate is optional. | `E2E-MOB007-016` | | | | |
| `TC-MOB-SUV-B31` | Plate — accepted forms | P0 | `ABJ1234` · `abj 1234` · `1234-ABJ` · `ابح1234` · `ابح١٢٣٤` | All accepted, in either letters-then-digits or digits-then-letters order, in Arabic or Latin, with Arabic-Indic digits. | `E2E-MOB007-016` | | | | |
| `TC-MOB-SUV-B32` | Plate — rejected forms | P0 | 2 letters · 4 letters · 5 digits · digits only · symbols · the out-of-set letters `C` and `ج` | All rejected inline and server-side. Only the **official 17** plate letters are selectable. | `E2E-MOB007-016` | | | | |
| `TC-MOB-SUV-B33` | Plate — canonical round-trip | P0 | Enter `ابح١٢٣٤`, advance, then reopen the profile. | Stored as the canonical **Latin code**; the response carries **both** `plateNumberAr` (`ابح١٢٣٤`) and `plateNumberEn` (`ABJ1234`); the screen redisplays it correctly in the current language. | `E2E-MOB007-016` | | | | |
| `TC-MOB-SUV-B34` | Organisation typeahead | P1 | Type into the organisation field. | The search is **debounced** — it does not fire per keystroke. Selecting a result sets the id; **Clear** unlinks it; a search with no match shows an empty state, not an error. | `E2E-MOB007-008` | | | | |
| `TC-MOB-SUV-B35` | Gender selection | P0 | Select each gender pill. | Selecting one deselects the other; the choice is unambiguous; the selection drives the face-photo rule in §C. | D-698 | | | | |

### C. Functional and business rules

| ID | Action | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-SUV-C01` | Golden path | P0 | Fill every required field validly as **FX-2 / FX-4**, attach the ID image, capture the face, then tap **Next**. | The app advances to the **interests** screen carrying the full form state. **No POST** is made on this screen. | `E2E-MOB007-003` | | | | |
| `TC-MOB-SUV-C02` | Registration-type tabs filter the lookup | P0 | Pick **Visitor**, then **Other**. | The ProfileType lookup is re-fetched with the matching `?isVisitor=` value each time. | `E2E-MOB007-001` | | | | |
| `TC-MOB-SUV-C03` | Switching type clears an invalid pick | P0 | On **Other** select a profile type, then switch to **Visitor** and back. | A now-invalid ProfileType selection is **cleared** rather than silently carried across. | `E2E-MOB007-002` | | | | |
| `TC-MOB-SUV-C04` | **Visitor tab hides the picker** | P0 | Select the **Visitor** tab. | **No** ProfileType picker is shown, and the draft auto-carries the seeded **"Normal" (عادي)** id. A visitor must not be able to self-select an audience tier. | `E2E-MOB007-014` (C5 / D-371) | | | | |
| `TC-MOB-SUV-C05` | **Other tab requires a pick** | P0 | Select **Other** and tap Next without choosing a profile type. | Blocked with an inline error. The control is a simple **dropdown**, not the full-screen search sheet. | `E2E-MOB007-015` (D-722) | | | | |
| `TC-MOB-SUV-C06` | **ID image is mandatory for everyone** | P0 | Complete every field, omit the **ID image**, tap Next — as a man and as a woman. | Blocked in **both** cases with "an ID image is required". | `E2E-MOB007-017` (D-437) | | | | |
| `TC-MOB-SUV-C07` | **Face photo is mandatory for men** | P0 | As **FX-4** (male), complete everything including the ID image but omit the face photo, tap Next. | Blocked with "a face photo is required — capture it with the camera", and the requirement is signalled **up front**, not only on the blocked tap. | `E2E-MOB007-017` | | | | |
| `TC-MOB-SUV-C08` | **Face photo is optional for women** | P0 | As **FX-5** (female), complete everything with the ID image but **no** face photo, tap Next. | **Advances.** | `E2E-MOB007-017` | | | | |
| `TC-MOB-SUV-C09` | ID image takes no face check | P1 | Attach an ID document image with no recognisable face. | Accepted. The ID image is a **document** — the self-service id-image endpoint does not face-gate it. | `E2E-MOB007-018` | | | | |
| `TC-MOB-SUV-C10` | **Face capture is live only** | P0 | Open the face capture and look for a gallery option. | There is **no gallery fallback**. The photo can only be captured live through the guided face-detection screen. | `E2E-MOB007-018` (D-662) | | | | |
| `TC-MOB-SUV-C11` | Guided liveness sequence | P0 | Run the face capture on a real device. | The guided sequence (smile, turn, turn) runs and the on-device face and liveness check gates the result. A static photo held to the camera should not pass. | `E2E-MOB007-018` | | | | |
| `TC-MOB-SUV-C12` | Face capture is reachable while pending | P0 | Reach the face capture from a **pending** sign-up account. | The route is universal-auth, so a pending account **reaches it** rather than bouncing to Home. | `E2E-MOB007-018` (D-694) | | | | |
| `TC-MOB-SUV-C13` | Camera permission | P0 | Deny the camera permission, then open the face capture. | A clear localized explanation and a route to settings. No crash, and no silent failure that looks like a broken button. | `E2E-MOB007-018` | | | | |
| `TC-MOB-SUV-C14` | Pre-fill on re-entry | P0 | Reopen the screen as **FX-6**. | Every stored field is pre-filled from `GET /app/account/user-profile`, including the Arabic job title and the place of birth. | `E2E-MOB007-024`, `-022` | | | | |
| `TC-MOB-SUV-C15` | Empty lookup is not an error | P1 | Force a lookup to return zero rows. | The picker shows its **empty state** — never a blocking error that traps the user in the form. | `E2E-MOB007-010` | | | | |
| `TC-MOB-SUV-C16` | Terms link | P1 | Tap the underlined terms link. | The terms screen opens and returns cleanly to the form with the entered data intact. | D-368 | | | | |
| `TC-MOB-SUV-C17` | Globe toggle preserves entered data | P1 | Fill several fields, then switch language. | The language switches and **no entered data is lost**. Losing a half-completed long form on a language switch is a defect. | CB-02.6 | | | | |
| `TC-MOB-SUV-C18` | Back navigation preserves or warns | P1 | Fill several fields, then tap back. | Either the data is preserved on return, or the user is warned before losing it. Silent loss of a long form is a defect. | — | | | | |
| `TC-MOB-SUV-C19` | Double-tap Next | P1 | Tap Next twice rapidly on a valid form. | Advances once; the interests screen is not pushed twice. | CB-06.5 | | | | |

### D. Server-side and NCA security

> The client rules above are mirrored server-side. **Run these against the
> profile-upsert endpoint directly**, bypassing the app.

| ID | Control | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-SUV-D01` | **Auth gate** | P0 | Open `/sign-up/visitor` with **no** session. | Redirected to sign-in. Protected content does not flash first. | `E2E-MOB007-009` | | | | |
| `TC-MOB-SUV-D02` | Lookups require a session | P0 | Call the four lookup endpoints with no token. | Refused. These are signed-in endpoints even though they carry no role or permission requirement. | catalogue header (D7) | | | | |
| `TC-MOB-SUV-D03` | **Audience-tier self-pick is refused** | P0 | POST the profile upsert with a ProfileType other than the seeded "Normal" while on the visitor audience. | **400.** A visitor cannot promote themselves to a privileged tier by crafting the request, even though the client hides the picker. | `E2E-MOB007-014` (C5) | | | | |
| `TC-MOB-SUV-D04` | Name rules mirrored | P0 | POST names with 1 part, 5 parts, and mixed script. | Each rejected with **400** by `UpsertUserProfileRequestValidator`. | `E2E-MOB007-021` | | | | |
| `TC-MOB-SUV-D05` | Age rule mirrored | P0 | POST a date of birth under 18. | Rejected server-side. The picker cap is not the only gate. | `E2E-MOB007-007` | | | | |
| `TC-MOB-SUV-D06` | National ID and Luhn mirrored | P0 | POST a wrong-prefix ID and a Luhn-failing ID. | Both rejected server-side. | `E2E-MOB007-005` | | | | |
| `TC-MOB-SUV-D07` | Iqama and passport mirrored | P0 | POST an invalid Iqama, an out-of-range passport, and neither. | Each rejected server-side. | `E2E-MOB007-006` | | | | |
| `TC-MOB-SUV-D08` | Mobile rules mirrored | P0 | POST each rejected Saudi and international form from B26 and B28. | Each returns **400**. | `E2E-MOB007-012`, `-013` | | | | |
| `TC-MOB-SUV-D09` | Plate rules mirrored | P0 | POST each rejected plate form from B32, then a valid Arabic plate. | Invalid forms return 400; the valid Arabic plate round-trips to the canonical Latin code with both language forms returned. | `E2E-MOB007-016` | | | | |
| `TC-MOB-SUV-D10` | Required fields mirrored | P0 | POST with the job title, place of birth and mobile each omitted in turn. | Each rejected server-side. | `E2E-MOB007-023` | | | | |
| `TC-MOB-SUV-D11` | **Over-posting is refused** | P0 | POST the upsert with `accountState`, `isApproved`, `role`, `userType`, `id` and another user's `userId`. | Every one is ignored. The caller cannot approve themselves, change their role, or write another user's profile. | A4, A1 | | | | |
| `TC-MOB-SUV-D12` | Profile is scoped to the caller | P0 | POST the upsert while authenticated as user A, targeting user B. | Refused. The endpoint operates on the caller's own `sub` only. | A1 | | | | |
| `TC-MOB-SUV-D13` | Image upload — type and size | P0 | Upload a non-image file, an oversized file, and a file with a mismatched extension. | Each refused with a clear error. A renamed executable must not be accepted as an ID image. | A6 | | | | |
| `TC-MOB-SUV-D14` | Image upload — access control | P0 | Retrieve another user's uploaded ID image by guessing or altering its identifier. | Refused. An ID document is highly sensitive; it must be reachable only by its owner and by authorised staff. | A1, A9-9 | | | | |
| `TC-MOB-SUV-D15` | Face image is not accepted from the gallery server-side | P0 | POST an avatar image directly, bypassing the liveness screen. | The server enforces its own gate on the avatar path — a gallery image must not become the face photo. Record the exact server behaviour. | `E2E-MOB007-018` (D-662) | | | | |
| `TC-MOB-SUV-D16` | Organisation search is not an enumeration tool | P1 | Call the organisation lookup with an empty search and a very large `top`. | Results are bounded; the endpoint does not dump the whole table or leak non-public organisation data. | A1-14 | | | | |
| `TC-MOB-SUV-D17` | Personal data in transit | P0 | Capture the requests carrying the national ID, mobile and images. | TLS only. No identity document is sent over plain HTTP. | A5 | | | | |
| `TC-MOB-SUV-D18` | Personal data at rest on the device | P0 | Inspect local storage and the recents thumbnail after filling the form. | The national ID, mobile number and captured images are not left in unencrypted local storage, and the recents snapshot does not expose them. | A11, A2 | | | | |
| `TC-MOB-SUV-D19` | No personal data in the log | P0 | Capture the device log across a full run. | No national ID, Iqama, passport number, mobile number or image payload is printed. | A9-9 | | | | |
| `TC-MOB-SUV-D20` | Session expiry mid-form | P0 | Fill the long form, let the access token expire, then tap Next. | The token refreshes silently and the form is **not** lost. If the absolute session has ended, the user is signed out cleanly with their data preserved or an explicit warning. | CB-05.1, CB-05.3 | | | | |

### E. Error handling and resilience

| ID | Condition | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-SUV-E01` | Lookup fails | P0 | Force each of the four lookups to fail in turn. | The affected picker shows an error with a retry; the rest of the form stays usable. A single failed lookup must not block the whole screen. | CB-03.3 | | | | |
| `TC-MOB-SUV-E02` | Pre-fill fails | P1 | Force `GET /app/account/user-profile` to fail. | The form opens empty and usable with an error surfaced — not a blank screen or a spinner that never resolves. | CB-03.3 | | | | |
| `TC-MOB-SUV-E03` | Offline | P0 | Turn the network off and open the screen. | A clear offline state with a retry. The user is not stranded. | CB-06.1 | | | | |
| `TC-MOB-SUV-E04` | Image upload fails | P0 | Force the ID-image upload to fail. | A retryable failure is surfaced. The app **must not** report the image as attached when it was not. | CB-06.2 | | | | |
| `TC-MOB-SUV-E05` | Image upload retry | P1 | Retry after E04. | The upload completes and the image is attached exactly once. | CB-06.3 | | | | |
| `TC-MOB-SUV-E06` | Face capture interrupted | P1 | Start the face capture, then take a call or background the app mid-sequence. | Returning leaves a consistent state — either the capture resumes or it restarts cleanly. No crash and no half-captured avatar. | — | | | | |
| `TC-MOB-SUV-E07` | Server 500 | P1 | Force a 500 on a lookup. | Localized fallback; no crash; no stack trace shown. | CB-07 | | | | |
| `TC-MOB-SUV-E08` | Slow network on a long form | P2 | Throttle the connection and work through the form. | The typeahead stays responsive; pickers do not freeze the screen; no duplicate lookup requests stack. | CB-06.4 | | | | |

### F. Accessibility and localisation

| ID | Area | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-SUV-F01` | Arabic RTL | P0 | Run the whole sheet in Arabic. | The form mirrors; segmented tabs, radio pills and pickers all read RTL; the gold ring sits on the correct edge of the gender pills. | `E2E-MOB007-011`, D-698 | | | | |
| `TC-MOB-SUV-F02` | Lookup labels follow the locale | P0 | Switch language with lookups loaded. | Country, region, profile-type and organisation labels switch language with the app. | `E2E-MOB007-011` | | | | |
| `TC-MOB-SUV-F03` | Field direction per script | P0 | Inspect each field in Arabic. | The English name and English job title are **LTR**; the Arabic name and Arabic job title are **RTL**; the mobile number and plate digits read correctly. | `E2E-MOB007-025` | | | | |
| `TC-MOB-SUV-F04` | No hardcoded string | P0 | Compare every label, hint, error, tab, pill, picker title and toast in both languages. | All translated. This form has the largest string surface in the app — check the picker sheets and the empty states too. | CB-02.3 | | | | |
| `TC-MOB-SUV-F05` | Accessible names | P1 | Screen reader on; traverse the whole form. | Every field, tab, radio pill, picker, attach control and the Next action announces its own label. Required fields are announced as required. | CB-08.1 | | | | |
| `TC-MOB-SUV-F06` | Errors announced | P1 | Trigger B01 with the screen reader on. | The required errors and the toast are announced, and focus moves to the first offending field. | CB-08.3 | | | | |
| `TC-MOB-SUV-F07` | Gender pills are not colour-only | P1 | Inspect the selected pill. | Selection is conveyed by more than the gold ring alone. | CB-08.4 | | | | |
| `TC-MOB-SUV-F08` | Text scaling | P2 | Largest supported font size. | Labels wrap; pickers stay usable; nothing overlaps on the longest Arabic labels. | CB-08.5 | | | | |

### G. Data integrity

| ID | Area | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-SUV-G01` | Nothing is written here | P0 | Complete the form to **Next**, then check the profile without completing the interests step. | **No** profile write has occurred. The single save happens on the interests screen. | `E2E-MOB007-003` (D-332) | | | | |
| `TC-MOB-SUV-G02` | Draft survives to the next screen | P0 | Complete C01 and inspect the interests screen's carried state. | Every entered value, including the Arabic job title and the captured images, reaches the save step intact. | `E2E-MOB007-024` | | | | |
| `TC-MOB-SUV-G03` | Round-trip fidelity | P0 | Complete the full save, then reopen the form. | Every value redisplays exactly as entered — names, both job titles, region, mobile and plate in both language forms. | `E2E-MOB007-016`, `-022` | | | | |
| `TC-MOB-SUV-G04` | Images bind to the right record | P0 | Complete a save with both images, then view the profile in the Control Panel. | The ID document and the face photo are attached to the correct account and are not swapped. | A4 | | | | |

### H. Customer acceptance and Definition of Done

| ID | Criterion | Pri | Steps | Expected result | Refs | **Actual** | **Status** | **Evidence** | **Notes** |
|---|---|---|---|---|---|---|---|---|---|
| `TC-MOB-SUV-H01` | Registration data is captured | P0 | Run C01 for a Saudi man, a Saudi woman, a non-Saudi with an Iqama and a non-Saudi with a passport. | All four complete and produce a correct, complete profile. | **FR-2xx** (registration) | | | | |
| `TC-MOB-SUV-H02` | Self-elevation is impossible | P0 | Run C04, D03, D11, D12. | A registrant cannot pick a privileged tier, approve themselves, or write another user's profile. | **NFR-01**, A1, A4 | | | | |
| `TC-MOB-SUV-H03` | Identity documents are protected | P0 | Run D13, D14, D17 → D19. | ID documents and personal identifiers are validated, access-controlled, encrypted in transit and never logged. | **NFR-01**, A2, A6, A9-9 | | | | |
| `TC-MOB-SUV-H04` | Liveness cannot be bypassed | P0 | Run C10, C11, D15. | The face photo can only come from a live guided capture. | **NFR-01**, D-662 | | | | |
| `TC-MOB-SUV-H05` | Design parity | P1 | Compare the live render against Figma `168:2972`. | Strings, typography, colour, spacing and radii match. The DOB, place-of-birth and Saudi-switch fields are **kept even though the frame omits them** — they are API-required, so their presence is not a deviation. | DoD-Gate-4, D-368 | | | | |
| `TC-MOB-SUV-H06` | Live-render gate | P0 | Capture a full screenshot of every branch, the device log and the network list. | Screenshots captured; **zero** console errors; **zero** failed assets; no horizontal overflow. | DoD-Gate-4 | | | | |
| `TC-MOB-SUV-H07` | Both languages completed | P0 | Confirm two runs recorded. | Arabic (RTL) and English (LTR) both complete. | SIMF-TST-001 §11 | | | | |
| `TC-MOB-SUV-H08` | Device matrix | P0 | Run C01, C10 and C11 on a standard phone, the **Huawei / no-GMS** handset and a tablet. | The form and the live face capture work on every class. | SIMF-MAA-001 | | | | |
| `TC-MOB-SUV-H09` | Catalogue alignment | P1 | Cross-check against `E2E-MOB007-001…025`. | Every scenario is covered here and none contradicts the catalogue. | DoD-SES-7 | | | | |

## 5. Execution summary

| | Count |
|---|---|
| Total cases (95 authored + 9 applicable inherited blocks) | |
| PASS | |
| FAIL | |
| BLOCKED | |
| N-A | |
| NOT-RUN | |
| **Pass rate** (PASS / (PASS+FAIL)) | |

| Exit criterion | Met? | Note |
|---|---|---|
| Every **P0** case is PASS | | |
| No open **high-severity** defect | | |
| Both language runs completed | | |
| All four nationality / gender branches exercised | | |
| Device matrix completed (incl. live face capture on a real handset) | | |
| Evidence captured for every PASS and FAIL | | |
| **No real identity data used**; every test account and uploaded image removed | | |

## 6. Defect, fix and re-test ledger

| Case ID | Defect ID | Severity | Reported by / date-time | Expected vs actual | Root cause | **Developer** | **Fix ref (commit / PR)** | **Fixed date + time** | **Re-test result** | **Re-test date + time** | **Re-tested by** | **State** |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| | | Critical / High / Medium / Low | | | | | | | PASS / FAIL | | | New / Assigned / Fixed / Re-test / Closed / Reopened |

**After any fix, re-run this screen's whole P0 set.** This screen hands its draft
to the interests screen, which performs the only save — include
[sign-up-interests.md](sign-up-interests.md) in the regression pass, and the
identity-verification screen for anything touching the face capture.

## 7. Sign-off

| Role | Name | Date | Verdict |
|---|---|---|---|
| Tester | | | Accept / Reject |
| QA Lead | | | Accept / Reject |
| Developer | | | Fixes complete: yes / no |
| Owner | | | Accepted for release: yes / no |

## 8. Revision history

| Version | Date | Author | Change |
|---|---|---|---|
| 1.0 | 2026-08-03 | SIMF Team | First issue. Grounded in `e2e/mobile-sign-up-visitor.md` `E2E-MOB007-001…025`, `mobile_field.dart` and the D-332 / D-371 / D-437 / D-459 / D-469 / D-662 / D-694 / D-722 / D-723 decisions. |

---

_Authored:_ 2026-08-03 · _Last reviewed:_ 2026-08-03
