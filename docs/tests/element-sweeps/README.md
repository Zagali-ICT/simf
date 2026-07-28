# Element-sweep run artifacts

Per-run output of `tools/qa/element-sweep.js`, one JSON per page per run
(`{surface}-{slug}.{run}.json`), plus `predicted-inventory.json` — the expected
control set derived from source by `tools/qa/predicted_inventory.py`.

These live here rather than in the 183 per-page catalogue files because a full
inventory is hundreds of lines per page and would bury the scenarios. The
catalogue carries the *contract* (`E2E-{NS}-ELS-001/002`); this directory carries
the *evidence* a given run produced.

`predicted-inventory.json` is committed and regenerated on demand: it is derived
entirely from the `.razor` sources, so a diff against it after a UI change shows
exactly which controls appeared or disappeared.

Run artifacts are not committed — regenerate them per run.
