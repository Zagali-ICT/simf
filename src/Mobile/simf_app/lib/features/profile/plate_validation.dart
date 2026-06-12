/// C6 (D-371) — the Saudi vehicle-plate standard, shared by the profile
/// form and its tests. Mirrors the server's
/// `UpsertUserProfileRequestValidator.IsStandardPlateNumber` exactly:
/// exactly 3 letters (Arabic or Latin) + 1–4 digits, either order, ≤ 7
/// chars once spaces and dashes are stripped.
library;

final RegExp _plateShape =
    RegExp(r'^([A-Za-zء-ي]{3}\d{1,4}|\d{1,4}[A-Za-zء-ي]{3})$');

String _normalize(String value) =>
    value.trim().replaceAll(' ', '').replaceAll('-', '');

/// True when [value] is a standard Saudi plate (separators ignored).
bool isStandardPlateNumber(String value) =>
    _plateShape.hasMatch(_normalize(value));
