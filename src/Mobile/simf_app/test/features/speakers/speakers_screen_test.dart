import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/widgets/simf_svg_icon.dart';
import 'package:simf_app/features/speakers/data/speaker_models.dart';
import 'package:simf_app/features/speakers/data/speakers_repository.dart';
import 'package:simf_app/features/speakers/speakers_screen.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

const _speakers = <SpeakerSummary>[
  SpeakerSummary(
    id: 'sp1',
    name: 'Capt. Reef',
    nameArabic: 'القبطان ريف',
    displayOrder: 0,
    rank: 'Sea captain',
    countryId: 682, // Saudi Arabia → 🇸🇦 flag badge on the avatar corner
    countryNameEn: 'RSNF',
  ),
  SpeakerSummary(
    id: 'sp2',
    name: 'Dr Wave',
    nameArabic: 'د. موجة',
    displayOrder: 1,
  ),
  // A host row — detected by the affiliation text carrying the host word
  // ("Host" in EN / "المضيف" in AR), the only signal on the public summary.
  SpeakerSummary(
    id: 'sp3',
    name: 'Brig. Anchor',
    nameArabic: 'العميد مرساة',
    displayOrder: 2,
    rank: 'Brigadier · Host',
  ),
];

// The avatar builds the photo URL from the base; the must-override config
// provider throws otherwise. The test HTTP client fails network-image loads, so
// each avatar shows its anchor fall-back.
const _testConfig = SimfDataConfig(
  baseUrl: 'http://test.local/api/v1',
  appKey: 'test',
  deviceType: SimfDeviceType.android,
);

class _FakeSpeakersRepo implements SpeakersRepository {
  _FakeSpeakersRepo({this.list = const <SpeakerSummary>[], this.fail = false});

  final List<SpeakerSummary> list;
  final bool fail;
  int calls = 0;

  @override
  Future<List<SpeakerSummary>> getSpeakers() async {
    calls++;
    if (fail) {
      throw const ApiFailure(code: ApiErrorCodes.clientNetwork, message: 'x');
    }
    return list;
  }

  @override
  Future<SpeakerDetail> getSpeaker(String id) => throw UnimplementedError();

  @override
  Future<List<SpeakerSlot>> getAvailableSlots(String speakerId) =>
      throw UnimplementedError();

  @override
  Future<void> submitMeetingRequest(
    String speakerId, {
    required String requesterName,
    required String subject,
    DateTime? slotStartUtc,
    DateTime? slotEndUtc,
  }) =>
      throw UnimplementedError();
}

Future<void> _pump(
  WidgetTester tester, {
  required SpeakersRepository repo,
  Locale locale = const Locale('en'),
}) async {
  final router = GoRouter(
    initialLocation: '/speakers',
    routes: <RouteBase>[
      GoRoute(
        path: '/speakers',
        name: RouteNames.speakers,
        builder: (_, __) => const SpeakersScreen(),
      ),
      GoRoute(
        path: '/speakers/:speakerId',
        name: RouteNames.speakerProfile,
        builder: (_, state) =>
            Scaffold(body: Text('PROFILE ${state.pathParameters['speakerId']}')),
      ),
    ],
  );
  await tester.pumpWidget(
    ProviderScope(
      overrides: <Override>[
        simfDataConfigProvider.overrideWithValue(_testConfig),
        speakersRepositoryProvider.overrideWithValue(repo),
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
  group('SpeakersScreen (Page 019)', () {
    testWidgets('renders the header title and a card per speaker',
        (tester) async {
      await _pump(tester, repo: _FakeSpeakersRepo(list: _speakers));
      expect(find.text('Speakers'), findsOneWidget);
      expect(find.text('Capt. Reef'), findsOneWidget);
      expect(find.text('Dr Wave'), findsOneWidget);
      expect(find.text('Brig. Anchor'), findsOneWidget);
      // The sub-line is now the rank only — the country shows as a flag badge on
      // the avatar's top-left corner (D-453 follow-up), not as text.
      expect(find.text('Sea captain'), findsOneWidget);
      expect(find.text('Sea captain · RSNF'), findsNothing);
      // The country flag badge (sp1 = Saudi Arabia, countryId 682 → 🇸🇦).
      expect(find.text('\u{1F1F8}\u{1F1E6}'), findsOneWidget);
    });

    testWidgets('each avatar falls back to the anchor when there is no photo — '
        'the host star is per-session (shown on the detail), not here (D-432)',
        (tester) async {
      await _pump(tester, repo: _FakeSpeakersRepo(list: _speakers));
      // No photo uploaded (the test image load fails) → every tile shows its
      // anchor fall-back; host is contextual to a session, so never a star here.
      expect(find.byIcon(Icons.anchor), findsNWidgets(3));
      expect(find.byIcon(Icons.star_border), findsNothing);
    });

    testWidgets('each card builds its avatar from the SpeakerPhoto asset route',
        (tester) async {
      await _pump(tester, repo: _FakeSpeakersRepo(list: _speakers));
      final urls = tester
          .widgetList<Image>(find.byType(Image))
          .map((image) => image.image)
          .whereType<NetworkImage>()
          .map((provider) => provider.url)
          .toList();
      for (final id in <String>['sp1', 'sp2', 'sp3']) {
        expect(
          urls,
          contains('http://test.local/api/v1/app/assets/SpeakerPhoto/$id/image'),
        );
      }
    });

    testWidgets('RTL card matches Figma 908:1744 — the gold anchor tile sits on '
        'the RIGHT (next to the name), the navigation caret on the LEFT',
        (tester) async {
      await _pump(
        tester,
        repo: _FakeSpeakersRepo(list: _speakers),
        locale: const Locale('ar'),
      );
      // Reference everything to the FIRST card (sp1 = القبطان ريف) so the
      // comparison is unambiguous (not a header/nav SVG).
      final nameDx = tester.getCenter(find.text('القبطان ريف')).dx;
      final anchorDx = tester.getCenter(find.byIcon(Icons.anchor).first).dx;
      // The caret SVG that shares the first card's row (same dy as the name).
      final nameDy = tester.getCenter(find.text('القبطان ريف')).dy;
      final caretDx = tester
          .getCenter(find.byWidgetPredicate((w) =>
              w is SimfSvgIcon && w.asset.contains('caret')).first)
          .dx;
      final caretDy = tester
          .getCenter(find.byWidgetPredicate((w) =>
              w is SimfSvgIcon && w.asset.contains('caret')).first)
          .dy;
      // Figma (Arabic/RTL frame 908:1744): the gold anchor tile is the right-
      // most element (right of the name), the caret is the left-most.
      expect(anchorDx, greaterThan(nameDx),
          reason: 'anchor tile must be right of the name (Figma 908:1744)');
      expect(caretDx, lessThan(nameDx),
          reason: 'caret must be left of the name (Figma 908:1744)');
      expect((caretDy - nameDy).abs(), lessThan(60),
          reason: 'caret shares the first card row');
    });

    testWidgets('tapping a card opens the profile', (tester) async {
      await _pump(tester, repo: _FakeSpeakersRepo(list: _speakers));
      await tester.tap(find.text('Capt. Reef'));
      await tester.pumpAndSettle();
      expect(find.text('PROFILE sp1'), findsOneWidget);
    });

    testWidgets('empty list shows the empty state', (tester) async {
      await _pump(tester, repo: _FakeSpeakersRepo());
      expect(find.text('No speakers'), findsOneWidget);
    });

    testWidgets('error shows retry, which re-fetches', (tester) async {
      final repo = _FakeSpeakersRepo(fail: true);
      await _pump(tester, repo: repo);
      expect(find.text('Could not load the speakers.'), findsOneWidget);
      await tester.tap(find.widgetWithText(FilledButton, 'Retry'));
      await tester.pumpAndSettle();
      expect(repo.calls, greaterThanOrEqualTo(2));
    });
  });
}
