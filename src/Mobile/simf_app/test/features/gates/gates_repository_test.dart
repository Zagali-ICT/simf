import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/features/gates/data/gate_models.dart';
import 'package:simf_app/features/gates/data/gate_offline_config.dart';
import 'package:simf_app/features/gates/data/gate_scan_queue.dart';
import 'package:simf_app/features/gates/data/gates_repository.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../accessibility/_fake_prefs.dart';

/// A throwaway client — `recordScan` is overridden below so it is never used.
final _client = SimfApiClient.build(
  config: const SimfDataConfig(
    baseUrl: 'http://x',
    appKey: 'k',
    deviceType: SimfDeviceType.android,
  ),
  tokenSource: const NoAuthTokenSource(),
  currentLanguageCode: () => 'en',
);

/// Overrides only `recordScan` to simulate a server outcome, and exercises the
/// real `recordScanOrQueue` / `flushPending` over a real `GateScanQueue`.
class _StubGates extends GatesRepository {
  _StubGates(
    super.client,
    super.queue,
    super.offlineConfig,
  );

  /// When set, the next `recordScan` throws it; otherwise it returns
  /// `successResult`. A network failure is `ApiFailure(httpStatus: null)`.
  ApiFailure? nextFailure;

  GateScanResult successResult = GateScanResult.fromJson(
    const <String, dynamic>{'outcome': 0, 'direction': 0},
  );

  /// The idempotency keys `recordScan` actually received, in order.
  final List<String> recordedKeys = <String>[];

  @override
  Future<GateScanResult> recordScan({
    required String gateId,
    required String qr,
    required String idempotencyKey,
    ScanDirection? direction,
  }) async {
    recordedKeys.add(idempotencyKey);
    final failure = nextFailure;
    if (failure != null) {
      throw failure;
    }
    return successResult;
  }
}

_StubGates _build() {
  // One prefs instance for both stores: the queue and the D-820 offline-config
  // cache live side by side on a real device too.
  final prefs = FakePrefs();
  return _StubGates(
    _client,
    GateScanQueue(prefs),
    GateOfflineConfigCache(prefs),
  );
}

ApiFailure _network() =>
    const ApiFailure(code: ApiErrorCodes.clientNetwork, message: 'x');

ApiFailure _http(int status, {String code = 'X'}) =>
    ApiFailure(code: code, message: 'x', httpStatus: status);

Future<GateScanResult?> _record(_StubGates repo) =>
    repo.recordScanOrQueue(gateId: 'g1', qr: 'AB12', idempotencyKey: 'KEY-1');

void main() {
  group('recordScanOrQueue', () {
    test('returns the result on success (nothing queued)', () async {
      final repo = _build();
      final result = await _record(repo);
      expect(result, isNotNull);
      expect(repo.pendingCount(), 0);
    });

    test('a network failure (null status) queues one item and returns null',
        () async {
      final repo = _build()..nextFailure = _network();
      final result = await _record(repo);
      expect(result, isNull);
      expect(repo.pendingCount(), 1);
    });

    test('a 503 GATE_INACTIVE (switched-off gate) RETHROWS, never queues',
        () async {
      final repo = _build()..nextFailure = _http(503, code: 'GATE_INACTIVE');
      await expectLater(_record(repo), throwsA(isA<ApiFailure>()));
      expect(repo.pendingCount(), 0);
    });

    test('a 403 / 409 / 429 RETHROWS, never queues', () async {
      for (final status in <int>[403, 409, 429]) {
        final repo = _build()..nextFailure = _http(status);
        await expectLater(
          _record(repo),
          throwsA(isA<ApiFailure>()),
          reason: 'status $status must rethrow',
        );
        expect(repo.pendingCount(), 0, reason: 'status $status must not queue');
      }
    });
  });

  group('flushPending', () {
    test('drains a queued scan on a later success, reusing its key', () async {
      final repo = _build()..nextFailure = _network();
      await _record(repo); // offline → queued
      expect(repo.pendingCount(), 1);

      repo.nextFailure = null; // link is back
      final remaining = await repo.flushPending();
      expect(remaining, 0);
      expect(repo.pendingCount(), 0);
      // The retry used the ORIGINAL per-scan key (idempotent replay, no
      // double).
      expect(repo.recordedKeys, <String>['KEY-1', 'KEY-1']);
    });

    test('drops a queued scan on a 409 / 403 (a retry cannot change it)',
        () async {
      for (final status in <int>[409, 403]) {
        final repo = _build()..nextFailure = _network();
        await _record(repo);
        expect(repo.pendingCount(), 1);

        repo.nextFailure = _http(status);
        final remaining = await repo.flushPending();
        expect(remaining, 0, reason: 'status $status drops the item');
        expect(repo.pendingCount(), 0);
      }
    });

    test('a 503 GATE_INACTIVE during drain drops the item (not kept forever)',
        () async {
      final repo = _build()..nextFailure = _network();
      await _record(repo);
      expect(repo.pendingCount(), 1);

      repo.nextFailure = _http(503, code: 'GATE_INACTIVE');
      final remaining = await repo.flushPending();
      expect(remaining, 0);
      expect(repo.pendingCount(), 0);
    });

    test('a still-offline (null status) drain STOPS and keeps the item',
        () async {
      final repo = _build()..nextFailure = _network();
      await _record(repo);
      expect(repo.pendingCount(), 1);

      // Still offline on retry.
      final remaining = await repo.flushPending();
      expect(remaining, 1);
      expect(repo.pendingCount(), 1);
    });

    test('a 429 throttle during drain STOPS and keeps the item', () async {
      final repo = _build()..nextFailure = _network();
      await _record(repo);
      expect(repo.pendingCount(), 1);

      repo.nextFailure = _http(429);
      final remaining = await repo.flushPending();
      expect(remaining, 1);
      expect(repo.pendingCount(), 1);
    });
  });
}
