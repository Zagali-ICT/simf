import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/features/chatbot/chatbot_screen.dart';

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

Future<void> _pump(
  WidgetTester tester, {
  required ChatbotResponder responder,
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
    ],
  );

  await tester.pumpWidget(
    ProviderScope(
      overrides: <Override>[
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
  group('ChatbotScreen (Page 036)', () {
    testWidgets('starts empty with the preview banner', (tester) async {
      await _pump(tester, responder: _FakeResponder('hi'));

      expect(
        find.text('The AI assistant is in preview — replies are interim.'),
        findsOneWidget,
      );
      expect(find.text('Ask the assistant to get started.'), findsOneWidget);
    });

    testWidgets('typing + send appends the user message and the reply',
        (tester) async {
      final responder = _FakeResponder('Canned reply');
      await _pump(tester, responder: responder);

      await tester.enterText(find.byType(TextField), 'When does it start?');
      await tester.tap(find.byTooltip('Send'));
      await tester.pumpAndSettle();

      expect(responder.lastPrompt, 'When does it start?');
      expect(find.text('When does it start?'), findsOneWidget);
      expect(find.text('Canned reply'), findsOneWidget);
    });

    testWidgets('the default responder returns the canned interim notice',
        (tester) async {
      // No override here — exercise the real CannedChatbotResponder.
      final router = GoRouter(
        initialLocation: '/chatbot',
        routes: <RouteBase>[
          GoRoute(
            path: '/chatbot',
            name: RouteNames.chatbot,
            builder: (_, __) => const ChatbotScreen(),
          ),
        ],
      );
      await tester.pumpWidget(
        ProviderScope(
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

      await tester.enterText(find.byType(TextField), 'hello');
      await tester.tap(find.byTooltip('Send'));
      await tester.pumpAndSettle();

      expect(
        find.text(
          'The AI assistant is being connected — automatic replies are coming soon.',
        ),
        findsOneWidget,
      );
    });

    testWidgets('an empty prompt does not append a bubble', (tester) async {
      await _pump(tester, responder: _FakeResponder('reply'));

      await tester.enterText(find.byType(TextField), '   ');
      await tester.tap(find.byTooltip('Send'));
      await tester.pumpAndSettle();

      expect(find.text('reply'), findsNothing);
      expect(find.text('Ask the assistant to get started.'), findsOneWidget);
    });

    testWidgets('dismissing the preview banner hides it', (tester) async {
      await _pump(tester, responder: _FakeResponder('reply'));

      await tester.tap(find.byIcon(Icons.close));
      await tester.pumpAndSettle();

      expect(
        find.text('The AI assistant is in preview — replies are interim.'),
        findsNothing,
      );
    });

    testWidgets('renders the Arabic canned notice in Arabic', (tester) async {
      final router = GoRouter(
        initialLocation: '/chatbot',
        routes: <RouteBase>[
          GoRoute(
            path: '/chatbot',
            name: RouteNames.chatbot,
            builder: (_, __) => const ChatbotScreen(),
          ),
        ],
      );
      await tester.pumpWidget(
        ProviderScope(
          child: MaterialApp.router(
            routerConfig: router,
            locale: const Locale('ar'),
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

      await tester.enterText(find.byType(TextField), 'مرحبا');
      await tester.tap(find.byType(IconButton).last);
      await tester.pumpAndSettle();

      expect(
        find.text('المساعد الذكي قيد التفعيل — سيتوفر الرد التلقائي قريباً.'),
        findsOneWidget,
      );
    });
  });
}
