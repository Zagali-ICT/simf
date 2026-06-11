# Page 004 — REMOVED (see [README](README.md) · D-332)

This screen — the invented **"Sign up — type"** gate — was **removed**: it is not in
`Mockup.html`, and (as the old version of this file already stated) it had **no SIMF
API**. The API has no "registration type" field at all — the only stored value is
`ProfileTypeId`. Sign-up goes **Page 003 → Page 005** (register: email + password +
confirm) directly; the **Visitor / Other** category (the `ProfileType.IsForVisitor`
filter) and the **ProfileType** are chosen inside the profile form on
**[Page 007](../Page_007/README.md)**; interests are on
**[Page 007-01](../Page_007-01/README.md)**.
