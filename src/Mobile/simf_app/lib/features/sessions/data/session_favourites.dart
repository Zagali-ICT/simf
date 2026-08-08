import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/features/sessions/data/sessions_endpoints.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// Session favourites (المفضلة) — the per-user heart shown on the
/// session-summaries (Figma 1388:8392) and my-sessions (1388:9067) screens.
///
/// One app-wide [Set] of favourited session ids, loaded once from
/// `GET /app/sessions/favourites`. [toggle] flips a single id **optimistically**
/// and then POSTs (add) / DELETEs (remove) the change, reverting the set if the
/// call fails so the heart never lies. Both screens watch this provider, so a
/// toggle on one reflects on the other immediately. Approved-account only — a
/// failed initial load surfaces as an error the heart UI treats as "no
/// favourites" (the heart still toggles once loaded).
class SessionFavouritesController extends AsyncNotifier<Set<String>> {
  @override
  Future<Set<String>> build() async {
    final client = ref.watch(simfApiClientProvider);
    final ids = await client.get<List<String>>(
      SessionsEndpoints.favourites,
      decodeData: _decodeIds,
    );
    return ids.toSet();
  }

  /// True when [sessionId] is currently favourited (false while loading/error).
  bool isFavourite(String sessionId) =>
      state.valueOrNull?.contains(sessionId) ?? false;

  /// Flips the heart for [sessionId]: updates the set immediately, then POSTs
  /// (add) or DELETEs (remove). Reverts the optimistic change and rethrows if
  /// the call fails, so the heart stays in sync with the server.
  Future<void> toggle(String sessionId) async {
    final current = state.valueOrNull;
    if (current == null) {
      return; // not loaded yet — ignore the tap
    }

    final wasFavourite = current.contains(sessionId);
    final next = <String>{...current};
    if (wasFavourite) {
      next.remove(sessionId);
    } else {
      next.add(sessionId);
    }
    state = AsyncData<Set<String>>(next);

    final client = ref.read(simfApiClientProvider);
    try {
      if (wasFavourite) {
        await client.delete<bool>(
          SessionsEndpoints.favourite(sessionId),
          decodeData: (_) => true,
        );
      } else {
        await client.post<bool>(
          SessionsEndpoints.favourite(sessionId),
          decodeData: (_) => true,
        );
      }
    } on ApiFailure {
      state = AsyncData<Set<String>>(current); // revert — the server wins
      rethrow;
    }
  }

  static List<String> _decodeIds(Object? data) =>
      (data as List? ?? const <dynamic>[])
          .whereType<String>()
          .toList(growable: false);
}

final sessionFavouritesProvider =
    AsyncNotifierProvider<SessionFavouritesController, Set<String>>(
  SessionFavouritesController.new,
);
