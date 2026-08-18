/// Required-field rule: a value is "blank" when it is null or trims to empty.
/// Pure predicate — the screen owns the l10n message.
library;

bool isBlank(String? value) => value == null || value.trim().isEmpty;
