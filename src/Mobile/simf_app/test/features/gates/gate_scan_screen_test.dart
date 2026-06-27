import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/features/gates/data/gate_models.dart';
import 'package:simf_app/features/gates/data/gates_repository.dart';
import 'package:simf_app/features/gates/gate_scan_screen.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

OperatorGate _gate({String id = 'g1', int directionMode = 2}) =>
    OperatorGate.fromJson(<String, dynamic>{
      'gateId': id,
      'code': 'MAIN',
      'name': 'Main Gate',
      'nameArabic': 'البوابة الرئيسية',
      'directionMode': directionMode,
      'isActive': true,
    });

class _FakeGates implements GatesRepository {
  _FakeGates({
    this.gates = const <OperatorGate>[],
    this.listStatus = 0,
    this.result,
    this.scanStatus = 0,
  });

  List<OperatorGate> gates;
  final int listStatus;
  final GateScanResult? result;
  final int scanStatus;
  int scanCalls = 0;
  ScanDirection? lastDirection;

  @override
  Future<List<OperatorGate>> myAssignments() async {
    if (listStatus != 0) {
      throw ApiFailure(
        code: ApiErrorCodes.clientNetwork,
        message: 'x',
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
        message: 'x',
        httpStatus: scanStatus,
      );
    }
    return result ??
        GateScanResult.fromJson(<String, dynamic>{'outcome': 0, 'direction': 0});
  }
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

/// Walks the new setup → scanner flow (D-509): pick the movement direction, then
/// open the scanner. Defaults to Entry (دخول).
Future<void> _openScanner(WidgetTester tester, {String direction = 'Entry'}) async {
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
      final button =
          tester.widget<FilledButton>(find.widgetWithText(FilledButton, 'Scan code'));
      expect(button.onPressed, isNull);

      await _openScanner(tester);
      expect(find.byType(TextField), findsOneWidget); // scanner is now open
    });

    testWidgets('manual entry → allowed result (مسموح) sends the direction',
        (tester) async {
      final repo = _FakeGates(
        gates: <OperatorGate>[_gate()],
        result: GateScanResult.fromJson(<String, dynamic>{
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
        result: GateScanResult.fromJson(<String, dynamic>{
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
      final button =
          tester.widget<FilledButton>(find.widgetWithText(FilledButton, 'Scan code'));
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
  });
}
