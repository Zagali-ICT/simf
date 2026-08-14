import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/widgets/simf_language_toggle.dart';
import 'package:simf_app/core/widgets/simf_field_style.dart';
import 'package:simf_app/core/widgets/simf_image_source_sheet.dart';
import 'package:simf_app/core/widgets/simf_picker_field.dart';
import 'package:simf_app/features/account/data/profile_models.dart';
import 'package:simf_app/features/account/data/profile_repository.dart';
import 'package:simf_app/features/staff/data/staff_models.dart';
import 'package:simf_app/features/staff/data/staff_repository.dart';
import 'package:simf_app/features/staff/register_visitor_screen.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// Fake lookups — only the three the screen reads; the rest throw.
class _FakeProfileRepo implements ProfileRepository {
  _FakeProfileRepo({this.fail = false, this.profileTypes});

  final bool fail;

  /// Null = the usual two seeded tiers; an empty list drives DEF-STF-007.
  final List<ProfileTypeItem>? profileTypes;

  @override
  Future<List<CountryItem>> getCountries() async {
    if (fail) {
      throw const ApiFailure(
          code: ApiErrorCodes.clientNetwork, message: 'x', httpStatus: 500,);
    }
    return const <CountryItem>[
      CountryItem(code: 'SA', name: 'Saudi Arabia', nameArabic: 'السعودية'),
      CountryItem(code: 'EG', name: 'Egypt', nameArabic: 'مصر'),
    ];
  }

  @override
  Future<List<ProfileTypeItem>> getProfileTypes({bool? isVisitor}) async =>
      profileTypes ??
      const <ProfileTypeItem>[
        ProfileTypeItem(
            id: 'pt-normal', name: 'Normal', nameArabic: 'عادي', isVisitor: true,),
        ProfileTypeItem(
            id: 'pt-vip', name: 'VIP', nameArabic: 'كبار الزوار', isVisitor: true,),
      ];

  @override
  Future<List<OrganisationItem>> searchOrganisations(
          {String? search, int top = 20,}) async =>
      const <OrganisationItem>[
        OrganisationItem(id: 'org-1', nameAr: 'أكمي', nameEn: 'Acme'),
      ];

  @override
  dynamic noSuchMethod(Invocation invocation) =>
      throw UnimplementedError(invocation.memberName.toString());
}

class _FakeStaffRepo implements StaffRepository {
  _FakeStaffRepo({this.registerFailure, this.idUploadFailures = 0});

  /// When set, `registerVisitor` throws it instead of creating the visitor.
  final ApiFailure? registerFailure;

  /// How many times the ID-document upload fails before it succeeds — drives
  /// the DEF-STF-004 retry path.
  final int idUploadFailures;

  StaffWalkInRequest? lastRequest;
  int registerCalls = 0;
  int idUploadCalls = 0;
  int avatarUploadCalls = 0;

  @override
  Future<StaffWalkInResult> registerVisitor(StaffWalkInRequest request) async {
    registerCalls++;
    lastRequest = request;
    final failure = registerFailure;
    if (failure != null) {
      throw failure;
    }
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
      required String filename,}) async {
    idUploadCalls++;
    if (idUploadCalls <= idUploadFailures) {
      throw const ApiFailure(
        code: ApiErrorCodes.clientNetwork,
        message: 'upload failed',
        httpStatus: 500,
      );
    }
    return true;
  }

  @override
  Future<bool> uploadAvatar(
      {required String userId,
      required List<int> bytes,
      required String filename,}) async {
    avatarUploadCalls++;
    return true;
  }
}

Future<void> _pump(
  WidgetTester tester, {
  required _FakeProfileRepo profile,
  required _FakeStaffRepo staff,
  String language = 'en',
}) async {
  await tester.pumpWidget(
    ProviderScope(
      overrides: <Override>[
        profileRepositoryProvider.overrideWithValue(profile),
        staffRepositoryProvider.overrideWithValue(staff),
      ],
      child: MaterialApp(
        locale: Locale(language),
        localizationsDelegates: const <LocalizationsDelegate<dynamic>>[
          ...AppL10n.localizationsDelegates,
          GlobalMaterialLocalizations.delegate,
          GlobalWidgetsLocalizations.delegate,
          GlobalCupertinoLocalizations.delegate,
        ],
        supportedLocales: AppL10n.supportedLocales,
        home: const StaffRegisterVisitorScreen(),
      ),
    ),
  );
  await tester.pumpAndSettle();
}

/// A valid 1x1 transparent PNG — the attachment preview decodes it for real.
const List<int> _onePixelPng = <int>[
  0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, //
  0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
  0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
  0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
  0x89, 0x00, 0x00, 0x00, 0x0A, 0x49, 0x44, 0x41,
  0x54, 0x78, 0x9C, 0x63, 0x00, 0x01, 0x00, 0x00,
  0x05, 0x00, 0x01, 0x0D, 0x0A, 0x2D, 0xB4, 0x00,
  0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE,
  0x42, 0x60, 0x82,
];

/// Stands in for the OS image picker: every `pickImage` call resolves to a real
/// on-disk temp file so `XFile.readAsBytes()` yields bytes and the screen ends
/// up with an attachment to upload.
void _mockImagePicker(WidgetTester tester) {
  final file = File(
    '${Directory.systemTemp.createTempSync('simf_staff').path}/id.png',
  )..writeAsBytesSync(_onePixelPng);
  const channel = MethodChannel('plugins.flutter.io/image_picker');
  tester.binding.defaultBinaryMessenger.setMockMethodCallHandler(
    channel,
    (call) async => call.method == 'pickImage' ? file.path : null,
  );
  addTearDown(
    () => tester.binding.defaultBinaryMessenger
        .setMockMethodCallHandler(channel, null),
  );
}

/// Attaches the ID document through the real picker flow (source sheet →
/// picker), so the screen holds bytes to upload.
Future<void> _attachIdDocument(WidgetTester tester) async {
  final attach = find.text('Attach file');
  await tester.ensureVisible(attach);
  await tester.pumpAndSettle();
  await tester.tap(attach);
  await tester.pumpAndSettle();
  await tester.tap(find.byKey(const ValueKey<String>('imageSource_camera')));
  // Close the sheet so the screen actually calls the picker, then alternate
  // pumps with real-clock slices: the picked XFile is read with REAL file I/O,
  // which cannot complete inside the binding's fake async.
  await tester.pumpAndSettle();
  for (var attempt = 0; attempt < 20; attempt++) {
    await tester.runAsync(
      () => Future<void>.delayed(const Duration(milliseconds: 20)),
    );
    await tester.pumpAndSettle();
    if (find.text('Remove').evaluate().isNotEmpty) {
      return;
    }
  }
  fail('the picked ID document never reached the form');
}

/// Fills every required field on the Saudi-default form (order: arabicName,
/// englishName, nationalId, jobTitle, jobTitleArabic, email, phone) and picks
/// the organisation, leaving the form ready to submit.
Future<void> _fillSaudiForm(WidgetTester tester) async {
  final fields = find.byType(TextFormField);
  await tester.enterText(fields.at(0), 'رائد سالم');
  await tester.enterText(fields.at(1), 'Raed Salem');
  await tester.enterText(fields.at(2), '1000000008'); // Luhn-valid (D-700)
  await tester.enterText(fields.at(3), 'Engineer');
  await tester.enterText(fields.at(6), '0512345678');
  await _pickFromSheet(tester, 'staffOrganisationPicker', 'Acme');
}

/// Opens a [SimfPickerField] by its stable [fieldKey] and taps [optionLabel] in
/// the shared searchable sheet.
Future<void> _pickFromSheet(
  WidgetTester tester,
  String fieldKey,
  String optionLabel,
) async {
  final field = find.byKey(ValueKey<String>(fieldKey));
  await tester.ensureVisible(field);
  await tester.pumpAndSettle();
  await tester.tap(field);
  await tester.pumpAndSettle();
  await tester.tap(find.text(optionLabel).last);
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

      // Saudi default field order: arabicName, englishName, nationalId,
      // jobTitle, jobTitleArabic, email, phone. Each carries a sensible
      // maxLength so over-long input can never reach the server. (maxLength
      // lives on the inner TextField that TextFormField builds.)
      final inputs =
          tester.widgetList<TextField>(find.byType(TextField)).toList();
      expect(inputs, isNotEmpty);
      expect(
        inputs.every((f) => f.maxLength != null),
        isTrue,
        reason: 'every staff register input must declare a maxLength',
      );

      // The email field truncates input beyond its 50-char cap.
      final email = find.byType(TextFormField).at(5);
      await tester.enterText(email, 'a' * 80);
      await tester.pump();
      final emailState = tester.widget<TextField>(find.descendant(
        of: email,
        matching: find.byType(TextField),
      ),);
      expect(emailState.controller!.text.length, 50);
    });

    testWidgets('a filled form posts the walk-in registration', (tester) async {
      final staff = _FakeStaffRepo();
      await _pump(tester, profile: _FakeProfileRepo(), staff: staff);

      // Saudi default → field order: arabicName, englishName, nationalId,
      // jobTitle, jobTitleArabic, email, phone.
      final fields = find.byType(TextFormField);
      await tester.enterText(fields.at(0), 'رائد سالم');
      await tester.enterText(fields.at(1), 'Raed Salem');
      await tester.enterText(fields.at(2), '1000000008'); // Luhn-valid (D-700)
      await tester.enterText(fields.at(3), 'Engineer'); // jobTitle (D-723)
      await tester.enterText(fields.at(4), 'مهندس'); // jobTitleArabic (#37)
      await tester.enterText(fields.at(5), 'raed@example.com');
      await tester.enterText(fields.at(6), '0512345678');

      // BUG-019 / 19j — the lookups are the shared searchable picker now.
      await _pickFromSheet(tester, 'staffOrganisationPicker', 'Acme');

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

      // Saudi default order: arabicName, englishName, nationalId.
      // 1012345678 has the right shape (^1\d{9}$) but fails the Luhn checksum;
      // the client now rejects it before the server does.
      final fields = find.byType(TextFormField);
      await tester.enterText(fields.at(2), '1012345678');
      await tester.pump();

      // BUG-019 / 19m — the message names the check digit, because the
      // validator applies Luhn on top of the "10 digits starting with 1" shape.
      expect(
        find.text(
          'Invalid national ID (10 digits starting with 1, with a valid check '
          'digit)',
        ),
        findsOneWidget,
      );
      expect(staff.registerCalls, 0);
    });
  });

  group('StaffRegisterVisitorScreen — BUG-019 design-system rebuild', () {
    testWidgets('19i — the inputs use the shared simfFieldDecoration '
        '(unfilled), not a page-local filled copy', (tester) async {
      await _pump(tester, profile: _FakeProfileRepo(), staff: _FakeStaffRepo());

      final decorations = tester
          .widgetList<TextField>(find.byType(TextField))
          .map((f) => f.decoration)
          .toList();
      expect(decorations, isNotEmpty);
      // The removed local `_decoration()` was `filled: true` with a white
      // fillColor — the wrong background the owner reported.
      expect(
        decorations.every((d) => d?.filled == false),
        isTrue,
        reason: 'every input must come from simfFieldDecoration (filled: false)',
      );
      // Sanity-check the shared decoration really is unfilled.
      expect(simfFieldDecoration().filled, isFalse);
    });

    testWidgets('19j — every lookup is a SimfPickerField, no raw dropdown',
        (tester) async {
      await _pump(tester, profile: _FakeProfileRepo(), staff: _FakeStaffRepo());

      expect(find.byType(DropdownButtonFormField<String>), findsNothing);
      // Profile type (19g), nationality and organisation.
      expect(find.byType(SimfPickerField), findsNWidgets(3));
      expect(
        find.byKey(const ValueKey<String>('staffProfileTypePicker')),
        findsOneWidget,
      );
      expect(
        find.byKey(const ValueKey<String>('staffNationalityPicker')),
        findsOneWidget,
      );
      expect(
        find.byKey(const ValueKey<String>('staffOrganisationPicker')),
        findsOneWidget,
      );
    });

    testWidgets('19a — the language control is the shared SimfLanguageToggle',
        (tester) async {
      await _pump(tester, profile: _FakeProfileRepo(), staff: _FakeStaffRepo());

      expect(find.byType(SimfLanguageToggle), findsOneWidget);
      // The page-local globe IconButton is gone.
      expect(find.byIcon(Icons.language), findsNothing);
    });

    testWidgets('19g — the operator can change the visitor classification',
        (tester) async {
      final staff = _FakeStaffRepo();
      await _pump(tester, profile: _FakeProfileRepo(), staff: staff);

      // Seeded to the "Normal" tier, then re-picked by the operator.
      expect(find.text('Normal'), findsOneWidget);
      await _pickFromSheet(tester, 'staffProfileTypePicker', 'VIP');
      expect(find.text('VIP'), findsOneWidget);

      final fields = find.byType(TextFormField);
      await tester.enterText(fields.at(0), 'رائد سالم');
      await tester.enterText(fields.at(1), 'Raed Salem');
      await tester.enterText(fields.at(2), '1000000008');
      await tester.enterText(fields.at(3), 'Engineer');
      await tester.enterText(fields.at(6), '0512345678');
      await _pickFromSheet(tester, 'staffOrganisationPicker', 'Acme');

      final next = find.widgetWithText(FilledButton, 'Next');
      await tester.ensureVisible(next);
      await tester.pumpAndSettle();
      await tester.tap(next);
      await tester.pumpAndSettle();

      expect(staff.lastRequest?.profileTypeId, 'pt-vip');
    });

    testWidgets('19l — a pristine submit reveals every field error AND scrolls '
        'the first one into view', (tester) async {
      tester.view.physicalSize = const Size(400, 800);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(tester.view.reset);

      await _pump(tester, profile: _FakeProfileRepo(), staff: _FakeStaffRepo());

      final next = find.widgetWithText(FilledButton, 'Next');
      await tester.ensureVisible(next);
      await tester.pumpAndSettle();
      await tester.tap(next);
      await tester.pumpAndSettle();

      final errors = find.text('This field is required');
      expect(errors, findsWidgets);

      // The regression the owner reported: the errors rendered, but the CTA is
      // at the BOTTOM of a long form so every one of them was off-screen above.
      // The submit now brings the first problem (the Arabic name) into view.
      final viewport = tester.getRect(find.byType(StaffRegisterVisitorScreen));
      final visible = errors.evaluate().where((element) {
        final rect = tester.getRect(find.byWidget(element.widget));
        return rect.top >= viewport.top && rect.bottom <= viewport.bottom;
      });
      expect(
        visible, isNotEmpty,
        reason: 'at least the first invalid field must be on screen',
      );
    });

    testWidgets('19c — the phone card builds in Arabic with no overflow',
        (tester) async {
      tester.view.physicalSize = const Size(400, 900);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(tester.view.reset);

      await _pump(
        tester,
        profile: _FakeProfileRepo(),
        staff: _FakeStaffRepo(),
        language: 'ar',
      );
      // A RenderFlex overflow throws in the test binding, so reaching here with
      // the card rendered is the assertion. Confirm the Arabic copy landed.
      expect(find.text('إنشاء ملف زائر'), findsWidgets);
      expect(tester.takeException(), isNull);
    });

    testWidgets('19c — the tablet card builds two columns with no overflow',
        (tester) async {
      tester.view.physicalSize = const Size(1024, 1314);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(tester.view.reset);

      await _pump(
        tester,
        profile: _FakeProfileRepo(),
        staff: _FakeStaffRepo(),
        language: 'ar',
      );
      expect(tester.takeException(), isNull);
      // Two-column: the Arabic and English name fields share a row.
      final arabic = tester.getRect(find.byType(TextFormField).at(0));
      final english = tester.getRect(find.byType(TextFormField).at(1));
      expect(arabic.top, english.top);
    });

    testWidgets('19f — an attachment offers the camera as well as a file',
        (tester) async {
      await _pump(tester, profile: _FakeProfileRepo(), staff: _FakeStaffRepo());

      final attach = find.text('Attach file');
      await tester.ensureVisible(attach);
      await tester.pumpAndSettle();
      await tester.tap(attach);
      await tester.pumpAndSettle();

      // The walk-in desk must be able to shoot the document on the spot; the
      // screen used to open the photo gallery with no camera path at all.
      expect(find.byType(SimfImageSourceSheet), findsOneWidget);
      expect(find.text('Take a photo'), findsOneWidget);
      expect(find.text('Choose a file'), findsOneWidget);
      expect(
        find.byKey(const ValueKey<String>('imageSource_camera')),
        findsOneWidget,
      );
    });

    testWidgets('19k — the attachment captions are one line', (tester) async {
      await _pump(tester, profile: _FakeProfileRepo(), staff: _FakeStaffRepo());

      // The old caption "Attachments (ID / Iqama / passport image)" wrapped to
      // two lines next to every one-line sibling; the detail is a hint now.
      expect(find.text('ID document'), findsOneWidget);
      expect(find.text('National ID, Iqama or passport'), findsOneWidget);
      expect(find.text('Personal photo'), findsOneWidget);
    });
  });

  group('StaffRegisterVisitorScreen — deferred walk-in defects', () {
    testWidgets("DEF-STF-003 — the name inputs cap at the server's 50, not "
        '100', (tester) async {
      await _pump(tester, profile: _FakeProfileRepo(), staff: _FakeStaffRepo());

      // UserProfile.Name / NameArabic are nvarchar(50) and
      // AdminWalkInRegistrationRequestValidator caps both at 50. The inputs used
      // to accept 100, so a long name round-tripped into a 400.
      final fields = find.byType(TextFormField);
      await tester.enterText(fields.at(0), 'ب' * 80);
      await tester.enterText(fields.at(1), 'B' * 80);
      await tester.pump();

      final arabic = tester.widget<TextField>(
        find.descendant(of: fields.at(0), matching: find.byType(TextField)),
      );
      final english = tester.widget<TextField>(
        find.descendant(of: fields.at(1), matching: find.byType(TextField)),
      );
      expect(arabic.maxLength, 50);
      expect(english.maxLength, 50);
      expect(arabic.controller!.text.length, 50);
      expect(english.controller!.text.length, 50);
    });

    testWidgets('DEF-STF-003 — a server field rejection paints ON the field, '
        'and clears when it is edited', (tester) async {
      final staff = _FakeStaffRepo(
        registerFailure: const ApiFailure(
          code: 'VALIDATION_FAILED',
          message: 'One or more fields are invalid.',
          httpStatus: 400,
          details: <ApiResultErrorDetail>[
            ApiResultErrorDetail(
              field: 'EnglishName',
              message: 'English name must be at most 50 characters.',
              messageArabic: 'يجب ألا يتجاوز الاسم بالإنجليزية 50 حرفًا.',
            ),
          ],
        ),
      );
      await _pump(tester, profile: _FakeProfileRepo(), staff: staff);
      await _fillSaudiForm(tester);

      final next = find.widgetWithText(FilledButton, 'Next');
      await tester.ensureVisible(next);
      await tester.pumpAndSettle();
      await tester.tap(next);
      await tester.pumpAndSettle();

      // Before the fix the 400 was a bare toast with no field highlighted.
      expect(
        find.text('English name must be at most 50 characters.'),
        findsOneWidget,
      );

      // Correcting the field drops the server message without another round-trip.
      await tester.enterText(find.byType(TextFormField).at(1), 'Raed S');
      await tester.pumpAndSettle();
      expect(
        find.text('English name must be at most 50 characters.'),
        findsNothing,
      );
    });

    testWidgets('DEF-STF-004 — a failed attachment upload is surfaced and can '
        'be retried WITHOUT registering the visitor again', (tester) async {
      _mockImagePicker(tester);
      final staff = _FakeStaffRepo(idUploadFailures: 1);
      await _pump(tester, profile: _FakeProfileRepo(), staff: staff);
      await _attachIdDocument(tester);
      await _fillSaudiForm(tester);

      final next = find.widgetWithText(FilledButton, 'Next');
      await tester.ensureVisible(next);
      await tester.pumpAndSettle();
      await tester.tap(next);
      await tester.pumpAndSettle();

      // The upload failure used to be swallowed by an empty catch, so the
      // operator was told everything succeeded while the document was missing.
      expect(
        find.text('Visitor registered — attachments not uploaded'),
        findsOneWidget,
      );
      expect(find.text('ID document'), findsWidgets);
      expect(staff.registerCalls, 1);
      expect(staff.idUploadCalls, 1);

      await tester.tap(find.byKey(const ValueKey<String>('staffUploadRetry')));
      await tester.pumpAndSettle();

      // The retry re-sends the FILE against the same visitor; the person is
      // never registered a second time.
      expect(staff.idUploadCalls, 2);
      expect(staff.registerCalls, 1);
      expect(find.text('The attachments were uploaded.'), findsOneWidget);
    });

    testWidgets('DEF-STF-007 — an empty classification lookup explains itself '
        'instead of silently blocking submit', (tester) async {
      final staff = _FakeStaffRepo();
      await _pump(
        tester,
        profile: _FakeProfileRepo(profileTypes: const <ProfileTypeItem>[]),
        staff: staff,
      );

      // The field says there is nothing to pick and what to do about it, and
      // the picker is inert (an empty sheet would waste the operator's time).
      expect(
        find.text('Could not load the visitor classification.'),
        findsOneWidget,
      );
      expect(
        find.textContaining('No active visitor classifications exist'),
        findsOneWidget,
      );
      // SimfPickerField carries its fieldKey on the tappable InkWell.
      final picker = tester.widget<InkWell>(
        find.byKey(const ValueKey<String>('staffProfileTypePicker')),
      );
      expect(picker.onTap, isNull);
      expect(staff.registerCalls, 0);
    });
  });
}
