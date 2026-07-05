# Contact us — تواصل معنا (Page 203, `#203`)

- **Route:** `/contact-us` (`RouteNames.contactUs`). Access: **Guest+ (public)** — the form submit is anonymous.
- **Figma:** **1388:7711**. **Clean-code freeze:** D-627 (2026-07-04). Built from a ComingSoon stub in D-464.

## Purpose

A public contact surface: an "أرسل رسالة" form (name / email / message → the
public `POST /app/contact-inquiry`), a "معلومات التواصل" panel (phone / email /
location) and a "وسائل التواصل الاجتماعي" row — the info panel + social links read
the app-lifetime `orgProfileProvider` (the same data the About screen uses), so
only the fields the admin actually set are shown.

## Structure (post-decomposition)

| File | Holds |
|------|-------|
| `contact_us_screen.dart` (121) | `ContactUsScreen` (`ConsumerStatefulWidget`) — the form controllers, `_send` (submit → clear → toast, `ApiFailure` → error toast), `_hasAnySocial`, and the `ListView` composing the three cards. |
| `widgets/contact_card_chrome.dart` | `ContactCard` (the shared navy-deep card chrome) + `ContactCardHeading` (white 16 Medium) — used by all three cards. |
| `widgets/contact_send_message_card.dart` (`ContactSendMessageCard` + `_Field`) | The "أرسل رسالة" form card — name / email / message fields (+ validators) over the gold send button. |
| `widgets/contact_info_card.dart` (`ContactInfoCard` + `_InfoRow`) | The "معلومات التواصل" panel — phone / email / location rows (each a gold icon box + beige divider), built from the org profile. |
| `widgets/contact_social_card.dart` (`ContactSocialCard` + `_SocialButton`) | The "وسائل التواصل الاجتماعي" row — one bordered box per set social link (forced-LTR order), each → `confirmThenLaunchExternal`. |

The shared `ContactCard`/`ContactCardHeading` extracted once (used by three
cards); each card's single-use leaf (`_Field`/`_InfoRow`/`_SocialButton`)
colocated with its parent. No raw `Color(0x..)` and no inline hex — the screen was
already fully tokenised, so this freeze is a **pure decomposition** (no token
change). Every file ≤400 lines (largest 164).

## L4 Figma parity (frame 1388:7711)

Captured `contact_us_1388-7711.png` (@375×1200, ar, full org profile) as the
**baseline before** the refactor, then **held it WITHOUT `--update`** after —
proving the 4-file decomposition is byte-identical. Golden read: header تواصل
معنا, the أرسل رسالة form (name/email/message + gold إرسال), the معلومات التواصل
panel (phone/email/location with gold icon boxes + dividers), the five-box social
row, bottom nav, RTL — all correct, no tofu.

## Level-F

Wired: form validation (name / valid-email / message required) → `POST
/app/contact-inquiry` (via `contactUsRepositoryProvider.submit`) → clear + toast
(`ApiFailure` → error toast); the info panel + social boxes hydrate from
`orgProfileProvider`; each social box → confirm-then-launch external. Reads
`GET /app/organization-profile`.

**Backend note (out of this Flutter-only freeze — owned by the backend session):**
per the PAGE-INDEX row, the `ContactInquiries` table (additive migration) + the CP
inbox `/admin/contact-inquiries` (perms `ContactInquiries.View`/`.Manage`) were
noted **pending**; unchanged here.

## Tests

`test/golden/contact_us_golden_test.dart` (frame 1388:7711, @375×1200, ar) +
`test/features/contact_us/contact_us_screen_test.dart` (3 — renders form + info
panel, empty-submit validation blocks the API, valid-submit posts + toast). E2E:
`docs/tests/e2e/mobile-contact-us.md`.

## Related decisions

- **D-627** (this clean-code freeze — 4-file decomposition + first golden).
- **D-464** (built from ComingSoon → Figma 1388:7711), **D-495** (org-profile provider the info panel reads).
