# Page 004 — Function (إنشاء حساب — النوع · Sign up — type)

What this screen does, the elements on it, and exactly what the user can do.
Business rules are in [Page_004_Logic.md](Page_004_Logic.md); there is **no API**
(see [Page_004_API.md](Page_004_API.md)).

## In one line
A guest picks the kind of account to create. Only the **Visitor** path proceeds;
everything else is informational. No network call happens on this screen.

## How the user reaches it
- From **Page 003 (Sign in)** → tap **"Create account / إنشاء حساب"**.
- From the **welcome / boot** flow's sign-up entry point.
- Privilege at entry: **Guest** (no token yet).

## Elements
| # | Element | AR | EN |
|---|---------|----|----|
| 1 | Screen title | إنشاء حساب — النوع | Sign up — type |
| 2 | Lead line | اختر نوع الحساب | Choose your account type |
| 3 | **Visitor** option (primary, selectable) | زائر | Visitor |
| 4 | Visitor helper text | حساب لحضور الفعالية والتفاعل معها | Account to attend and interact with the event |
| 5 | Exhibitor option (disabled / info) | عارض | Exhibitor |
| 6 | Sponsor option (disabled / info) | راعٍ | Sponsor |
| 7 | CP-only note (under 5 & 6) | تُدار حسابات العارضين والرعاة من لوحة التحكم | Exhibitor & sponsor accounts are managed from the Control Panel |
| 8 | **Continue** button (primary) | متابعة | Continue |
| 9 | Back / cancel (app bar) | رجوع | Back |
| 10 | Already have an account? link | لديك حساب؟ تسجيل الدخول | Have an account? Sign in |

## User actions
| Action | Result |
|--------|--------|
| Tap **Visitor** | Selects the only enabled option; **Continue** becomes enabled. |
| Tap **Exhibitor** / **Sponsor** | No selection; shows the CP-only note (a `SnackBar` / inline hint). Not selectable. |
| Tap **Continue** (Visitor selected) | Navigates to **Page 005** `/sign-up/form` carrying `type = Visitor`. No API call. |
| Tap **Continue** (nothing selected) | Inline validation hint "اختر نوع الحساب / Choose an account type"; stays on page. |
| Tap **Back** | Returns to the previous screen (Page 003 / welcome). Nothing persisted. |
| Tap **Sign in** link | Navigates to **Page 003** `/sign-in`. |

## Navigation map
```
Page 003 (Sign in) ──"Create account"──▶ Page 004 (Sign up — type)
Page 004 ──Visitor + Continue──▶ Page 005 (/sign-up/form, type=Visitor)
Page 004 ──Back──▶ Page 003 / welcome
Page 004 ──"Sign in" link──▶ Page 003 (/sign-in)
```

## Acceptance criteria
- AC1 — On entry, **Visitor** is the only enabled, selectable option; Exhibitor and Sponsor are visibly **disabled** with the CP-only note.
- AC2 — **Continue** is disabled until **Visitor** is selected; tapping it routes to Page 005 with `type = Visitor`.
- AC3 — Tapping Exhibitor/Sponsor never selects them and never enables Continue; it surfaces the CP-only explanation.
- AC4 — **No backend request** is issued by this screen (verified offline: the screen behaves identically with no network).
- AC5 — Full **RTL** in Arabic; the app bar back affordance flips side; both label languages render from resources, never hard-coded strings.
- AC6 — A "Sign in" affordance lets a returning user leave the sign-up flow.
