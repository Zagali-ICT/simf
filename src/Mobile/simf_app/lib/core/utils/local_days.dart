/// Day-grouping for any list of timed items.
///
/// These lived in `features/sessions/data/session_models.dart`, where their own
/// doc already said they existed "so every day-grouped list (sessions,
/// presentations, …) shares one algorithm instead of re-declaring it". Two
/// features outside sessions re-declared it anyway - the speaker and delegation
/// meeting sheets each carried a private copy - because reaching them meant
/// importing another feature's models. Living in `core/utils` removes that
/// reason. `session_models.dart` re-exports them, so its existing call sites
/// are unchanged.
library;

/// The distinct device-local calendar days spanned by [items], ascending, each
/// a midnight-local [DateTime]. [localStart] pulls the device-local start out
/// of an item.
List<DateTime> distinctLocalDays<T>(
  Iterable<T> items,
  DateTime Function(T) localStart,
) {
  final byKey = <String, DateTime>{};
  for (final item in items) {
    final local = localStart(item);
    final key = '${local.year}-${local.month}-${local.day}';
    byKey.putIfAbsent(key, () => DateTime(local.year, local.month, local.day));
  }
  return byKey.values.toList()..sort();
}

/// Whether [a] and [b] fall on the same device-local calendar day
/// (time-of-day- and argument-order-independent) — the one home for the
/// day-equality predicate.
bool sameLocalDay(DateTime a, DateTime b) =>
    a.year == b.year && a.month == b.month && a.day == b.day;
