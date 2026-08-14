// D-820 — the on-device verdict a scanner reaches with no network.
//
// The rule under test is which decisions a device is ALLOWED to make. It may
// refuse a forged badge and a disallowed profile type, because both are
// checkable from the badge itself plus cached rules. It must ABSTAIN on
// anything that needs live data — a hall booking, an account that was disabled
// this morning — so the server decides when the queued scan uploads.
import 'dart:async';
import 'dart:convert';
import 'dart:typed_data';

import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/features/gates/data/gate_offline_config.dart';
import 'package:simf_app/features/gates/data/gate_scan_queue.dart';
import 'package:simf_app/features/gates/data/gates_repository.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../accessibility/_fake_prefs.dart';

/// A badge for profile-type code 1, edition 2026, under the fixture key,
/// produced by the C# encoder. See offline_badge_test.dart.
const String visitorBadge =
    // A C# encoder token; wrapping breaks the character-for-character compare.
    // ignore: lines_longer_than_80_chars
    '15F9NADHE9H94MTGBNK7WMMWB6Q3GTDED9NMBF161ZHXRFWS8KFRCYG8SWRCQ0NP1EJQ0SQXAS0NRA';

/// Profile-type code 7 — a type the perimeter gate below does not admit.
const String partnerBadge =
    // A C# encoder token; wrapping breaks the character-for-character compare.
    // ignore: lines_longer_than_80_chars
    '1CVTC4V9WG32MWFARN461D6MK7EZ7A0B7SW1S7QZW4EJCXYSYSFBCZ3M69YD16B3TP591QT4GPXPRG';

void main() {
  final key = base64Encode(
    Uint8List.fromList(List<int>.generate(32, (i) => i)),
  );

  GateOfflineConfig config({
    bool armed = true,
    bool sessionWalkIn = false,
    List<int> allowed = const <int>[1, 2],
    bool gateActive = true,
  }) {
    return GateOfflineConfig(
      badgeKey: armed ? key : null,
      badgeKeyVersion: 1,
      previousBadgeKey: null,
      previousBadgeKeyVersion: 0,
      sessionWalkIn: sessionWalkIn,
      issuedAtIso: '2026-11-23T06:00:00.000Z',
      gates: <GateOfflineRule>[
        GateOfflineRule(
          gateId: 'perimeter',
          code: 'G-MAIN',
          allowedProfileTypeCodes: allowed,
          isHallDoor: false,
          isActive: gateActive,
        ),
        GateOfflineRule(
          gateId: 'hall',
          code: 'G-H1',
          allowedProfileTypeCodes: allowed,
          isHallDoor: true,
          isActive: true,
        ),
      ],
    );
  }

  GatesRepository build(GateOfflineConfig? cached) {
    final prefs = FakePrefs();
    final cache = GateOfflineConfigCache(prefs);
    if (cached != null) {
      unawaited(cache.write(cached));
    }
    return GatesRepository(_UnusedClient(), GateScanQueue(prefs), cache);
  }

  group('judgeOffline', () {
    test('admits a badge whose type the gate allows', () {
      final verdict = build(config())
          .judgeOffline(gateId: 'perimeter', qr: visitorBadge);

      expect(verdict, isNotNull);
      expect(verdict!.isAllowed, isTrue);
      // The operator can read this out to the Control Panel: a badge names the
      // ATTENDEE now, which is the id the CP looks people up by.
      expect(verdict.badge?.profileId, '11111111-2222-3333-4444-555555555555');
      expect(verdict.badge?.editionYear, 2026);
    });

    test('refuses a type this gate does not admit', () {
      // The rule the walk-in mode NEVER relaxes, online or offline.
      final verdict =
          build(config()).judgeOffline(gateId: 'perimeter', qr: partnerBadge);

      expect(verdict?.isAllowed, isFalse);
      expect(verdict?.reason, OfflineDenialReason.profileTypeNotAllowed);
      // The holder is still identified, so the operator knows who was refused.
      expect(verdict?.badge?.profileId, 'aabbccdd-eeff-0011-2233-445566778899');
    });

    test('admits every type when the gate has no allow-list', () {
      // An empty list is "no restriction" on the server too — not "nobody".
      final verdict = build(config(allowed: const <int>[]))
          .judgeOffline(gateId: 'perimeter', qr: partnerBadge);

      expect(verdict?.isAllowed, isTrue);
    });

    test('refuses a badge that does not authenticate under a loaded key', () {
      const forged = '1ZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZ';
      final verdict =
          build(config()).judgeOffline(gateId: 'perimeter', qr: forged);

      expect(verdict?.isAllowed, isFalse);
      expect(verdict?.reason, OfflineDenialReason.badgeInvalid);
    });

    test('refuses at a gate the last synced rules had switched off', () {
      final verdict = build(config(gateActive: false))
          .judgeOffline(gateId: 'perimeter', qr: visitorBadge);

      expect(verdict?.isAllowed, isFalse);
      expect(verdict?.reason, OfflineDenialReason.gateInactive);
    });

    group('abstains rather than denying', () {
      test('when this device has never been online', () {
        expect(
          build(null).judgeOffline(gateId: 'perimeter', qr: visitorBadge),
          isNull,
        );
      });

      test('when the gate is not in the cached rules', () {
        expect(
          build(config()).judgeOffline(gateId: 'unknown', qr: visitorBadge),
          isNull,
        );
      });

      test('when offline badges are not armed, so no key was issued', () {
        expect(
          build(config(armed: false))
              .judgeOffline(gateId: 'perimeter', qr: visitorBadge),
          isNull,
        );
      });

      test('at a hall door, because a booking needs live data', () {
        // Denying here would turn every offline hall door into a wall.
        expect(
          build(config()).judgeOffline(gateId: 'hall', qr: visitorBadge),
          isNull,
        );
      });

      test('on a code that is not a badge at all', () {
        expect(
          build(config()).judgeOffline(gateId: 'perimeter', qr: 'K7Q2M9ABCDEF'),
          isNull,
        );
      });

      test('on an ordinary minted serial, even when its first character '
          'collides with the loaded key version', () {
        // D-821 review regression. 'K' is Crockford index 19. Run the device at
        // key version 19 and the old code found a key, failed the GCM tag, and
        // hard-denied a LEGITIMATE badge as forged. Every version from 2 to 31
        // collides with some minter character, so the first key rotation would
        // have refused roughly one in thirty attendees at every offline gate.
        final atVersion19 = GateOfflineConfig(
          badgeKey: key,
          badgeKeyVersion: 19,
          previousBadgeKey: null,
          previousBadgeKeyVersion: 0,
          sessionWalkIn: false,
          issuedAtIso: '',
          gates: config().gates,
        );
        expect(
          build(atVersion19)
              .judgeOffline(gateId: 'perimeter', qr: 'K7Q2M9ABCDEF'),
          isNull,
        );
      });
    });

    test('admits at a hall door once session walk-in is armed', () {
      final verdict = build(config(sessionWalkIn: true))
          .judgeOffline(gateId: 'hall', qr: visitorBadge);

      expect(verdict?.isAllowed, isTrue);
    });
  });

  group('GateOfflineConfigCache', () {
    test('round-trips through prefs', () async {
      final prefs = FakePrefs();
      final cache = GateOfflineConfigCache(prefs);
      // Awaited on purpose. FakePrefs.setString is `async` with no `await`, so
      // its body runs synchronously and a dropped future happened to work; the
      // real prefs storage crosses a platform channel and would not.
      await cache.write(config());

      final read = cache.read();
      expect(read, isNotNull);
      expect(read!.canVerifyOffline, isTrue);
      expect(read.gates, hasLength(2));
      expect(read.ruleFor('hall')?.isHallDoor, isTrue);
      expect(read.ruleFor('perimeter')?.admits(1), isTrue);
      expect(read.ruleFor('perimeter')?.admits(7), isFalse);
    });

    test('reads nothing on a device that has never synced', () {
      expect(GateOfflineConfigCache(FakePrefs()).read(), isNull);
    });

    test('treats a key that is not AES-256 as no key at all', () {
      // Fail closed: a mis-provisioned key disables offline verification rather
      // than being padded into something that "works".
      final broken = GateOfflineConfig(
        badgeKey: base64Encode(Uint8List(16)),
        badgeKeyVersion: 1,
        previousBadgeKey: null,
        previousBadgeKeyVersion: 0,
        sessionWalkIn: false,
        issuedAtIso: '',
        gates: const <GateOfflineRule>[],
      );
      expect(broken.canVerifyOffline, isFalse);
      expect(broken.keyFor(1), isNull);
    });

    test('serves both keys during a rotation window', () {
      final previous = base64Encode(
        Uint8List.fromList(List<int>.generate(32, (i) => 31 - i)),
      );
      final rotating = GateOfflineConfig(
        badgeKey: key,
        badgeKeyVersion: 2,
        previousBadgeKey: previous,
        previousBadgeKeyVersion: 1,
        sessionWalkIn: false,
        issuedAtIso: '',
        gates: const <GateOfflineRule>[],
      );
      expect(rotating.keyFor(2), isNotNull);
      expect(rotating.keyFor(1), isNotNull);
      expect(rotating.keyFor(3), isNull);
    });
  });
}

/// judgeOffline never touches the network, so the client is deliberately inert:
/// a test that could reach it would be testing the wrong thing.
class _UnusedClient implements SimfApiClient {
  @override
  dynamic noSuchMethod(Invocation invocation) =>
      throw UnsupportedError('judgeOffline must not call the API');
}
