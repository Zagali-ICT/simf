import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/core/utils/saudi_time.dart';
import 'package:simf_app/features/live/data/current_live_session.dart';
import 'package:simf_app/features/sessions/data/session_models.dart';
import 'package:simf_app/features/sessions/data/sessions_repository.dart';

SessionListItem _session(
  String id, {
  required DateTime start,
  required DateTime end,
}) =>
    SessionListItem(
      id: id,
      code: id,
      title: id,
      titleArabic: id,
      hallId: 'h',
      hallName: 'H',
      hallNameArabic: 'ه',
      start: start,
      end: end,
      status: SessionStatus.scheduled,
      speakers: const <SessionSpeaker>[],
    );

ProviderContainer _containerWith(List<SessionListItem> sessions) {
  return ProviderContainer(
    overrides: <Override>[
      programmeSessionsProvider.overrideWith((ref) async => sessions),
    ],
  );
}

void main() {
  group('currentLiveSessionIdProvider (Home LIVE-banner deep-link)', () {
    test('returns the id of the session that is live right now', () async {
      final now = saudiNow();
      final container = _containerWith(<SessionListItem>[
        _session(
          'ended',
          start: now.subtract(const Duration(hours: 3)),
          end: now.subtract(const Duration(hours: 2)),
        ),
        _session(
          'live',
          start: now.subtract(const Duration(minutes: 10)),
          end: now.add(const Duration(minutes: 50)),
        ),
        _session(
          'upcoming',
          start: now.add(const Duration(hours: 1)),
          end: now.add(const Duration(hours: 2)),
        ),
      ]);
      addTearDown(container.dispose);

      expect(
        await container.read(currentLiveSessionIdProvider.future),
        'live',
      );
    });

    test('returns null when nothing is live (card falls back id-less)',
        () async {
      final now = saudiNow();
      final container = _containerWith(<SessionListItem>[
        _session(
          'ended',
          start: now.subtract(const Duration(hours: 3)),
          end: now.subtract(const Duration(hours: 2)),
        ),
        _session(
          'upcoming',
          start: now.add(const Duration(hours: 1)),
          end: now.add(const Duration(hours: 2)),
        ),
      ]);
      addTearDown(container.dispose);

      expect(
        await container.read(currentLiveSessionIdProvider.future),
        isNull,
      );
    });
  });
}
