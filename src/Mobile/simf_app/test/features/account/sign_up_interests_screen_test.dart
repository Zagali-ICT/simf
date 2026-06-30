import 'dart:typed_data';

import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/features/account/data/profile_models.dart';
import 'package:simf_app/features/account/data/profile_repository.dart';
import 'package:simf_app/features/account/sign_up_interests_screen.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// A fake profile repository for the interests screen (Page 007‑01) — returns
/// canned interests and records the single upsert + image upload, so the
/// pick → save → navigate glue is testable without HTTP. Only the three methods
/// this screen uses are implemented; the rest throw.
class _FakeProfileRepository implements ProfileRepository {
  _FakeProfileRepository({
    this.interests = _canned,
    this.throwOnSave = false,
    this.throwOnUpload = false,
    this.throwOnAvatarUpload = false,
  });

  static const List<InterestItem> _canned = <InterestItem>[
    InterestItem(
      id: 'i1',
      name: 'Naval Defence',
      nameArabic: 'الدفاع البحري',
      displayOrder: 1,
    ),
    InterestItem(
      id: 'i2',
      name: 'AI',
      nameArabic: 'الذكاء الاصطناعي',
      displayOrder: 2,
    ),
  ];

  List<InterestItem> interests;
  bool throwOnSave;
  bool throwOnUpload;
  bool throwOnAvatarUpload;
  UpsertUserProfileRequest? upserted;
  bool uploadCalled = false;
  bool avatarUploadCalled = false;

  @override
  Future<List<InterestItem>> getInterests() async => interests;

  @override
  Future<UserProfileResponse> upsertMyProfile(
    UpsertUserProfileRequest request,
  ) async {
    if (throwOnSave) {
      throw const ApiFailure(code: 'X', message: 'boom');
    }
    upserted = request;
    return const UserProfileResponse(
      interestIds: <String>['i1'],
      arabicName: '',
      englishName: '',
      nationalityCode: '',
      placeOfBirth: '',
      isSaudi: false,
      gender: AppGender.unspecified,
      hasIdImage: false,
      hasAvatar: false,
    );
  }

  @override
  Future<bool> uploadIdImage({
    required List<int> bytes,
    required String filename,
  }) async {
    uploadCalled = true;
    if (throwOnUpload) {
      throw const ApiFailure(code: 'X', message: 'upload-boom');
    }
    return true;
  }

  @override
  Future<bool> uploadAvatar({
    required List<int> bytes,
    required String filename,
  }) async {
    avatarUploadCalled = true;
    if (throwOnAvatarUpload) {
      throw const ApiFailure(code: 'X', message: 'avatar-boom');
    }
    return true;
  }

  @override
  Future<UserProfileResponse> getMyProfile() => throw UnimplementedError();
  @override
  Future<List<CountryItem>> getCountries() => throw UnimplementedError();
  @override
  Future<List<ProfileTypeItem>> getProfileTypes({bool? isVisitor}) =>
      throw UnimplementedError();
  @override
  Future<List<OrganisationItem>> searchOrganisations({
    String? search,
    int top = 20,
  }) =>
      throw UnimplementedError();
}

const SignUpProfileDraft _draft = SignUpProfileDraft(
  request: UpsertUserProfileRequest(
    interestIds: <String>[],
    arabicName: 'راكان السالم',
    englishName: 'Rakan Alsalem',
    nationalityCode: 'SA',
    placeOfBirth: 'Riyadh',
    isSaudi: true,
    gender: AppGender.male,
    nationalId: '1000000008',
    dateOfBirth: '2000-01-31',
  ),
);

Future<void> _pump(
  WidgetTester tester,
  _FakeProfileRepository repo, {
  SignUpProfileDraft? draft = _draft,
}) async {
  final router = GoRouter(
    initialLocation: '/sign-up/interests',
    routes: <RouteBase>[
      GoRoute(
        name: RouteNames.signUpInterests,
        path: '/sign-up/interests',
        builder: (c, s) => SignUpInterestsScreen(draft: draft),
      ),
      GoRoute(
        name: RouteNames.registrationSuccess,
        path: '/registration/success',
        builder: (c, s) => const Scaffold(body: Text('REG-SUCCESS')),
      ),
      GoRoute(
        name: RouteNames.signUpVisitor,
        path: '/sign-up/visitor',
        builder: (c, s) => const Scaffold(body: Text('DATA')),
      ),
    ],
  );

  await tester.pumpWidget(
    ProviderScope(
      overrides: <Override>[
        profileRepositoryProvider.overrideWithValue(repo),
      ],
      child: MaterialApp.router(
        routerConfig: router,
        locale: const Locale('en'),
        supportedLocales: AppL10n.supportedLocales,
        localizationsDelegates: const <LocalizationsDelegate<dynamic>>[
          ...AppL10n.localizationsDelegates,
          GlobalMaterialLocalizations.delegate,
          GlobalWidgetsLocalizations.delegate,
          GlobalCupertinoLocalizations.delegate,
        ],
      ),
    ),
  );
  await tester.pumpAndSettle();
}

bool _saveEnabled(WidgetTester tester) {
  final save = find.widgetWithText(FilledButton, 'Continue');
  return tester.widget<FilledButton>(save).onPressed != null;
}

void main() {
  group('SignUpInterestsScreen (Page 007‑01)', () {
    testWidgets('renders interests + counter; Save disabled until one picked',
        (tester) async {
      await _pump(tester, _FakeProfileRepository());

      expect(find.text('My interests'), findsOneWidget);
      expect(find.text('Naval Defence'), findsOneWidget);
      expect(find.text('0 / 10 selected'), findsOneWidget);
      expect(_saveEnabled(tester), isFalse);

      await tester.tap(find.text('Naval Defence'));
      await tester.pumpAndSettle();

      expect(find.text('1 / 10 selected'), findsOneWidget);
      expect(_saveEnabled(tester), isTrue);
    });

    testWidgets('Save fires ONE upsert carrying the draft data + interestIds, '
        'then navigates to registration success', (tester) async {
      final repo = _FakeProfileRepository();
      await _pump(tester, repo);

      await tester.tap(find.text('Naval Defence'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, 'Continue'));
      await tester.pumpAndSettle();

      expect(repo.upserted, isNotNull);
      expect(repo.upserted!.interestIds, contains('i1'));
      // The carried Page-007 data rides on the same single request.
      expect(repo.upserted!.arabicName, 'راكان السالم');
      expect(repo.upserted!.nationalId, '1000000008');
      expect(find.text('REG-SUCCESS'), findsOneWidget);
    });

    testWidgets('an empty interests lookup shows the empty state',
        (tester) async {
      await _pump(
        tester,
        _FakeProfileRepository(interests: const <InterestItem>[]),
      );

      expect(find.text('No interests available'), findsOneWidget);
      expect(_saveEnabled(tester), isFalse);
    });

    testWidgets('a save failure shows the error and keeps the selection',
        (tester) async {
      final repo = _FakeProfileRepository(throwOnSave: true);
      await _pump(tester, repo);

      await tester.tap(find.text('Naval Defence'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, 'Continue'));
      await tester.pumpAndSettle();

      expect(find.text('boom'), findsOneWidget);
      expect(find.text('REG-SUCCESS'), findsNothing);
      expect(find.text('1 / 10 selected'), findsOneWidget);
    });

    testWidgets('a male whose photo upload fails is blocked — photo is tried '
        'first and the profile is NOT saved without it (D-431)', (tester) async {
      final repo = _FakeProfileRepository(throwOnUpload: true);
      final draftWithImage = SignUpProfileDraft(
        request: _draft.request,
        idImageBytes: Uint8List.fromList(<int>[1, 2, 3]),
        idImageName: 'id.jpg',
      );
      await _pump(tester, repo, draft: draftWithImage);

      await tester.tap(find.text('Naval Defence'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, 'Continue'));
      await tester.pumpAndSettle();

      // The photo is uploaded BEFORE the profile save, and a male whose upload
      // failed never reaches the save → no incomplete profile, no success route.
      expect(repo.uploadCalled, isTrue);
      expect(repo.upserted, isNull);
      expect(find.text('REG-SUCCESS'), findsNothing);
      expect(find.text('1 / 10 selected'), findsOneWidget);
    });

    testWidgets('a draft with both photos uploads the ID document AND the face '
        '(avatar) before the save (two-photo split)', (tester) async {
      final repo = _FakeProfileRepository();
      final draftWithBoth = SignUpProfileDraft(
        request: _draft.request,
        idImageBytes: Uint8List.fromList(<int>[1, 2, 3]),
        idImageName: 'id.jpg',
        faceImageBytes: Uint8List.fromList(<int>[4, 5, 6]),
        faceImageName: 'face.jpg',
      );
      await _pump(tester, repo, draft: draftWithBoth);

      await tester.tap(find.text('Naval Defence'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, 'Continue'));
      await tester.pumpAndSettle();

      expect(repo.uploadCalled, isTrue); // ID document
      expect(repo.avatarUploadCalled, isTrue); // face photo
      expect(repo.upserted, isNotNull);
      expect(find.text('REG-SUCCESS'), findsOneWidget);
    });

    testWidgets('a male whose FACE upload fails is blocked — not saved without '
        'the face photo (two-photo split)', (tester) async {
      final repo = _FakeProfileRepository(throwOnAvatarUpload: true);
      final draftWithBoth = SignUpProfileDraft(
        request: _draft.request, // male
        idImageBytes: Uint8List.fromList(<int>[1, 2, 3]),
        idImageName: 'id.jpg',
        faceImageBytes: Uint8List.fromList(<int>[4, 5, 6]),
        faceImageName: 'face.jpg',
      );
      await _pump(tester, repo, draft: draftWithBoth);

      await tester.tap(find.text('Naval Defence'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, 'Continue'));
      await tester.pumpAndSettle();

      expect(repo.uploadCalled, isTrue);
      expect(repo.avatarUploadCalled, isTrue);
      expect(repo.upserted, isNull); // male — a failed face upload blocks the save
      expect(find.text('REG-SUCCESS'), findsNothing);
    });

    testWidgets('a direct open with no draft shows the recover state',
        (tester) async {
      await _pump(tester, _FakeProfileRepository(), draft: null);

      final recover = find.byType(FilledButton);
      expect(recover, findsOneWidget);
      await tester.tap(recover);
      await tester.pumpAndSettle();

      expect(find.text('DATA'), findsOneWidget);
    });
  });
}
