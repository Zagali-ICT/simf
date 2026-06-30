/// Required-field rule: a value is "blank" when it is null or trims to empty.
/// Pure predicate — the screen owns the l10n message.
library;

/// True when [value] is null or contains only whitespace.
bool isBlank(String? value) => value == null || value.trim().isEmpty;
