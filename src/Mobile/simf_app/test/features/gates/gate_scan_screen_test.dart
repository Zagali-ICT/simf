import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/features/gates/data/gate_models.dart';
import 'package:simf_app/features/gates/data/gate_offline_config.dart';
import 'package:simf_app/features/gates/data/gates_repository.dart';
import 'package:simf_app/features/gates/data/offline_badge.dart';
import 'package:simf_app/features/gates/gate_scan_screen.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

OperatorGate _gate({
  String id = 'g1',
  int directionMode = 2,
  String name = 'Main Gate',
  bool isActive = true,
}) =>
    OperatorGate.fromJson(<String, dynamic>{
      'gateId': id,
      'code': 'MAIN',
      'name': name,
      'nameArabic': 'البوابة الرئيسية',
      'directionMode': directionMode,
      'isActive': isActive,
    });

class _FakeGates implements GatesRepository {
  _FakeGates({
    this.gates = const <OperatorGate>[],
    this.listStatus = 0,
    this.listMessage = '',
    this.result,
    this.scanStatus = 0,
    this.scanMessage = '',
    this.offline = false,
    this.offlineVerdict,
  });

  List<OperatorGate> gates;

  /// D-821 — what `judgeOffline` returns while [offline] is set.
  final OfflineGateVerdict? offlineVerdict;

  /// D-821 — how many times the console refreshed its offline rules.
  int offlineConfigRefreshes = 0;

  final int listStatus;

  /// The server's own message on a failed my-assignments call. Blank models a
  /// bare policy 403 (no envelope body) — the screen then shows its own copy.
  final String listMessage;
  final GateScanResult? result;

  /// A hard HTTP failure the scan surfaces (rethrown), e.g. 429. 0 = success.
  final int scanStatus;

  /// The server's own message on that failure.
  final String scanMessage;

  /// Simulates a no-verdict network failure (null httpStatus): the scan is
  /// queued on-device and `recordScanOrQueue` returns null (G-4).
  final bool offline;

  int scanCalls = 0;
  ScanDirection? lastDirection;

  /// The idempotency keys the screen sent, in order (G-1 asserts a fresh one
  /// per scan).
  final List<String> idempotencyKeys = <String>[];

  /// The on-device backlog, backing [pendingCount] / [flushPending].
  final List<String> _pendingKeys = <String>[];

  @override
  Future<List<OperatorGate>> myAssignments() async {
    if (listStatus != 0) {
      throw ApiFailure(
        code: ApiErrorCodes.clientNetwork,
        message: listMessage,
        httpStatus: listStatus,
      );
    }
    return gates;
  }

  @override
  Future<GateScanResult> recordScan({
    required String gateId,
    required String qr,
    required String idempotencyKey,
    ScanDirection? direction,
  }) async {
    scanCalls++;
    lastDirection = direction;
    if (scanStatus != 0) {
      throw ApiFailure(
        code: ApiErrorCodes.clientNetwork,
        message: scanMessage,
        httpStatus: scanStatus,
      );
    }
    return result ??
        GateScanResult.fromJson(
            const <String, dynamic>{'outcome': 0, 'direction': 0},);
  }

  @override
  Future<GateScanResult?> recordScanOrQueue({
    required String gateId,
    required String qr,
    required String idempotencyKey,
    ScanDirection? direction,
  }) async {
    scanCalls++;
    lastDirection = direction;
    idempotencyKeys.add(idempotencyKey);
    if (offline) {
      // No verdict from the server → queue for retry, no toast/result.
      _pendingKeys.add(idempotencyKey);
      return null;
    }
    if (scanStatus != 0) {
      throw ApiFailure(
        code: ApiErrorCodes.clientNetwork,
        message: scanMessage,
        httpStatus: scanStatus,
      );
    }
    return result ??
        GateScanResult.fromJson(
            const <String, dynamic>{'outcome': 0, 'direction': 0},);
  }

  @override
  int pendingCount() => _pendingKeys.length;

  // D-821 — the on-device verdict the console shows when the server is
  // unreachable. Null (the default) is "the device could not decide", which is
  // the pre-D-821 behaviour. The verdict LOGIC has its own suite
  // (gate_offline_verdict_test.dart); these tests cover what the operator sees.
  @override
  Future<GateOfflineConfig?> refreshOfflineConfig() async {
    offlineConfigRefreshes++;
    return null;
  }

  @override
  GateOfflineConfig? cachedOfflineConfig() => null;

  @override
  OfflineGateVerdict? judgeOffline({
    required String gateId,
    required String qr,
  }) =>
      offlineVerdict;

  @override
  Future<int> flushPending() async => _pendingKeys.length;
}

Future<void> _pump(WidgetTester tester, _FakeGates repo) async {
  await tester.pumpWidget(
    ProviderScope(
      overrides: <Override>[
        gatesRepositoryProvider.overrideWithValue(repo),
      ],
      child: const MaterialApp(
        localizationsDelegates: <LocalizationsDelegate<dynamic>>[
          ...AppL10n.localizationsDelegates,
          GlobalMaterialLocalizations.delegate,
          GlobalWidgetsLocalizations.delegate,
          GlobalCupertinoLocalizations.delegate,
        ],
        supportedLocales: AppL10n.supportedLocales,
        home: GateScanScreen(enableCamera: false),
      ),
    ),
  );
  await tester.pumpAndSettle();
}

/// Walks the new setup → scanner flow (D-509): pick the movement direction,
/// then open the scanner. Defaults to Entry (دخول).
Future<void> _openScanner(WidgetTester tester,
    {String direction = 'Entry',}) async {
  await tester.tap(find.text(direction));
  await tester.pumpAndSettle();
  await tester.tap(find.widgetWithText(FilledButton, 'Scan code'));
  await tester.pumpAndSettle();
}

void main() {
  group('GateScanScreen (D-406/D-509, staff gate operator)', () {
    testWidgets('Both gate: a movement type is required before scanning',
        (tester) async {
      await _pump(tester, _FakeGates(gates: <OperatorGate>[_gate()]));
      // Setup is shown (the hint + the disabled scan button), no scanner yet.
      expect(find.text('Scan code'), findsOneWidget);
      expect(find.byType(TextField), findsNothing);
      // Scan stays disabled until a direction is picked.
      final button = tester
          .widget<FilledButton>(find.widgetWithText(FilledButton, 'Scan code'));
      expect(button.onPressed, isNull);

      await _openScanner(tester);
      expect(find.byType(TextField), findsOneWidget); // scanner is now open
    });

    testWidgets('manual entry → allowed result (مسموح) sends the direction',
        (tester) async {
      final repo = _FakeGates(
        gates: <OperatorGate>[_gate()],
        result: GateScanResult.fromJson(const <String, dynamic>{
          'outcome': 0,
          'direction': 0,
          'userProfile': <String, dynamic>{
            'displayName': 'Raed',
            'displayNameArabic': 'راند',
            'profileTypeName': 'VIP',
          },
        }),
      );
      await _pump(tester, repo);
      await _openScanner(tester, direction: 'Exit');

      await tester.enterText(find.byType(TextField), 'SIMF-2026-V-08431');
      await tester.tap(find.text('Check'));
      await tester.pumpAndSettle();

      expect(repo.scanCalls, 1);
      expect(repo.lastDirection, ScanDirection.checkOut);
      expect(find.text('Allowed'), findsOneWidget);
      expect(find.text('Raed'), findsOneWidget);
      expect(find.text('VIP'), findsOneWidget);
      expect(find.widgetWithText(FilledButton, 'Scan again'), findsOneWidget);
    });

    testWidgets('manual entry → denied result (ممنوع) with the message',
        (tester) async {
      final repo = _FakeGates(
        gates: <OperatorGate>[_gate()],
        result: GateScanResult.fromJson(const <String, dynamic>{
          'outcome': 1,
          'direction': 0,
          'denialReasonCode': 2,
          'denialMessage': 'Holder not approved',
        }),
      );
      await _pump(tester, repo);
      await _openScanner(tester);

      await tester.enterText(find.byType(TextField), 'BADCODE');
      await tester.tap(find.text('Check'));
      await tester.pumpAndSettle();

      expect(find.text('Denied'), findsOneWidget);
      expect(find.text('Holder not approved'), findsOneWidget);
    });

    testWidgets('a fixed In gate auto-selects Entry and can scan immediately',
        (tester) async {
      final repo = _FakeGates(gates: <OperatorGate>[_gate(directionMode: 0)]);
      await _pump(tester, repo);
      // No movement choice needed — scan is enabled straight away.
      final button = tester
          .widget<FilledButton>(find.widgetWithText(FilledButton, 'Scan code'));
      expect(button.onPressed, isNotNull);
      await tester.tap(find.widgetWithText(FilledButton, 'Scan code'));
      await tester.pumpAndSettle();
      await tester.enterText(find.byType(TextField), 'X');
      await tester.tap(find.text('Check'));
      await tester.pumpAndSettle();
      expect(repo.lastDirection, ScanDirection.checkIn);
    });

    testWidgets('scan-again returns to the scanner', (tester) async {
      await _pump(tester, _FakeGates(gates: <OperatorGate>[_gate()]));
      await _openScanner(tester);
      await tester.enterText(find.byType(TextField), 'X');
      await tester.tap(find.text('Check'));
      await tester.pumpAndSettle();
      expect(find.text('Allowed'), findsOneWidget);

      await tester.tap(find.widgetWithText(FilledButton, 'Scan again'));
      await tester.pumpAndSettle();
      expect(find.text('Allowed'), findsNothing);
      expect(find.byType(TextField), findsOneWidget); // back on the scanner
    });

    testWidgets('no assignments shows the not-assigned state', (tester) async {
      await _pump(tester, _FakeGates());
      expect(find.textContaining('not assigned to any gate'), findsOneWidget);
    });

    testWidgets('403 on load shows the not-authorised state', (tester) async {
      await _pump(tester, _FakeGates(listStatus: 403));
      expect(find.textContaining('not authorised to operate'), findsOneWidget);
    });

    testWidgets('a non-403 load failure shows retry', (tester) async {
      await _pump(tester, _FakeGates(listStatus: 500));
      expect(find.widgetWithText(FilledButton, 'Retry'), findsOneWidget);
    });

    testWidgets('a 429 scan failure shows the rate-limit toast, no result',
        (tester) async {
      await _pump(
        tester,
        _FakeGates(gates: <OperatorGate>[_gate()], scanStatus: 429),
      );
      await _openScanner(tester);
      await tester.enterText(find.byType(TextField), 'X');
      await tester.tap(find.text('Check'));
      await tester.pump(); // let the SnackBar appear
      expect(find.textContaining('Too many attempts'), findsOneWidget);
      expect(find.text('Allowed'), findsNothing);
    });

    testWidgets('G-1: each scan sends a fresh distinct UUIDv4 idempotency key',
        (tester) async {
      final uuidV4 = RegExp(
        r'^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$',
      );
      final repo = _FakeGates(gates: <OperatorGate>[_gate()]);
      await _pump(tester, repo);
      await _openScanner(tester);
      await tester.enterText(find.byType(TextField), 'X');
      await tester.tap(find.text('Check'));
      await tester.pumpAndSettle();
      expect(find.text('Allowed'), findsOneWidget);

      // Scan again → a genuine re-entry of the next holder, a fresh key.
      await tester.tap(find.widgetWithText(FilledButton, 'Scan again'));
      await tester.pumpAndSettle();
      await tester.enterText(find.byType(TextField), 'Y');
      await tester.tap(find.text('Check'));
      await tester.pumpAndSettle();

      expect(repo.idempotencyKeys.length, 2);
      expect(repo.idempotencyKeys[0], isNot(repo.idempotencyKeys[1]));
      expect(uuidV4.hasMatch(repo.idempotencyKeys[0]), isTrue);
      expect(uuidV4.hasMatch(repo.idempotencyKeys[1]), isTrue);
    });

    testWidgets(
        'G-4: an unreachable server queues the scan and shows '
        'saved-offline + the waiting-to-sync banner', (tester) async {
      final repo = _FakeGates(gates: <OperatorGate>[_gate()], offline: true);
      await _pump(tester, repo);
      await _openScanner(tester);
      await tester.enterText(find.byType(TextField), 'X');
      await tester.tap(find.text('Check'));
      await tester.pump(); // resolve the async + setState
      await tester.pump(); // render the snackbar frame

      // The saved-offline snackbar, no allowed result, and the sync banner.
      expect(
        find.text(
          'No connection — the scan was saved and will retry automatically.',
        ),
        findsOneWidget,
      );
      expect(find.text('Allowed'), findsNothing);
      expect(find.textContaining('waiting to sync'), findsOneWidget);
      expect(repo.pendingCount(), 1);
    });

    testWidgets(
        'D-821: an offline ALLOWED verdict is shown as provisional, '
        'never as a final answer', (tester) async {
      final repo = _FakeGates(
        gates: <OperatorGate>[_gate()],
        offline: true,
        offlineVerdict: const OfflineGateVerdict.allowed(
          OfflineBadge(profileTypeCode: 1, sequence: 3000042),
        ),
      );
      await _pump(tester, repo);
      await _openScanner(tester);
      await tester.enterText(find.byType(TextField), 'X');
      await tester.tap(find.text('Check'));
      await tester.pump();
      await tester.pump();

      expect(
        find.text('Allowed (offline) — saved for confirmation.'),
        findsOneWidget,
      );
      // NOT the server's allowed card: the decision is still the server's when
      // the queue drains, and the operator must not read this as final.
      expect(find.text('Allowed'), findsNothing);
      expect(repo.pendingCount(), 1);
    });

    testWidgets(
        'D-821: an offline DENIED verdict names the reason and still '
        'says the scan was saved', (tester) async {
      final repo = _FakeGates(
        gates: <OperatorGate>[_gate()],
        offline: true,
        offlineVerdict: const OfflineGateVerdict.denied(
          OfflineDenialReason.profileTypeNotAllowed,
        ),
      );
      await _pump(tester, repo);
      await _openScanner(tester);
      await tester.enterText(find.byType(TextField), 'X');
      await tester.tap(find.text('Check'));
      await tester.pump();
      await tester.pump();

      expect(
        find.textContaining('This badge type is not allowed at this gate.'),
        findsOneWidget,
      );
      // Saying only "denied" would imply nothing was recorded.
      expect(find.textContaining('the scan was saved'), findsOneWidget);
    });

    testWidgets('D-821: opening the console refreshes the offline rules',
        (tester) async {
      // Without this the device would fall back on rules from whenever it last
      // happened to sync, or on nothing at all.
      final repo = _FakeGates(gates: <OperatorGate>[_gate()]);
      await _pump(tester, repo);

      expect(repo.offlineConfigRefreshes, greaterThanOrEqualTo(1));
    });
  });

  group('GateScanScreen — deferred gate-console defects', () {
    testWidgets(
        "DEF-STF-005 — a 403 on load shows the SERVER's reason, not "
        'the generic permission copy', (tester) async {
      await _pump(
        tester,
        _FakeGates(
          listStatus: 403,
          listMessage: 'You are not assigned to this gate.',
        ),
      );

      // "no Gates.Operate grant" and "not assigned to this gate" are both 403
      // but need different operator actions; flattening them made the second
      // one undiagnosable.
      expect(find.text('You are not assigned to this gate.'), findsOneWidget);
      expect(find.textContaining('not authorised to operate'), findsNothing);
    });

    testWidgets('DEF-STF-005 — a 403 on the scan surfaces the server message',
        (tester) async {
      await _pump(
        tester,
        _FakeGates(
          gates: <OperatorGate>[_gate()],
          scanStatus: 403,
          scanMessage: 'You are not assigned to this gate.',
        ),
      );
      await _openScanner(tester);
      await tester.enterText(find.byType(TextField), 'X');
      await tester.tap(find.text('Check'));
      await tester.pump();

      expect(find.text('You are not assigned to this gate.'), findsOneWidget);
    });

    testWidgets(
        'DEF-STF-005 — a 403 with no server body keeps the generic copy',
        (tester) async {
      await _pump(tester, _FakeGates(listStatus: 403));
      expect(find.textContaining('not authorised to operate'), findsOneWidget);
    });

    testWidgets(
        'DEF-STF-006 — an inactive gate is tagged in the picker and '
        'warns before the first scan', (tester) async {
      await _pump(
        tester,
        _FakeGates(
          gates: <OperatorGate>[
            _gate(name: 'Closed Gate', isActive: false),
            _gate(id: 'g2', name: 'Working Gate'),
          ],
        ),
      );

      // The list used to render an inactive gate exactly like a working one, so
      // every scan came back as a red denial with nothing pointing at the gate.
      expect(find.text('Closed Gate — inactive'), findsWidgets);
      expect(find.text('Working Gate'), findsNothing);
      expect(
        find.textContaining('This gate is inactive'),
        findsOneWidget,
      );
    });

    testWidgets('DEF-STF-006 — an ACTIVE gate carries no tag and no warning',
        (tester) async {
      await _pump(tester, _FakeGates(gates: <OperatorGate>[_gate()]));

      expect(find.text('Main Gate'), findsWidgets);
      expect(find.textContaining('inactive'), findsNothing);
    });
  });
}
