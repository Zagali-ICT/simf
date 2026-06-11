# Page 004 — REMOVED · إنشاء حساب — النوع · Sign up — type

> **This screen was REMOVED (D-332, 2026-06-07).** It was an *invented* account-type
> gate that does **not** exist in `Mockup.html`. The owner directed the app to follow
> the mockup, where sign-up goes straight from **Sign in (Page 003)** to **Register
> (Page 005 — email + password + confirm)** with no type chooser. The account
> category (**Visitor / Other**, the `ProfileType.IsForVisitor` split) is chosen
> **inside the profile form** ([Page 007](../Page_007/README.md)), not on a separate
> screen.

## Why it was removed
- **Mockup:** there is no type screen. The flow is
  Login `03` → Register `04` (email + pwd + confirm) → OTP `4-01` →
  Data `05` (نوع التسجيل Visitor/Other + التصنيف ProfileType + fields) →
  Interests `5-01` → Success `10`.
- **API:** there is **no "registration type" field** — the only stored value is
  `ProfileTypeId` (the `VisitorType` discriminator was dropped in P8;
  `UpsertUserProfileRequestValidator` confirms). So nothing on a "type" screen
  could ever be sent to the server.
- This folder previously documented (**D-268**) a restructure that *added* a
  standalone type gate, citing "controlled docs override the mockup." The **owner
  overruled** that and chose mockup fidelity (D-332).

## Corrected flow
See **[D-332](../../decisions/DECISIONS_LOG.md)**, the reworked
**[Page 007](../Page_007/README.md)** (profile data incl. the Visitor/Other
filter + ProfileType select), and the new **[Page 007-01](../Page_007-01/README.md)**
(interests, 1–10).

The `Page_004_*` sub-docs are kept only as a removed-screen marker.
