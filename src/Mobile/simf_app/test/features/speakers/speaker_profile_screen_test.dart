import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/features/account/data/profile_repository.dart';
import 'package:simf_app/features/speakers/data/speaker_models.dart';
import 'package:simf_app/features/speakers/data/speakers_repository.dart';
import 'package:simf_app/features/speakers/speaker_profile_screen.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

// The avatar builds the photo URL from the base; the must-override config
// provider throws otherwise. The test HTTP client fails the load → initials.
const _testConfig = SimfDataConfig(
  baseUrl: 'http://test.local/api/v1',
  appKey: 'test',
  deviceType: SimfDeviceType.android,
);

SpeakerDetail _detail({bool allowsMeeting = true, bool allowsData = true}) {
  final start = DateTime.utc(2026, 11, 23, 6);
  return SpeakerDetail(
    id: 'sp1',
    name: 'Capt. Reef',
    nameArabic: 'القبطان ريف',
    rank: 'Sea captain',
    allowsMeetingRequests: allowsMeeting,
    allowsDataSharing: allowsData,
    displayOrder: 0,
    bio: 'A maritime leader.',
    facebookUrl: allowsData ? 'https://fb/x' : null,
    websiteUrl: allowsData ? 'https://reef.example.sa' : null,
    sessions: <SpeakerSession>[
      SpeakerSession(
        id: 'se1',
        code: 'S-1',
        title: 'Opening talk',
        titleArabic: 'حديث',
        hallName: 'Main Hall',
        hallNameArabic: 'الرئيسية',
        startUtc: start,
        endUtc: start.add(const Duration(hours: 1)),
      ),
    ],
  );
}

/// A speaker with all four CV sections, for the tab-order test.
SpeakerDetail _detailAllCv() => const SpeakerDetail(
      id: 'sp1',
      name: 'Capt. Reef',
      nameArabic: 'القبطان ريف',
      rank: 'Sea captain',
      allowsMeetingRequests: false,
      allowsDataSharing: false,
      displayOrder: 0,
      bio: 'Bio body',
      qualifications: 'Quals body',
      trainingExperience: 'Training body',
      awards: 'Awards body',
      sessions: <SpeakerSession>[],
    );

CurrentUser _visitor() => CurrentUser(
      id: 'u1',
      email: 'v@x.sa',
      displayName: 'Visitor One',
      appRole: AppRole.visitor,
      preferredLanguage: PreferredLanguage.fromJson('en'),
      registrationStatus: RegistrationStatus.approved,
    );

Session _session() => Session(
      accessToken: 'A',
      refreshToken: 'R',
      accessTokenExpiresAt: DateTime.now().add(const Duration(minutes: 30)),
      user: _visitor(),
    );

class _SignedIn extends AuthController {
  @override
  AuthState build() => AuthStateSignedIn(_session());
}

class _Guest extends AuthController {
  @override
  AuthState build() => const AuthStateSignedOut();
}

// Two real slots on 2026-07-10 (09:00 + 10:00 local → UTC so toLocal()
// round-trips regardless of the test machine's timezone).
final List<SpeakerSlot> _profileSlots = <SpeakerSlot>[
  SpeakerSlot(
    startUtc: DateTime(2026, 7, 10, 9).toUtc(),
    endUtc: DateTime(2026, 7, 10, 9, 30).toUtc(),
  ),
  SpeakerSlot(
    startUtc: DateTime(2026, 7, 10, 10).toUtc(),
    endUtc: DateTime(2026, 7, 10, 10, 30).toUtc(),
  ),
];

class _FakeRepo implements SpeakersRepository {
  _FakeRepo({this.detail, this.status, this.slots = const <SpeakerSlot>[]});

  final SpeakerDetail? detail;
  final int? status;
  final List<SpeakerSlot> slots;
  int submits = 0;
  DateTime? lastSlotStart;
  String? lastRequesterName;

  @override
  Future<List<SpeakerSummary>> getSpeakers() => throw UnimplementedError();

  @override
  Future<SpeakerDetail> getSpeaker(String id) async {
    if (status != null) {
      throw ApiFailure(
        code: ApiErrorCodes.clientNetwork,
        message: 'x',
        httpStatus: status,
      );
    }
    return detail!;
  }

  @override
  Future<List<SpeakerSlot>> getAvailableSlots(String speakerId) async => slots;

  @override
  Future<void> submitMeetingRequest(
    String speakerId, {
    required String requesterName,
    required String subject,
    DateTime? slotStartUtc,
    DateTime? slotEndUtc,
  }) async {
    submits++;
    lastSlotStart = slotStartUtc;
    lastRequesterName = requesterName;
  }
}

Future<void> _pump(
  WidgetTester tester, {
  required SpeakersRepository repo,
  required AuthController controller,
  bool isVip = false,
  Locale locale = const Locale('en'),
}) async {
  final router = GoRouter(
    initialLocation: '/speakers/sp1',
    routes: <RouteBase>[
      GoRoute(
        path: '/speakers/:speakerId',
        name: RouteNames.speakerProfile,
        builder: (_, state) => SpeakerProfileScreen(
          speakerId: state.pathParameters['speakerId'] ?? '',
        ),
      ),
      GoRoute(
        path: '/sign-in',
        name: RouteNames.signIn,
        builder: (_, __) => const Scaffold(body: Text('SIGN-IN')),
      ),
      GoRoute(
        path: '/sessions/:sessionId',
        name: RouteNames.sessionDetail,
        builder: (_, __) => const Scaffold(body: Text('SESSION')),
      ),
    ],
  );
  await tester.pumpWidget(
    ProviderScope(
      overrides: <Override>[
        simfDataConfigProvider.overrideWithValue(_testConfig),
        speakersRepositoryProvider.overrideWithValue(repo),
        authControllerProvider.overrideWith(() => controller),
        // D-729 — the speaker "request meeting" CTA is VIP-only; gate it here.
        currentUserIsVipProvider.overrideWith((ref) async => isVip),
      ],
      child: MaterialApp.router(
        routerConfig: router,
        locale: locale,
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

void main() {
  group('SpeakerProfileScreen (Page 020)', () {
    testWidgets('renders the header, avatar, CV tabs, bio + sessions',
        (tester) async {
      // Tall surface so the whole lazy ListView (down to the sessions +
      // meeting button) lays out in the test viewport.
      tester.view.physicalSize = const Size(1200, 2600);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      await _pump(tester, repo: _FakeRepo(detail: _detail()), controller: _Guest());
      // The frame's two-line header: white name over the beige rank.
      expect(find.text('Capt. Reef'), findsOneWidget);
      expect(find.text('Sea captain'), findsOneWidget);
      // The active CV tab + the bio body it reveals.
      expect(find.text('Biography'), findsOneWidget);
      expect(find.text('A maritime leader.'), findsOneWidget);
      // The speaker's sessions render. (The meeting CTA is VIP-only, D-729 —
      // covered by the dedicated VIP / non-VIP tests below; a guest never sees it.)
      expect(find.text('Opening talk'), findsOneWidget);
    });

    testWidgets('the CV avatar builds from the SpeakerPhoto asset route',
        (tester) async {
      await _pump(tester, repo: _FakeRepo(detail: _detail()), controller: _Guest());
      final urls = tester
          .widgetList<Image>(find.byType(Image))
          .map((image) => image.image)
          .whereType<NetworkImage>()
          .map((provider) => provider.url)
          .toList();
      expect(
        urls,
        contains('http://test.local/api/v1/app/assets/SpeakerPhoto/sp1/image'),
      );
    });

    testWidgets('a guest does not see the Request meeting CTA (VIP-only, D-729)',
        (tester) async {
      await _pump(tester, repo: _FakeRepo(detail: _detail()), controller: _Guest());
      expect(find.widgetWithText(FilledButton, 'Request meeting'), findsNothing);
    });

    testWidgets(
        'a signed-in NON-VIP visitor does not see the Request meeting CTA '
        '(VIP-only, D-729)', (tester) async {
      await _pump(
        tester,
        repo: _FakeRepo(detail: _detail()),
        controller: _SignedIn(),
        isVip: false,
      );
      expect(find.widgetWithText(FilledButton, 'Request meeting'), findsNothing);
    });

    testWidgets('a signed-in VIP visitor can submit a meeting request',
        (tester) async {
      tester.view.physicalSize = const Size(1200, 2600);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      final repo = _FakeRepo(detail: _detail(), slots: _profileSlots);
      await _pump(tester, repo: repo, controller: _SignedIn(), isVip: true);

      await tester.tap(find.widgetWithText(FilledButton, 'Request meeting'));
      await tester.pumpAndSettle();
      // The sheet has no name field (owner: "no need for name") — only the
      // subject; the requester name comes from the signed-in account.
      expect(find.byType(TextField), findsOneWidget);
      expect(find.text('Subject'), findsOneWidget);
      // A request needs the subject + a picked (real) day + slot.
      await tester.enterText(find.byType(TextField), 'Discuss navigation');
      await tester.tap(find.byKey(const ValueKey<String>('meeting-day-0')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const ValueKey<String>('meeting-time-0')));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Send request'));
      await tester.pumpAndSettle();

      expect(repo.submits, 1);
      expect(repo.lastRequesterName, 'Visitor One');
      // The real slot the visitor picked was sent verbatim (D-709).
      expect(repo.lastSlotStart, DateTime(2026, 7, 10, 9).toUtc());
      expect(find.text('Meeting request sent'), findsOneWidget);
    });

    testWidgets(
        'the meeting sheet presents the speakers REAL slots — day cards when '
        'available, the no-slots notice when the speaker has none (D-709)',
        (tester) async {
      tester.view.physicalSize = const Size(1200, 2600);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);

      // With real slots → the day cards + choose-date/time labels render.
      await _pump(
        tester,
        repo: _FakeRepo(detail: _detail(), slots: _profileSlots),
        controller: _SignedIn(),
        isVip: true,
      );
      await tester.tap(find.widgetWithText(FilledButton, 'Request meeting'));
      await tester.pumpAndSettle();
      expect(find.text('Choose the date'), findsOneWidget);
      expect(
        find.byKey(const ValueKey<String>('meeting-day-0')),
        findsOneWidget,
      );

      // With no slots → the no-slots notice, and no day cards.
      await _pump(
        tester,
        repo: _FakeRepo(detail: _detail()),
        controller: _SignedIn(),
        isVip: true,
      );
      await tester.tap(find.widgetWithText(FilledButton, 'Request meeting'));
      await tester.pumpAndSettle();
      expect(find.text('No meeting slots available right now'), findsOneWidget);
      expect(find.byKey(const ValueKey<String>('meeting-day-0')), findsNothing);
    });

    testWidgets('shows a website chip when the speaker shared a website (D-544)',
        (tester) async {
      tester.view.physicalSize = const Size(1200, 2600);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      await _pump(tester, repo: _FakeRepo(detail: _detail()), controller: _Guest());
      expect(find.widgetWithIcon(ActionChip, Icons.language), findsOneWidget);
    });

    testWidgets('no website chip when the speaker did not share data',
        (tester) async {
      await _pump(
        tester,
        repo: _FakeRepo(detail: _detail(allowsData: false)),
        controller: _Guest(),
      );
      expect(find.widgetWithIcon(ActionChip, Icons.language), findsNothing);
    });

    testWidgets('no meeting button when the speaker opted out', (tester) async {
      // isVip:true so the absence is specifically the speaker opt-out, not the
      // VIP gate (D-729).
      await _pump(
        tester,
        repo: _FakeRepo(detail: _detail(allowsMeeting: false)),
        controller: _SignedIn(),
        isVip: true,
      );
      expect(find.widgetWithText(FilledButton, 'Request meeting'), findsNothing);
    });

    testWidgets('a 404 shows the not-found state', (tester) async {
      await _pump(tester, repo: _FakeRepo(status: 404), controller: _Guest());
      expect(find.text('This speaker was not found'), findsOneWidget);
    });

    // D-436 verification rule: confirm the CV-tab order with a deterministic
    // Arabic-locale position test. Frame 912:2312 places نبذة عنه (Bio, the
    // first section) at the inline-start (RIGHT in RTL) and الجوائز (Awards, the
    // last) at the inline-end (LEFT).
    testWidgets('CV tabs lay out Bio (first) right-most in Arabic',
        (tester) async {
      tester.view.physicalSize = const Size(1200, 2600);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      await _pump(
        tester,
        repo: _FakeRepo(detail: _detailAllCv()),
        controller: _Guest(),
        locale: const Locale('ar'),
      );
      final l10n = AppL10n.of(tester.element(find.byType(SpeakerProfileScreen)));
      final dx = <double>[
        tester.getCenter(find.text(l10n.cvBio)).dx,
        tester.getCenter(find.text(l10n.cvQualifications)).dx,
        tester.getCenter(find.text(l10n.cvTraining)).dx,
        tester.getCenter(find.text(l10n.cvAwards)).dx,
      ];
      // RTL: each subsequent pill (Bio→Quals→Training→Awards) sits further left.
      for (var i = 1; i < dx.length; i++) {
        expect(dx[i], lessThan(dx[i - 1]));
      }
    });
  });
}
