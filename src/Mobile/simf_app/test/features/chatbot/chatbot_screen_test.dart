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

// KsaPage renders the bottom nav + the المزيد drawer, which read the data
// config; the destinations are stubbed so the shell builds.
const _testConfig = SimfDataConfig(
  baseUrl: 'http://test.local/api/v1',
  appKey: 'test',
  deviceType: SimfDeviceType.android,
);

const _greetingEn = 'Hello 🤝 I’m your smart assistant. How can I help today?';
const _seedQ1En = 'When does the opening session start?';

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
    testWidgets('opens with the scripted Figma transcript', (tester) async {
      await _pump(tester, responder: _FakeResponder('hi'));

      expect(find.text('AI assistant'), findsOneWidget); // header title
      expect(find.text(_greetingEn), findsOneWidget);
      expect(find.text(_seedQ1En), findsOneWidget);
      // The four quick-reply chips.
      expect(find.text('Request a meeting'), findsOneWidget);
      expect(find.text('Today’s sessions'), findsOneWidget);
    });

    testWidgets('typing + send appends the user message and the reply',
        (tester) async {
      final responder = _FakeResponder('Canned reply');
      await _pump(tester, responder: responder);

      await tester.enterText(find.byType(TextField), 'Custom question?');
      await tester.tap(find.byIcon(Icons.send));
      await tester.pumpAndSettle();

      expect(responder.lastPrompt, 'Custom question?');
      expect(find.text('Custom question?'), findsOneWidget);
      expect(find.text('Canned reply'), findsOneWidget);
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

    testWidgets('the default responder returns the canned interim notice',
        (tester) async {
      // No responder override — exercise the real CannedChatbotResponder.
      await _pump(tester);

      await tester.enterText(find.byType(TextField), 'hello');
      await tester.tap(find.byIcon(Icons.send));
      await tester.pumpAndSettle();

      expect(
        find.text(
          'The AI assistant is being connected — automatic replies are coming soon.',
        ),
        findsOneWidget,
      );
    });

    testWidgets('Arabic: seeds the Arabic transcript and replies RTL',
        (tester) async {
      await _pump(tester, locale: const Locale('ar'));

      expect(
        find.text('مرحباً 🤝 أنا مساعدك الذكي. كيف يمكنني المساعدة اليوم؟'),
        findsOneWidget,
      );

      // Position: the user bubble (seed Q1) sits to the right of the assistant
      // greeting under RTL — the Figma pins user-right / assistant-left (D-436).
      final assistantX = tester
          .getCenter(
            find.text('مرحباً 🤝 أنا مساعدك الذكي. كيف يمكنني المساعدة اليوم؟'),
          )
          .dx;
      final userX = tester.getCenter(find.text('متى تبدأ جلسة الافتتاح؟')).dx;
      expect(userX, greaterThan(assistantX));
    });
  });
}
