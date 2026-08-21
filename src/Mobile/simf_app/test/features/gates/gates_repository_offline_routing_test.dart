import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/features/gates/data/gate_models.dart';
import 'package:simf_app/features/gates/data/gate_offline_config.dart';
import 'package:simf_app/features/gates/data/gate_scan_queue.dart';
import 'package:simf_app/features/gates/data/gates_repository.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../accessibility/_fake_prefs.dart';

/// A throwaway client — `recordScan` is overridden below so it never fires.
final _client = SimfApiClient.build(
  config: const SimfDataConfig(
    baseUrl: 'http://x',
    appKey: 'k',
    deviceType: SimfDeviceType.android,
  ),
  tokenSource: const NoAuthTokenSource(),
  currentLanguageCode: () => 'en',
);

/// Overrides `recordScan` PER IDEMPOTENCY KEY, so a drain can be made to fail
/// mid-backlog.
class _KeyedStubGates extends GatesRepository {
  _KeyedStubGates(super._client, super._queue, super._offlineConfig);

  /// Key -> the failure that key's attempt throws. An absent key succeeds.
  final Map<String, ApiFailure> failures = <String, ApiFailure>{};

  /// Every key `recordScan` was actually asked for, in order.
  final List<String> attempted = <String>[];

  @override
  Future<GateScanResult> recordScan({
    required String gateId,
    required String qr,
    required String idempotencyKey,
    ScanDirection? direction,
  }) async {
    attempted.add(idempotencyKey);
    final failure = failures[idempotencyKey];
    if (failure != null) {
      throw failure;
    }
    return GateScanResult.fromJson(
      const <String, dynamic>{'outcome': 0, 'direction': 0},
    );
  }
}

({_KeyedStubGates repo, GateScanQueue queue}) _build() {
  final prefs = FakePrefs();
  final queue = GateScanQueue(prefs);
  return (
    repo: _KeyedStubGates(_client, queue, GateOfflineConfigCache(prefs)),
    queue: queue,
  );
}

/// Seeds the backlog directly, bypassing the queue-or-throw decision above.
Future<void> _seed(GateScanQueue queue, List<String> keys) async {
  for (final key in keys) {
    await queue.enqueue(
      PendingGateScan(
        gateId: 'g1',
        qr: 'QR-$key',
        idempotencyKey: key,
        queuedAtIso: '2026-08-21T09:00:00.000',
      ),
    );
  }
}

List<String> _queuedKeys(GateScanQueue queue) =>
    queue.all().map((s) => s.idempotencyKey).toList();

Future<GateScanResult?> _record(_KeyedStubGates repo, ApiFailure failure) {
  repo.failures['KEY-1'] = failure;
  return repo.recordScanOrQueue(
    gateId: 'g1',
    qr: 'AB12',
    idempotencyKey: 'KEY-1',
  );
}

void main() {
  group('a failure is queued ONLY when no response came back', () {
    test('a network failure (no response at all) is queued', () async {
      final built = _build();
      final result = await _record(
        built.repo,
        const ApiFailure(code: ApiErrorCodes.clientNetwork, message: 'down'),
      );

      expect(result, isNull, reason: 'no verdict to show the operator');
      expect(_queuedKeys(built.queue), <String>['KEY-1']);
    });

    test(
        'a TIMEOUT is queued too — the absent response decides, not which '
        'client code named it', () async {
      final built = _build();
      // A timeout maps to CLIENT_TIMEOUT with no status — a different code
      // string, but still no response, so it must queue like a network drop.
      final result = await _record(
        built.repo,
        const ApiFailure(code: ApiErrorCodes.clientTimeout, message: 'slow'),
      );

      expect(result, isNull);
      expect(_queuedKeys(built.queue), <String>['KEY-1']);
    });

    test('a 500 RETHROWS even when it is reported as a network-class code',
        () async {
      final built = _build();
      // dio maps a 500 to CLIENT_NETWORK *carrying the status*, and the status
      // is what decides — a response arrived, so it must not queue.
      await expectLater(
        _record(
          built.repo,
          const ApiFailure(
            code: ApiErrorCodes.clientNetwork,
            message: 'boom',
            httpStatus: 500,
          ),
        ),
        throwsA(isA<ApiFailure>()),
      );
      expect(built.queue.all(), isEmpty);
    });

    test('a malformed 500 body RETHROWS as well', () async {
      final built = _build();
      await expectLater(
        _record(
          built.repo,
          const ApiFailure(
            code: ApiErrorCodes.clientMalformedResponse,
            message: 'html error page',
            httpStatus: 500,
          ),
        ),
        throwsA(isA<ApiFailure>()),
      );
      expect(built.queue.all(), isEmpty);
    });

    test('a 4xx RETHROWS — the server decided, so the operator must see it',
        () async {
      for (final status in <int>[400, 403, 404]) {
        final built = _build();
        await expectLater(
          _record(
            built.repo,
            ApiFailure(
              code: ApiErrorCodes.clientNetwork,
              message: 'no',
              httpStatus: status,
            ),
          ),
          throwsA(isA<ApiFailure>()),
          reason: '$status must reach the operator',
        );
        expect(built.queue.all(), isEmpty, reason: '$status must not queue');
      }
    });
  });

  group('flushPending stops at the first unreachable scan', () {
    test('a mid-backlog network failure leaves it AND everything after it',
        () async {
      final built = _build();
      await _seed(built.queue, <String>['K1', 'K2', 'K3']);
      built.repo.failures['K2'] =
          const ApiFailure(code: ApiErrorCodes.clientNetwork, message: 'down');

      final remaining = await built.repo.flushPending();

      expect(remaining, 2);
      expect(_queuedKeys(built.queue), <String>['K2', 'K3']);
      // K3 was never tried: the link is down, so the drain stops rather than
      // burning the tablet's battery and the server's rate limit.
      expect(built.repo.attempted, <String>['K1', 'K2']);
    });

    test('a mid-backlog 429 throttle stops the drain the same way', () async {
      final built = _build();
      await _seed(built.queue, <String>['K1', 'K2', 'K3']);
      built.repo.failures['K2'] = const ApiFailure(
        code: ApiErrorCodes.rateLimitExceeded,
        message: 'slow down',
        httpStatus: 429,
      );

      final remaining = await built.repo.flushPending();

      expect(remaining, 2);
      expect(_queuedKeys(built.queue), <String>['K2', 'K3']);
      expect(built.repo.attempted, <String>['K1', 'K2']);
    });

    test('a mid-backlog 409 does NOT stop it — that one scan is settled',
        () async {
      final built = _build();
      await _seed(built.queue, <String>['K1', 'K2', 'K3']);
      built.repo.failures['K2'] = const ApiFailure(
        code: 'IDEMPOTENCY_CONFLICT',
        message: 'already recorded',
        httpStatus: 409,
      );

      final remaining = await built.repo.flushPending();

      expect(remaining, 0);
      expect(built.queue.all(), isEmpty);
      expect(built.repo.attempted, <String>['K1', 'K2', 'K3']);
    });
  });
}
