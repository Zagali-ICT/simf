# E2E test catalogue — `Sign up — type` — **REMOVED (D-332)**

> **This screen was removed.** It was an *invented* account-type gate not present in
> `Mockup.html`; the API has no "registration type" field. See
> [`Page_004`](../../App/Page_004/README.md) and **D-332**.
>
> **Corrected sign-up flow:** Sign in ([mobile-sign-in](mobile-sign-in.md)) → **Register**
> ([mobile-sign-up-form](mobile-sign-up-form.md), email + pwd + confirm) → **OTP**
> ([mobile-email-otp](mobile-email-otp.md)) → **Profile data**
> ([mobile-sign-up-visitor](mobile-sign-up-visitor.md), incl. the Visitor/Other filter +
> ProfileType) → **Interests** ([mobile-sign-up-interests](mobile-sign-up-interests.md),
> 1–10 + the single save) → **Confirmation**
> ([mobile-registration-success](mobile-registration-success.md)).

The former scenarios `E2E-MOB004-001..007` are **retired** — there is no type screen.
The Visitor/Other category is chosen inside the profile-data screen
([mobile-sign-up-visitor](mobile-sign-up-visitor.md), `E2E-MOB007-001`).

---

_Last reviewed:_ `2026-06-07` by `SIMF Team` — removed under D-332.
