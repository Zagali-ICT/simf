import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
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
    countryNameEn: 'Saudi Arabia',
  ),
  SpeakerSummary(
    id: 'sp2',
    name: 'Dr Wave',
    nameArabic: 'د. موجة',
    displayOrder: 1,
  ),
];

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
  Future<void> submitMeetingRequest(
    String speakerId, {
    required String requesterName,
    required String subject,
  }) =>
      throw UnimplementedError();
}

Future<void> _pump(WidgetTester tester, {required SpeakersRepository repo}) async {
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
        speakersRepositoryProvider.overrideWithValue(repo),
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

void main() {
  group('SpeakersScreen (Page 019)', () {
    testWidgets('renders a card per speaker', (tester) async {
      await _pump(tester, repo: _FakeSpeakersRepo(list: _speakers));
      expect(find.text('Capt. Reef'), findsOneWidget);
      expect(find.text('Dr Wave'), findsOneWidget);
      expect(find.textContaining('Sea captain'), findsOneWidget);
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
