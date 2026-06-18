import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/features/auth/biometric_auth.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// A controllable [BiometricAuth] — `implements` ignores the real constructor
/// (which needs a Ref + local_auth), so the prompt can be driven without the
/// device plugin.
class _FakeBiometricAuth implements BiometricAuth {
  _FakeBiometricAuth({this.available = true, this.enabled = false});

  bool available;
  bool enabled;
  int enableCalls = 0;
  int disableCalls = 0;
  BiometricEnableResult enableResult = BiometricEnableResult.ok;

  @override
  Future<bool> isAvailable() async => available;
  @override
  Future<bool> isEnabled() async => enabled;
  @override
  Future<BiometricEnableResult> enable() async {
    enableCalls++;
    return enableResult;
  }

  @override
  Future<void> disable() async {
    disableCalls++;
  }
}

class _FakePrefs implements SimfPrefsStorage {
  final Map<String, Object> _store = <String, Object>{};

  @override
  bool? getBool(String key) => _store[key] as bool?;
  @override
  Future<bool> setBool(String key, bool value) async {
    _store[key] = value;
    return true;
  }

  @override
  String? getString(String key) => _store[key] as String?;
  @override
  Future<bool> setString(String key, String value) async {
    _store[key] = value;
    return true;
  }

  @override
  double? getDouble(String key) => null;
  @override
  Future<bool> setDouble(String key, double value) async => true;
  @override
  int? getInt(String key) => null;
  @override
  Future<bool> setInt(String key, int value) async => true;
  @override
  Future<bool> remove(String key) async {
    _store.remove(key);
    return true;
  }
}

Future<void> _pump(
  WidgetTester tester, {
  required _FakeBiometricAuth biometric,
  required _FakePrefs prefs,
}) async {
  await tester.pumpWidget(
    ProviderScope(
      overrides: <Override>[
        simfPrefsStorageProvider.overrideWithValue(prefs),
        biometricAuthProvider.overrideWithValue(biometric),
      ],
      child: MaterialApp(
        locale: const Locale('en'),
        supportedLocales: AppL10n.supportedLocales,
        localizationsDelegates: const <LocalizationsDelegate<dynamic>>[
          ...AppL10n.localizationsDelegates,
          GlobalMaterialLocalizations.delegate,
          GlobalWidgetsLocalizations.delegate,
          GlobalCupertinoLocalizations.delegate,
        ],
        home: Consumer(
          builder: (context, ref, _) => Scaffold(
            body: Center(
              child: ElevatedButton(
                onPressed: () => maybeOfferBiometricEnrolment(context, ref),
                child: const Text('GO'),
              ),
            ),
          ),
        ),
      ),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  group('maybeOfferBiometricEnrolment (D-441)', () {
    testWidgets('available + not enabled + not handled → offers, then enables '
        'on Enable and marks handled', (tester) async {
      final biometric = _FakeBiometricAuth(available: true, enabled: false);
      final prefs = _FakePrefs();
      await _pump(tester, biometric: biometric, prefs: prefs);

      await tester.tap(find.text('GO'));
      await tester.pumpAndSettle();

      // The one-time nudge dialog appears.
      expect(find.text('Enable Face ID sign-in?'), findsOneWidget);

      await tester.tap(find.widgetWithText(FilledButton, 'Enable'));
      await tester.pumpAndSettle();

      expect(biometric.enableCalls, 1);
      expect(prefs.getBool(StorageKeys.biometricPromptHandled), isTrue);
      expect(find.text('Face ID sign-in enabled'), findsOneWidget);
    });

    testWidgets('Enable that fails leaves the prompt armed (self-heal) and '
        'toasts the failure', (tester) async {
      final biometric = _FakeBiometricAuth(available: true, enabled: false)
        ..enableResult = BiometricEnableResult.failed;
      final prefs = _FakePrefs();
      await _pump(tester, biometric: biometric, prefs: prefs);

      await tester.tap(find.text('GO'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, 'Enable'));
      await tester.pumpAndSettle();

      expect(biometric.enableCalls, 1);
      // Not burned → the next sign-in re-offers.
      expect(prefs.getBool(StorageKeys.biometricPromptHandled), isNot(true));
      expect(find.text("Couldn't enable Face ID sign-in"), findsOneWidget);
    });

    testWidgets('Not now → no enrol, but still marked handled (one-time)',
        (tester) async {
      final biometric = _FakeBiometricAuth(available: true, enabled: false);
      final prefs = _FakePrefs();
      await _pump(tester, biometric: biometric, prefs: prefs);

      await tester.tap(find.text('GO'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(TextButton, 'Not now'));
      await tester.pumpAndSettle();

      expect(biometric.enableCalls, 0);
      expect(prefs.getBool(StorageKeys.biometricPromptHandled), isTrue);
    });

    testWidgets('already handled → no dialog', (tester) async {
      final biometric = _FakeBiometricAuth(available: true, enabled: false);
      final prefs = _FakePrefs();
      await prefs.setBool(StorageKeys.biometricPromptHandled, true);
      await _pump(tester, biometric: biometric, prefs: prefs);

      await tester.tap(find.text('GO'));
      await tester.pumpAndSettle();

      expect(find.text('Enable Face ID sign-in?'), findsNothing);
      expect(biometric.enableCalls, 0);
    });

    testWidgets('already enabled → no dialog', (tester) async {
      final biometric = _FakeBiometricAuth(available: true, enabled: true);
      final prefs = _FakePrefs();
      await _pump(tester, biometric: biometric, prefs: prefs);

      await tester.tap(find.text('GO'));
      await tester.pumpAndSettle();

      expect(find.text('Enable Face ID sign-in?'), findsNothing);
    });

    testWidgets('biometrics unavailable → no dialog', (tester) async {
      final biometric = _FakeBiometricAuth(available: false, enabled: false);
      final prefs = _FakePrefs();
      await _pump(tester, biometric: biometric, prefs: prefs);

      await tester.tap(find.text('GO'));
      await tester.pumpAndSettle();

      expect(find.text('Enable Face ID sign-in?'), findsNothing);
    });
  });
}
