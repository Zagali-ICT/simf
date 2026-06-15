import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/features/gates/data/gate_models.dart';
import 'package:simf_app/features/gates/data/gates_repository.dart';
import 'package:simf_app/features/gates/gate_scan_screen.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

OperatorGate _gate([String id = 'g1']) => OperatorGate.fromJson(<String, dynamic>{
      'gateId': id,
      'code': 'MAIN',
      'name': 'Main Gate',
      'nameArabic': 'البوابة الرئيسية',
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
  }) async {
    scanCalls++;
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

void main() {
  group('GateScanScreen (D-406, staff gate operator)', () {
    testWidgets('manual entry → allowed result (مسموح)', (tester) async {
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

      await tester.enterText(find.byType(TextField), 'SIMF-2026-V-08431');
      await tester.tap(find.text('Check'));
      await tester.pumpAndSettle();

      expect(repo.scanCalls, 1);
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

      await tester.enterText(find.byType(TextField), 'BADCODE');
      await tester.tap(find.text('Check'));
      await tester.pumpAndSettle();

      expect(find.text('Denied'), findsOneWidget);
      expect(find.text('Holder not approved'), findsOneWidget);
    });

    testWidgets('scan-again returns to the scanner', (tester) async {
      await _pump(tester, _FakeGates(gates: <OperatorGate>[_gate()]));
      await tester.enterText(find.byType(TextField), 'X');
      await tester.tap(find.text('Check'));
      await tester.pumpAndSettle();
      expect(find.text('Allowed'), findsOneWidget);

      await tester.tap(find.widgetWithText(FilledButton, 'Scan again'));
      await tester.pumpAndSettle();
      expect(find.text('Allowed'), findsNothing);
      expect(find.byType(TextField), findsOneWidget); // back on the scanner
    });

    testWidgets('starts paused; resume toggles to hold (D-426)', (tester) async {
      await _pump(tester, _FakeGates(gates: <OperatorGate>[_gate()]));
      // The camera starts paused so the on-screen controls are usable before the
      // live camera (which can swallow taps window-wide on some devices).
      expect(find.widgetWithText(OutlinedButton, 'Resume'), findsOneWidget);
      await tester.tap(find.widgetWithText(OutlinedButton, 'Resume'));
      await tester.pumpAndSettle();
      expect(find.widgetWithText(OutlinedButton, 'Hold'), findsOneWidget);
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
      await tester.enterText(find.byType(TextField), 'X');
      await tester.tap(find.text('Check'));
      await tester.pump(); // let the SnackBar appear
      expect(find.textContaining('Too many attempts'), findsOneWidget);
      expect(find.text('Allowed'), findsNothing);
    });
  });
}
