# Page 040 — تقييم · Rate

Per-page documentation folder (App screen 40).

## Identity
| | |
|---|---|
| Route | `RouteNames.rate` → `/rate` (**Visitor login-only — auth-gated**) |
| Titles | AR **تقييم** · EN **Rate** |
| Section | 8 — Settings & legal |
| Nature | **Overall + per-element star rating + comment** feedback form |
| Status | API **BUILT** (`POST /app/feedback/rate`); **Flutter screen BUILT (D-310); per-element scores + Figma `1116:16894` re-skin (D-463)** |

## API
`POST /api/v1/app/feedback/rate` (`RequireApprovedAccount`) — body
`{ stars: 1..5, comment?: <=2000, organizationStars?, contentStars?, appStars?,
venueStars? }` (each element score 1..5 **when present**, else omitted/null) →
`RatingView` (upsert; echoes all fields). The four element fields are **appended,
defaulted-null** so the original overall-only callers stay valid (D-463). The
route is auth-gated; a guest is redirected to sign-in. The CP `/admin/ratings`
grid + Excel export surface the four element columns.

## Behaviour
On the navy `KsaPage` shell (Figma `1116:16894`): the kicker (شارك تجربتك) + the
question, an **overall** 1–5 star bar that **fills from the inline start** (right
under RTL) with a dynamic "{n} من 5 · {word}" summary, the **قيّم العناصر** block
of four per-element star rows (التنظيم / المحتوى / التطبيق / المكان والمرافق), the
ملاحظاتك notes box and the gold إرسال التقييم button. Overall stars are required
(a 0-star submit prompts "pick a star rating"); element scores are optional
(unscored → null). Success → thank-you toast; wire failure → error toast.
Approved-only.

## Tests
Widget `src/Mobile/simf_app/test/features/feedback/rate_screen_test.dart` (4:
no-stars prompt, overall pick+summary+submit→sent, per-element score sent,
failure→toast). API `tests/SIMF.Api.Tests/FeedbackRatingsTests.cs` (9, incl. the
3 element-score cases). E2E: [`mobile-rate.md`](../../tests/e2e/mobile-rate.md).
