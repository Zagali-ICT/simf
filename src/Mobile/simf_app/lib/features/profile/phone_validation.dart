/// C4 (D-371) — the standard phone shapes, shared by the profile form and
/// its tests. Mirrors the server's `UpsertUserProfileRequestValidator`
/// exactly: Saudi mobile `05XXXXXXXX` or `+9665XXXXXXXX`; international
/// mobile E.164 (`+`, non-zero lead, 8–15 digits). Spaces and dashes are
/// stripped before the match, like the server.
library;

final RegExp _saudiMobileShape = RegExp(r'^(05\d{8}|\+9665\d{8})$');
final RegExp _e164Shape = RegExp(r'^\+[1-9]\d{7,14}$');

String _normalize(String value) =>
    value.trim().replaceAll(' ', '').replaceAll('-', '');

/// True when [value] is a standard Saudi mobile (separators ignored).
bool isStandardSaudiMobile(String value) =>
    _saudiMobileShape.hasMatch(_normalize(value));

/// True when [value] is a standard E.164 international mobile
/// (separators ignored).
bool isStandardInternationalMobile(String value) =>
    _e164Shape.hasMatch(_normalize(value));
