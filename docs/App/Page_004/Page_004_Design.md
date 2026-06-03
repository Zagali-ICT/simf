# Page 004 — Design (إنشاء حساب — النوع · Sign up — type)

Flutter screen design for the account-type chooser. Behaviour is in
[Page_004_Function.md](Page_004_Function.md); there is **no API**
([Page_004_API.md](Page_004_API.md)).

## Layout
```
┌─────────────────────────────────────────────┐
│  ←        إنشاء حساب — النوع / Sign up — type │  app bar (back leading)
├─────────────────────────────────────────────┤
│                                             │
│   اختر نوع الحساب / Choose your account type │  lead line
│                                             │
│  ┌───────────────────────────────────────┐  │
│  │ (•) زائر / Visitor                     │  │  ← enabled, selectable
│  │     حساب لحضور الفعالية والتفاعل معها    │  │     (helper text)
│  └───────────────────────────────────────┘  │
│  ┌───────────────────────────────────────┐  │
│  │ ( ) عارض / Exhibitor        [disabled] │  │  ← info only
│  └───────────────────────────────────────┘  │
│  ┌───────────────────────────────────────┐  │
│  │ ( ) راعٍ / Sponsor          [disabled] │  │  ← info only
│  └───────────────────────────────────────┘  │
│   ⓘ تُدار حسابات العارضين والرعاة من لوحة     │  CP-only note
│      التحكم / managed from the Control Panel │
│                                             │
│   [           متابعة / Continue           ]  │  primary, disabled until selection
│         لديك حساب؟ تسجيل الدخول / Sign in     │  secondary link
└─────────────────────────────────────────────┘
```

## Components
| Element | Widget | Notes |
|---------|--------|-------|
| App bar | `AppBar` with back `leading` | Title from resources; back pops the route. |
| Lead line | `Text` (titleMedium) | "Choose your account type". |
| Option rows | selectable cards / `RadioListTile`-style tiles | One mutually-exclusive group; only Visitor enabled. |
| Visitor helper | secondary `Text` under the Visitor tile | Explains the visitor account. |
| Disabled tiles | same tile, `enabled: false` + muted style | Exhibitor / Sponsor; tap surfaces the CP-only note. |
| CP-only note | inline `Row` with info icon + `Text` (or a `SnackBar` on tap) | The D-199 explanation. |
| Continue | primary `FilledButton` | `onPressed: null` until Visitor is selected. |
| Sign-in link | `TextButton` | Routes to Page 003. |

## States
| State | Appearance |
|-------|------------|
| **Initial** | Visitor enabled + unselected; Exhibitor/Sponsor disabled; **Continue disabled**; no spinner (no data load). |
| **Selected** | Visitor tile shows the selected indicator; **Continue enabled**. |
| **Disabled-tap** | Tapping Exhibitor/Sponsor shows the CP-only note; no selection change. |
| **Validation** | Forced Continue with no selection → inline hint "اختر نوع الحساب / Choose an account type". |
| **Loading** | **N/A** — the screen loads no data and makes no call. |
| **Empty** | **N/A** — the option list is a fixed in-code constant, never empty. |
| **Error** | **N/A** — no request, so no server/error state on this screen. |
| **Success / exit** | On Continue → forward navigation to Page 005 (type = Visitor). |

## RTL
- In Arabic the screen is **fully mirrored**: app-bar back affordance and tile
  chevrons flip side, text aligns right, the option group reads right-to-left.
- Layout uses direction-aware widgets (no `EdgeInsets.only(left:)` hard-coding);
  spacing comes from logical start/end.

## Localization
- Every visible string (title, lead line, option labels, helper text, CP-only note,
  buttons, link) is sourced from app **resources** in **AR + EN**. No hard-coded text.

## Accessibility
- Option tiles expose selected/disabled semantics; disabled tiles are announced as
  disabled with the CP-only reason available.
- Touch targets meet the platform minimum; Continue's disabled state is conveyed by
  more than colour alone.
