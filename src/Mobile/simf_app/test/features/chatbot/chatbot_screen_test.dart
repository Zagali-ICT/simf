import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/features/chatbot/chatbot_screen.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

class _FakeResponder implements ChatbotResponder {
  _FakeResponder(this.answer);

  final String answer;
  String? lastPrompt;

  @override
  Future<String> reply(String prompt, {required bool isArabic}) async {
    lastPrompt = prompt;
    return answer;
  }
}

/// Simulates a wire error so the screen shows its error bubble (never network).
class _ThrowingResponder implements ChatbotResponder {
  @override
  Future<String> reply(String prompt, {required bool isArabic}) async {
    throw const ApiFailure(code: 'AI_PROVIDER_FAILED', message: 'boom');
  }
}

/// Throws a NON-ApiFailure (mirrors the 401-refresh / keystore path). The screen
/// must still recover — clear the sending state and show the error bubble.
class _StateErrorResponder implements ChatbotResponder {
  @override
  Future<String> reply(String prompt, {required bool isArabic}) async {
    throw StateError('unexpected');
  }
}

// SimfPageShell renders the bottom nav + the المزيد drawer, which read the data
// config; the destinations are stubbed so the shell builds.
const _testConfig = SimfDataConfig(
  baseUrl: 'http://test.local/api/v1',
  appKey: 'test',
  deviceType: SimfDeviceType.android,
);

const _greetingEn = 'Hello 🤝 I’m your smart assistant. How can I help today?';
const _greetingAr = 'مرحباً 🤝 أنا مساعدك الذكي. كيف يمكنني المساعدة اليوم؟';
const _errorEn = 'Couldn’t get a reply right now. Please try again.';
// The old scripted-demo line — must NOT appear any more (no fake transcript).
const _oldSeedQ1En = 'When does the opening session start?';

Future<void> _pump(
  WidgetTester tester, {
  ChatbotResponder? responder,
  Locale locale = const Locale('en'),
}) async {
  final router = GoRouter(
    initialLocation: '/chatbot',
    routes: <RouteBase>[
      GoRoute(
        path: '/chatbot',
        name: RouteNames.chatbot,
        builder: (_, __) => const ChatbotScreen(),
      ),
      for (final (name, path, label) in <(String, String, String)>[
        (RouteNames.home, '/', 'HOME'),
        (RouteNames.sessions, '/sessions', 'SESSIONS'),
        (RouteNames.badge, '/badge', 'BADGE'),
        (RouteNames.venueMap, '/map', 'MAP'),
        (RouteNames.myArea, '/my-area', 'MY-AREA'),
      ])
        GoRoute(
          name: name,
          path: path,
          builder: (c, s) => Scaffold(body: Text(label)),
        ),
    ],
  );

  await tester.pumpWidget(
    ProviderScope(
      overrides: <Override>[
        simfDataConfigProvider.overrideWithValue(_testConfig),
        if (responder != null)
          chatbotResponderProvider.overrideWithValue(responder),
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
  group('ChatbotScreen (Page 036 — KSA frame 1064:13066)', () {
    testWidgets('opens with only the greeting — no scripted transcript',
        (tester) async {
      await _pump(tester, responder: _FakeResponder('hi'));

      expect(find.text('AI assistant'), findsOneWidget); // header title
      expect(find.text(_greetingEn), findsOneWidget);
      // The removed fake "history" must not render.
      expect(find.text(_oldSeedQ1En), findsNothing);
      // The four quick-reply chips are still there.
      expect(find.text('Request a meeting'), findsOneWidget);
      expect(find.text('Today’s sessions'), findsOneWidget);
    });

    testWidgets('typing + send appends the user message and the AI reply',
        (tester) async {
      final responder = _FakeResponder('The opening session is at 08:00.');
      await _pump(tester, responder: responder);

      await tester.enterText(find.byType(TextField), 'When is the opening?');
      await tester.tap(find.byIcon(Icons.send));
      await tester.pumpAndSettle();

      expect(responder.lastPrompt, 'When is the opening?');
      expect(find.text('When is the opening?'), findsOneWidget);
      expect(find.text('The opening session is at 08:00.'), findsOneWidget);
    });

    testWidgets('tapping a quick-reply chip sends it as the next prompt',
        (tester) async {
      final responder = _FakeResponder('Chip reply');
      await _pump(tester, responder: responder);

      await tester.tap(find.text('Request a meeting'));
      await tester.pumpAndSettle();

      expect(responder.lastPrompt, 'Request a meeting');
      expect(find.text('Chip reply'), findsOneWidget);
    });

    testWidgets('an empty prompt does not append a bubble', (tester) async {
      await _pump(tester, responder: _FakeResponder('reply'));

      await tester.enterText(find.byType(TextField), '   ');
      await tester.tap(find.byIcon(Icons.send));
      await tester.pumpAndSettle();

      expect(find.text('reply'), findsNothing);
    });

    testWidgets('a wire error shows the localized error bubble', (tester) async {
      await _pump(tester, responder: _ThrowingResponder());

      await tester.enterText(find.byType(TextField), 'anything');
      await tester.tap(find.byIcon(Icons.send));
      await tester.pumpAndSettle();

      expect(find.text('anything'), findsOneWidget); // the user's message stayed
      expect(find.text(_errorEn), findsOneWidget); // replaced by the error bubble
    });

    testWidgets('a non-ApiFailure error recovers (composer is not stuck)',
        (tester) async {
      await _pump(tester, responder: _StateErrorResponder());

      await tester.enterText(find.byType(TextField), 'first');
      await tester.tap(find.byIcon(Icons.send));
      await tester.pumpAndSettle();
      expect(find.text(_errorEn), findsOneWidget);

      // The composer is not stuck: a second send goes through and errors again.
      await tester.enterText(find.byType(TextField), 'second');
      await tester.tap(find.byIcon(Icons.send));
      await tester.pumpAndSettle();
      expect(find.text('second'), findsOneWidget);
      expect(find.text(_errorEn), findsNWidgets(2));
    });

    testWidgets('Arabic: greets in Arabic and pins the sent bubble RTL',
        (tester) async {
      final responder = _FakeResponder('رد');
      await _pump(tester, responder: responder, locale: const Locale('ar'));

      expect(find.text(_greetingAr), findsOneWidget);

      await tester.enterText(find.byType(TextField), 'متى تبدأ جلسة الافتتاح؟');
      await tester.tap(find.byIcon(Icons.send));
      await tester.pumpAndSettle();

      // The user bubble sits to the RIGHT of the assistant greeting under RTL —
      // the Figma pins user-right / assistant-left (D-436).
      final assistantX = tester.getCenter(find.text(_greetingAr)).dx;
      final userX =
          tester.getCenter(find.text('متى تبدأ جلسة الافتتاح؟')).dx;
      expect(userX, greaterThan(assistantX));
    });
  });
}
