/// Grouping a list into runs, without reordering it.
library;

/// Groups [items] into consecutive runs that share a key, preserving the
/// order the list arrived in.
///
/// This is NOT `groupBy`: two runs with the same key stay separate. That is the
/// point for a feed the server already ordered — the notifications list is
/// newest-first, so "Today" appears once at the top rather than collecting
/// every Today item from the whole history into one bucket.
///
/// An empty input gives an empty result; a null-ish key is the caller's problem
/// to encode (the notifications list uses `''` for an undated row).
List<MapEntry<K, List<T>>> groupConsecutive<T, K>(
  Iterable<T> items,
  K Function(T item) keyOf,
) {
  final groups = <MapEntry<K, List<T>>>[];
  for (final item in items) {
    final key = keyOf(item);
    if (groups.isNotEmpty && groups.last.key == key) {
      groups.last.value.add(item);
    } else {
      groups.add(MapEntry<K, List<T>>(key, <T>[item]));
    }
  }
  return groups;
}
