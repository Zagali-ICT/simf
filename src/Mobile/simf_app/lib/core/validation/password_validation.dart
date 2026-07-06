/// Password policy rule — the client-side mirror of the *structural* server
/// rules (SIMF-API-001 §12.5, hardened to the NCA standard): 8–128 characters
/// with an upper-case letter, a lower-case letter, a digit and a special
/// character. The server additionally rejects sequential runs (`123`/`abc`),
/// three-in-a-row repeats, common passwords and ones resembling the email —
/// those are enforced server-side and surfaced to the user via the field-error
/// details, so this is instant feedback for the common cases, not the full
/// policy. Keep in step with `PasswordPolicy` / `PasswordRules` on the backend.
library;

final RegExp _lower = RegExp('[a-z]');
final RegExp _upper = RegExp('[A-Z]');
final RegExp _digit = RegExp(r'\d');
final RegExp _special = RegExp(r'[^A-Za-z0-9]');

/// True when [value] satisfies the structural policy: 8–128 characters
/// containing a lower-case letter, an upper-case letter, a digit and a special
/// character.
bool isValidPassword(String value) =>
    value.length >= 8 &&
    value.length <= 128 &&
    _lower.hasMatch(value) &&
    _upper.hasMatch(value) &&
    _digit.hasMatch(value) &&
    _special.hasMatch(value);
