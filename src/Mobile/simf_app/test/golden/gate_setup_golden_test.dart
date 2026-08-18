@Tags(<String>['golden'])
library;

import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/app_theme.dart';
import 'package:simf_app/features/gates/data/gate_models.dart';
import 'package:simf_app/features/gates/data/gate_offline_config.dart';
import 'package:simf_app/features/gates/data/gates_repository.dart';
import 'package:simf_app/features/gates/gate_scan_screen.dart';

import '../support/simf_test_scope.dart';
import 'golden_fonts.dart';

/// Golden render of the gate-operator **setup** stage against Figma frame
/// **758:4651** ("فحص رمز QR — موظف"). Regenerate + compare:
///   flutter test --update-goldens test/golden/gate_setup_golden_test.dart
///
/// Frame parity: circular navy back button (left, forced-LTR bar) + centred
/// white title; the gold-tinted, gold-bordered QR tile; the "اختر البوابة"
/// dropdown (navy fill, gold hairline, البوابة الرئيسية); the "نوع الحركة"
/// دخول/خروج pills; and the choose-direction hint. Captured in the initial
/// **Both**-gate state (both pills unselected, hint shown, scan disabled) — the
/// frame additionally highlights دخول, which is the post-selection state.

final _gate = OperatorGate.fromJson(const <String, dynamic>{
  'gateId': 'g1',
  'code': 'MAIN',
  'name': 'Main Gate',
  'nameArabic': 'البوابة الرئيسية',
  'directionMode': 2,
  'isActive': true,
});

class _FakeGates implements GatesRepository {
  @override
  Future<List<OperatorGate>> myAssignments() async => <OperatorGate>[_gate];

  @override
  Future<GateScanResult> recordScan({
    required String gateId,
    required String qr,
    required String idempotencyKey,
    ScanDirection? direction,
  }) =>
      throw UnimplementedError();

  @override
  Future<GateScanResult?> recordScanOrQueue({
    required String gateId,
    required String qr,
    required String idempotencyKey,
    ScanDirection? direction,
  }) =>
      throw UnimplementedError();

  // The setup golden renders with an empty backlog → the sync banner is hidden.
  @override
  int pendingCount() => 0;

  // D-821 — the setup stage renders before any scan, so the offline path is
  // inert here. The refresh must still be answerable: the console fires it on
  // load, and an unimplemented call would fail the golden with a stack trace
  // instead of a pixel diff.
  @override
  Future<GateOfflineConfig?> refreshOfflineConfig() async => null;

  @override
  GateOfflineConfig? cachedOfflineConfig() => null;

  @override
  OfflineGateVerdict? judgeOffline({
    required String gateId,
    required String qr,
  }) =>
      null;

  @override
  Future<int> flushPending() async => 0;
}

void main() {
  setUpAll(loadGoldenFonts);

  testWidgets('Gate setup @375x812 — Figma 758:4651 (Arabic)', (tester) async {
    tester.view.physicalSize = const Size(375, 812);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

    await tester.pumpWidget(
      simfTestScope(
        overrides: <Override>[
          gatesRepositoryProvider.overrideWithValue(_FakeGates()),
        ],
        child: MaterialApp(
          debugShowCheckedModeBanner: false,
          theme: SimfTheme.dark(),
          locale: const Locale('ar'),
          supportedLocales: AppL10n.supportedLocales,
          localizationsDelegates: const <LocalizationsDelegate<dynamic>>[
            ...AppL10n.localizationsDelegates,
            GlobalMaterialLocalizations.delegate,
            GlobalWidgetsLocalizations.delegate,
            GlobalCupertinoLocalizations.delegate,
          ],
          home: const GateScanScreen(enableCamera: false),
        ),
      ),
    );
    await tester.pumpAndSettle();

    await expectLater(
      find.byType(GateScanScreen),
      matchesGoldenFile('goldens/gate_setup_758-4651.png'),
    );
  });
}
