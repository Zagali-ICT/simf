import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/features/account/data/profile_models.dart';
import 'package:simf_app/features/account/data/profile_repository.dart';
import 'package:simf_app/features/staff/data/staff_models.dart';
import 'package:simf_app/features/staff/data/staff_repository.dart';
import 'package:simf_app/features/staff/register_visitor_screen.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// Taps a [SimfPickerField] by its [fieldKey], then picks [label] from the
/// searchable bottom sheet that opens.
Future<void> _pickFromSheet(
  WidgetTester tester,
  String fieldKey,
  String label,
) async {
  final handle = find.byKey(ValueKey<String>(fieldKey));
  await tester.ensureVisible(handle);
  await tester.pumpAndSettle();
  await tester.tap(handle);
  await tester.pumpAndSettle();
  await tester.tap(find.text(label).last);
  await tester.pumpAndSettle();
}

/// Fake lookups — only the three the screen reads; the rest throw.
class _FakeProfileRepo implements ProfileRepository {
  _FakeProfileRepo({this.fail = false});

  final bool fail;

  @override
  Future<List<CountryItem>> getCountries() async {
    if (fail) {
      throw ApiFailure(
          code: ApiErrorCodes.clientNetwork, message: 'x', httpStatus: 500);
    }
    return const <CountryItem>[
      CountryItem(code: 'SA', name: 'Saudi Arabia', nameArabic: 'السعودية'),
      CountryItem(code: 'EG', name: 'Egypt', nameArabic: 'مصر'),
    ];
  }

  @override
  Future<List<ProfileTypeItem>> getProfileTypes({bool? isVisitor}) async =>
      const <ProfileTypeItem>[
        ProfileTypeItem(
            id: 'pt-normal', name: 'Normal', nameArabic: 'عادي', isVisitor: true),
      ];

  @override
  Future<List<OrganisationItem>> searchOrganisations(
          {String? search, int top = 20}) async =>
      const <OrganisationItem>[
        OrganisationItem(id: 'org-1', nameAr: 'أكمي', nameEn: 'Acme'),
      ];

  @override
  dynamic noSuchMethod(Invocation invocation) =>
      throw UnimplementedError(invocation.memberName.toString());
}

class _FakeStaffRepo implements StaffRepository {
  StaffWalkInRequest? lastRequest;
  int registerCalls = 0;

  @override
  Future<StaffWalkInResult> registerVisitor(StaffWalkInRequest request) async {
    registerCalls++;
    lastRequest = request;
    return const StaffWalkInResult(
      userId: 'u1',
      displayName: 'Raed Salem',
      qrId: '',
      profileTypeName: 'Normal',
    );
  }

  @override
  Future<bool> uploadIdImage(
          {required String userId,
          required List<int> bytes,
          required String filename}) async =>
      true;

  @override
  Future<bool> uploadAvatar(
          {required String userId,
          required List<int> bytes,
          required String filename}) async =>
      true;
}

Future<void> _pump(
  WidgetTester tester, {
  required _FakeProfileRepo profile,
  required _FakeStaffRepo staff,
}) async {
  await tester.pumpWidget(
    ProviderScope(
      overrides: <Override>[
        profileRepositoryProvider.overrideWithValue(profile),
        staffRepositoryProvider.overrideWithValue(staff),
      ],
      child: const MaterialApp(
        locale: Locale('en'),
        localizationsDelegates: <LocalizationsDelegate<dynamic>>[
          ...AppL10n.localizationsDelegates,
          GlobalMaterialLocalizations.delegate,
          GlobalWidgetsLocalizations.delegate,
          GlobalCupertinoLocalizations.delegate,
        ],
        supportedLocales: AppL10n.supportedLocales,
        home: StaffRegisterVisitorScreen(),
      ),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  group('StaffRegisterVisitorScreen (D-509, walk-in registration)', () {
    testWidgets('renders the form after the lookups load', (tester) async {
      await _pump(tester, profile: _FakeProfileRepo(), staff: _FakeStaffRepo());
      expect(find.text('Create visitor profile'), findsWidgets);
      expect(find.text('Email'), findsOneWidget);
      expect(find.widgetWithText(FilledButton, 'Next'), findsOneWidget);
    });

    testWidgets('a load failure shows the retry surface', (tester) async {
      await _pump(
        tester,
        profile: _FakeProfileRepo(fail: true),
        staff: _FakeStaffRepo(),
      );
      expect(find.widgetWithText(FilledButton, 'Retry'), findsOneWidget);
    });

    testWidgets('an empty submit is blocked with the complete-fields prompt',
        (tester) async {
      final staff = _FakeStaffRepo();
      await _pump(tester, profile: _FakeProfileRepo(), staff: staff);

      final next = find.widgetWithText(FilledButton, 'Next');
      await tester.ensureVisible(next);
      await tester.pumpAndSettle();
      await tester.tap(next);
      await tester.pump(); // let the SnackBar appear
      // The guard surfaces a SnackBar and does NOT call the API. (The same
      // prompt also renders as the always-present info notice, so assert on the
      // SnackBar + the no-call rather than the shared text.)
      expect(find.byType(SnackBar), findsOneWidget);
      expect(staff.registerCalls, 0);
    });

    testWidgets('every text input caps its length (maxLength set)',
        (tester) async {
      await _pump(tester, profile: _FakeProfileRepo(), staff: _FakeStaffRepo());

      // Saudi default field order: email, phone, arabicName, englishName,
      // nationalId. Each carries a sensible maxLength so over-long input can
      // never reach the server. (maxLength lives on the inner TextField that
      // TextFormField builds.)
      final inputs =
          tester.widgetList<TextField>(find.byType(TextField)).toList();
      expect(inputs, isNotEmpty);
      expect(
        inputs.every((f) => f.maxLength != null),
        isTrue,
        reason: 'every staff register input must declare a maxLength',
      );

      // The email field truncates input beyond its 50-char cap.
      final email = find.byType(TextFormField).at(0);
      await tester.enterText(email, 'a' * 80);
      await tester.pump();
      final emailState = tester.widget<TextField>(find.descendant(
        of: email,
        matching: find.byType(TextField),
      ));
      expect(emailState.controller!.text.length, 50);
    });

    testWidgets('a filled form posts the walk-in registration', (tester) async {
      final staff = _FakeStaffRepo();
      await _pump(tester, profile: _FakeProfileRepo(), staff: staff);

      // Saudi default → fields order: email, phone, arabicName, englishName,
      // nationalId, jobTitle, jobTitleArabic.
      final fields = find.byType(TextFormField);
      await tester.enterText(fields.at(0), 'raed@example.com');
      await tester.enterText(fields.at(1), '0512345678');
      await tester.enterText(fields.at(2), 'رائد سالم');
      await tester.enterText(fields.at(3), 'Raed Salem');
      await tester.enterText(fields.at(4), '1000000008'); // Luhn-valid (D-700)
      await tester.enterText(fields.at(5), 'Engineer'); // jobTitle (required, D-723)
      await tester.enterText(fields.at(6), 'مهندس'); // jobTitleArabic (optional, #37)

      // Pick the nationality (opens bottom sheet) — required for the form.
      await _pickFromSheet(tester, 'nationalityPicker', 'Saudi Arabia');
      // Pick the organisation (opens bottom sheet).
      await _pickFromSheet(tester, 'organisationPicker', 'Acme');

      final next = find.widgetWithText(FilledButton, 'Next');
      await tester.ensureVisible(next);
      await tester.pumpAndSettle();
      await tester.tap(next);
      await tester.pumpAndSettle();

      expect(staff.registerCalls, 1);
      expect(staff.lastRequest?.englishName, 'Raed Salem');
      expect(staff.lastRequest?.isSaudi, isTrue);
      expect(staff.lastRequest?.organisationId, 'org-1');
      expect(staff.lastRequest?.profileTypeId, 'pt-normal');
      expect(staff.lastRequest?.jobTitleArabic, 'مهندس');
      expect(find.textContaining('pending approval'), findsOneWidget);
    });

    testWidgets('D-700 — a national ID with a bad checksum shows the inline '
        'error and never posts', (tester) async {
      final staff = _FakeStaffRepo();
      await _pump(tester, profile: _FakeProfileRepo(), staff: staff);

      // Saudi default order: email, phone, arabicName, englishName, nationalId.
      // 1012345678 has the right shape (^1\d{9}$) but fails the Luhn checksum;
      // the client now rejects it before the server does.
      final fields = find.byType(TextFormField);
      await tester.enterText(fields.at(4), '1012345678');
      await tester.pump();

      expect(
        find.text('Invalid national ID (10 digits starting with 1)'),
        findsOneWidget,
      );
      expect(staff.registerCalls, 0);
    });
  });
}
