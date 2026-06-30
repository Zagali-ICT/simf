/// Email shape rule, shared by the sign-up form and sign-in screens. The same
/// lenient shape both screens already used (`local@domain.tld`, no whitespace),
/// kept in one place so the two stay in lockstep. The server re-validates.
library;

final RegExp _emailShape = RegExp(r'^[^@\s]+@[^@\s]+\.[^@\s]+$');

/// True when [value] matches the standard `local@domain.tld` shape (no
/// whitespace). The caller trims before calling, as the screens do today.
bool isValidEmail(String value) => _emailShape.hasMatch(value);
