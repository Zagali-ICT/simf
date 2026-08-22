# Page 024-01 — تفاصيل النسخة · Past-edition detail — **MOVED**

> **This page's reference doc now lives at
> [`docs/pages/mobile/archive-detail/README.md`](../../pages/mobile/archive-detail/README.md).**
> Read that one. It is grounded in the current screen; this location is kept only
> so existing links (the E2E catalogue file, the sibling `Page_024` doc) still land
> somewhere.

## Why this folder is not the doc any more

The four spec files beside this one were written in 2026-06, when the D-273
endpoint had just landed and the Flutter work had not. Four of their central
claims are now false, and the new file says so explicitly rather than quietly
replacing them:

1. **There is no `RouteNames.archiveDetail` and no `/archive/:editionId`.** The
   "planned" route was never added and is not needed — the detail is a **state of
   `/archive`**, selected by an edition pill.
2. **The gallery / session-titles / past-speakers lists are not deferred.** They
   were modelled and built in **D-432**; there are no "coming soon" placeholders.
3. **There is no cover banner.** `ArchiveBody` renders none, and
   `coverImageRelativePath` is decoded by the list model and read by nothing.
4. **There is no "not found" state.** `archiveEditionDetailProvider` folds every
   `ApiFailure` to `null`, so a 404 renders a thinner edition instead.

Two of their statements are still correct and are carried forward: the single-404
visibility surface (a hidden archive and an unknown edition are indistinguishable
to the client) and the "never render an empty labelled box" rule.

## Historical spec (superseded — do not build from these)

| Aspect | Document |
|--------|----------|
| Function | [Page_024-01_Function.md](Page_024-01_Function.md) |
| Logic | [Page_024-01_Logic.md](Page_024-01_Logic.md) |
| API | [Page_024-01_API.md](Page_024-01_API.md) |
| Design | [Page_024-01_Design.md](Page_024-01_Design.md) |

They are retained as the record of the D-273 increment, in the same way
`docs/App/Page_026/` is retained beside the send-question doc. The authoritative
wire contract, states, actions and findings are in the new file.

- Screen: `src/Mobile/simf_app/lib/features/archive/archive_screen.dart` +
  `widgets/archive_body.dart`
- Parent page doc: [`docs/pages/mobile/archive/README.md`](../../pages/mobile/archive/README.md)
- E2E: [`docs/tests/e2e/mobile-archive-detail.md`](../../tests/e2e/mobile-archive-detail.md)
