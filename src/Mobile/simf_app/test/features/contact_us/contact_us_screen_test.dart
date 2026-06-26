import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/core/organization_profile/organization_profile.dart';
import 'package:simf_app/features/contact_us/contact_us_screen.dart';
import 'package:simf_app/features/contact_us/data/contact_us_repository.dart';

/// Records the last submitted inquiry so the test can assert the wire call.
class _FakeContactUsRepository implements ContactUsRepository {
  ({String name, String email, String message})? lastSubmit;

  @override
  Future<void> submit({
    required String name,
    required String email,
    required String message,
  }) async {
    lastSubmit = (name: name, email: email, message: message);
  }
}

/// Pins the shared org profile so the contact-info panel + social render.
class _StubOrgProfile extends OrgProfileController {
  _StubOrgProfile(this._value);
  final OrgProfile? _value;
  @override
  OrgProfile? build() => _value;
  @override
  Future<void> warm() async {}
}

OrgProfile _profile() => const OrgProfile(
      name: 'SIMF',
      nameArabic: 'سيمف',
      title: 'Forum',
      titleArabic: 'الملتقى',
      currentYear: 2026,
      status: 'Open',
      social: OrgSocial(x: 'https://x.com/simf'),
      aboutItems: <OrgAboutItem>[],
      details: <OrgDetail>[],
      contactPhone: '+966 11 123 4567',
      contactEmail: 'info@simf2026.sa',
      locationText: 'Riyadh Convention Center',
      locationTextArabic: 'مركز الرياض للمؤتمرات',
    );

Future<void> _pump(
  WidgetTester tester,
  _FakeContactUsRepository repo, {
  OrgProfile? profile,
}) async {
  tester.view.physicalSize = const Size(375, 2200);
  tester.view.devicePixelRatio = 1.0;
  addTearDown(tester.view.resetPhysicalSize);
  addTearDown(tester.view.resetDevicePixelRatio);

  await tester.pumpWidget(
    ProviderScope(
      overrides: <Override>[
        contactUsRepositoryProvider.overrideWithValue(repo),
        orgProfileProvider.overrideWith(() => _StubOrgProfile(profile)),
      ],
      child: const MaterialApp(
        locale: Locale('en'),
        supportedLocales: AppL10n.supportedLocales,
        localizationsDelegates: <LocalizationsDelegate<dynamic>>[
          ...AppL10n.localizationsDelegates,
          GlobalMaterialLocalizations.delegate,
          GlobalWidgetsLocalizations.delegate,
          GlobalCupertinoLocalizations.delegate,
        ],
        home: ContactUsScreen(),
      ),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  group('ContactUsScreen (Page 203 — KSA frame 1388:7711)', () {
    testWidgets('renders the form + contact-info panel from the profile',
        (tester) async {
      await _pump(tester, _FakeContactUsRepository(), profile: _profile());

      expect(find.text('Contact us'), findsOneWidget);
      expect(find.text('Send a message'), findsOneWidget);
      expect(find.text('Send'), findsOneWidget);
      // Info panel hydrated from the org profile.
      expect(find.text('Contact information'), findsOneWidget);
      expect(find.text('+966 11 123 4567'), findsOneWidget);
      expect(find.text('info@simf2026.sa'), findsOneWidget);
      expect(find.text('Riyadh Convention Center'), findsOneWidget);
      // The one set social link renders.
      expect(find.text('Social media'), findsOneWidget);
    });

    testWidgets('empty submit shows validation and does not call the API',
        (tester) async {
      final repo = _FakeContactUsRepository();
      await _pump(tester, repo, profile: _profile());

      await tester.tap(find.text('Send'));
      await tester.pumpAndSettle();

      expect(find.text('Name is required'), findsOneWidget);
      expect(find.text('A valid email is required'), findsOneWidget);
      expect(find.text('Message is required'), findsOneWidget);
      expect(repo.lastSubmit, isNull);
    });

    testWidgets('valid submit posts the inquiry and shows the toast',
        (tester) async {
      final repo = _FakeContactUsRepository();
      await _pump(tester, repo, profile: _profile());

      await tester.enterText(find.byType(TextFormField).at(0), 'Sara Ahmed');
      await tester.enterText(
        find.byType(TextFormField).at(1),
        'sara@example.com',
      );
      await tester.enterText(
        find.byType(TextFormField).at(2),
        'I have a question.',
      );
      await tester.tap(find.text('Send'));
      await tester.pumpAndSettle();

      expect(repo.lastSubmit?.name, 'Sara Ahmed');
      expect(repo.lastSubmit?.email, 'sara@example.com');
      expect(repo.lastSubmit?.message, 'I have a question.');
      expect(
        find.textContaining('Your message has been sent'),
        findsOneWidget,
      );
    });
  });
}
