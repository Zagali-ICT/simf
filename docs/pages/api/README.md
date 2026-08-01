# API-only reference docs

`docs/pages/{cp,web,mobile}/` documents a **page**. Some deliverables have no page of
their own — a public API parameter, a background worker, an internal seam — but still
have to satisfy the D-246 definition of done (docs + E2E catalogue + tests in the same
changeset). Those live here, one file per surface, and are indexed from
[`../PAGE-INDEX.md`](../PAGE-INDEX.md) exactly like a page.

The per-page template's UI sections (screenshots, layout, RTL render) do not apply, so
these files use a shorter shape: purpose, contract, authorisation, behaviour, and the
tests that hold it.

| File | Covers |
|---|---|
| [`programme-sessions.md`](programme-sessions.md) | The public programme list + its `?day=` / `?categoryId=` filters |
| [`badge-activation.md`](badge-activation.md) | Badge resolve / activate / self-claim, including the profile capture |
| [`movement-tracking.md`](movement-tracking.md) | FR-1103 device-position capture + the dwell / route reports |
| [`workers.md`](workers.md) | The hosted background workers and what each one guarantees |
| [`notifications.md`](notifications.md) | The notification dispatcher and its delivery channels |
| [`account-preferences.md`](account-preferences.md) | The signed-in account's five accessibility preferences (`accessibility-server-sync`) |
