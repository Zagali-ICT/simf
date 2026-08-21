import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/features/gates/data/gate_models.dart';
import 'package:simf_app/features/gates/data/gate_offline_config.dart';
import 'package:simf_app/features/gates/data/gate_scan_queue.dart';
import 'package:simf_app/features/gates/data/gates_repository.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../accessibility/_fake_prefs.dart';

/// Captures the outgoing request and short-circuits it with a canned envelope,
/// so the assertions read the EXACT map the repository handed dio (an
/// interceptor runs before serialization). The stubbed `recordScan` in
/// `gates_repository_test.dart` deliberately never builds this body, which is
/// why the wire mapping needs its own harness.
class _CapturingInterceptor extends Interceptor {
  _CapturingInterceptor(this.responseData);

  /// The `data` member of the envelope this interceptor answers with.
  final Object? responseData;

  Map<String, dynamic>? body;
  String? path;

  @override
  void onRequest(RequestOptions options, RequestInterceptorHandler handler) {
    path = options.path;
    body = options.data as Map<String, dynamic>?;
    handler.resolve(
      Response<dynamic>(
        requestOptions: options,
        statusCode: 200,
        data: <String, dynamic>{
          'success': true,
          'data': responseData,
          'error': null,
          'meta': null,
        },
      ),
    );
  }
}

({GatesRepository repo, GateOfflineConfigCache cache, FakePrefs prefs}) _build(
  _CapturingInterceptor capture,
) {
  final dio = Dio(
    BaseOptions(
      baseUrl: 'https://api.test/api/v1',
      validateStatus: (_) => true,
    ),
  )..interceptors.add(capture);
  final client = SimfApiClient.build(
    config: const SimfDataConfig(
      baseUrl: 'https://api.test/api/v1',
      appKey: 'k',
      deviceType: SimfDeviceType.android,
    ),
    tokenSource: const NoAuthTokenSource(),
    currentLanguageCode: () => 'en',
    dioOverride: dio,
  );
  // One prefs instance behind both stores, as on a real device.
  final prefs = FakePrefs();
  final repo = GatesRepository(
    client,
    GateScanQueue(prefs),
    GateOfflineConfigCache(prefs),
  );
  // A SECOND cache over the same store stands in for a relaunched console.
  return (repo: repo, cache: GateOfflineConfigCache(prefs), prefs: prefs);
}

_CapturingInterceptor _scanCapture() => _CapturingInterceptor(
      const <String, dynamic>{'scanId': 7, 'outcome': 0, 'direction': 0},
    );

Future<GateScanResult> _scan(
  GatesRepository repo,
  ScanDirection? direction,
) =>
    repo.recordScan(
      gateId: 'g1',
      qr: 'AB12',
      idempotencyKey: 'KEY-1',
      direction: direction,
    );

void main() {
  group('recordScan — the direction integer on the wire (D-509)', () {
    test('a دخول (check-in) choice posts direction 0', () async {
      final capture = _scanCapture();
      final built = _build(capture);

      await _scan(built.repo, ScanDirection.checkIn);

      // The whole point of the assertion: an attendance row is written with
      // the direction this integer names, so a flipped mapping records every
      // entry as an exit and is invisible in the app.
      expect(capture.body!['direction'], 0);
      expect(capture.path, '/app/gates/g1/scans');
    });

    test('a خروج (check-out) choice posts direction 1', () async {
      final capture = _scanCapture();
      final built = _build(capture);

      await _scan(built.repo, ScanDirection.checkOut);

      expect(capture.body!['direction'], 1);
    });

    test('an unset direction omits the key entirely (server auto-resolves)',
        () async {
      final capture = _scanCapture();
      final built = _build(capture);

      await _scan(built.repo, null);

      expect(capture.body!.containsKey('direction'), isFalse);
      // The rest of the frozen body (D-219) travels either way.
      expect(capture.body!['qr'], 'AB12');
      expect(capture.body!['idempotencyKey'], 'KEY-1');
      expect(capture.body!['source'], 1);
    });

    test('the two directions never share one integer', () async {
      final checkIn = _scanCapture();
      await _scan(_build(checkIn).repo, ScanDirection.checkIn);
      final checkOut = _scanCapture();
      await _scan(_build(checkOut).repo, ScanDirection.checkOut);

      expect(checkIn.body!['direction'], isNot(checkOut.body!['direction']));
    });
  });

  group('refreshOfflineConfig — the fetched rules are PERSISTED (D-820)', () {
    _CapturingInterceptor configCapture() => _CapturingInterceptor(
          const <String, dynamic>{
            'badgeKey': null,
            'badgeKeyVersion': 4,
            'previousBadgeKey': null,
            'previousBadgeKeyVersion': 3,
            'sessionWalkIn': true,
            'issuedAt': '2026-08-21T09:00:00.000',
            'gates': <Map<String, dynamic>>[
              <String, dynamic>{
                'gateId': 'g1',
                'code': 'MAIN',
                'allowedProfileTypeCodes': <int>[2],
                'isHallDoor': false,
                'isActive': true,
              },
              <String, dynamic>{
                'gateId': 'g2',
                'code': 'SIDE',
                'allowedProfileTypeCodes': <int>[],
                'isHallDoor': false,
                'isActive': false,
              },
            ],
          },
        );

    test('a later read on this device sees the freshly fetched rules',
        () async {
      final built = _build(configCapture());

      final fetched = await built.repo.refreshOfflineConfig();
      expect(fetched, isNotNull, reason: 'the call itself must succeed');

      // Read back through the repository rather than the returned object: the
      // return value is in memory, and the console re-reads the cache on every
      // scan.
      final cached = built.repo.cachedOfflineConfig();
      expect(cached, isNotNull);
      expect(cached!.badgeKeyVersion, 4);
      expect(cached.sessionWalkIn, isTrue);
      expect(cached.gates.first.code, 'MAIN');
    });

    test('the rules survive a restart (a fresh cache over the same store)',
        () async {
      final built = _build(configCapture());

      await built.repo.refreshOfflineConfig();

      // `built.cache` is a SEPARATE instance over the same prefs — what a
      // relaunched console gets. Without the write, this device boots into a
      // dead network holding nothing and every scan queues with no verdict.
      final afterRestart = built.cache.read();
      expect(afterRestart, isNotNull);
      expect(afterRestart!.badgeKeyVersion, 4);
      expect(afterRestart.previousBadgeKeyVersion, 3);
      expect(afterRestart.gates.first.gateId, 'g1');
    });

    test('judgeOffline decides against the rules the refresh stored',
        () async {
      final built = _build(configCapture());

      // g2 is switched off in the fetched rules. Before the fetch this device
      // has no rules at all, so it cannot judge.
      expect(built.repo.judgeOffline(gateId: 'g2', qr: 'ZZ'), isNull);

      await built.repo.refreshOfflineConfig();

      // Now it refuses at the closed gate — a decision it can only reach from
      // rules that were actually written down.
      final verdict = built.repo.judgeOffline(gateId: 'g2', qr: 'ZZ');
      expect(verdict, isNotNull);
      expect(verdict!.isAllowed, isFalse);
      expect(verdict.reason, OfflineDenialReason.gateInactive);
    });
  });
}
