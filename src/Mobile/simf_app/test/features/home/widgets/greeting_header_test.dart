import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/misc.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/features/account/data/profile_repository.dart';
import 'package:simf_app/features/home/widgets/greeting_header.dart';
import 'package:simf_app/features/notifications/data/notifications_repository.dart';

import '../../../support/simf_test_scope.dart';

/// OA-D1 — the Home greeting used to render `name.trim().split(' ').first`,
/// which amputated every Arabic compound given name (عبد الله → عبد) and
/// greeted the wrong token whenever the family name came first. The greeting
/// now renders the FULL trimmed name; these cases fail against the old split.
Future<void> _pumpGreeting(
  WidgetTester tester,
  String name, {
  Locale locale = const Locale('ar'),
}) async {
  await tester.pumpWidget(
    simfTestScope(
      overrides: <Override>[
        // The bell watches the unread count and the avatar watches the photo
        // bytes — both auth-scoped. Stub them so a header test does not have to
        // stand up a whole signed-in session.
        unreadNotificationCountProvider.overrideWith((ref) async => 0),
        myAvatarBytesProvider.overrideWith((ref) async => null),
      ],
      child: MaterialApp(
        locale: locale,
        supportedLocales: AppL10n.supportedLocales,
        localizationsDelegates: const <LocalizationsDelegate<dynamic>>[
          ...AppL10n.localizationsDelegates,
          GlobalMaterialLocalizations.delegate,
          GlobalWidgetsLocalizations.delegate,
          GlobalCupertinoLocalizations.delegate,
        ],
        home: Builder(
          builder: (context) => Scaffold(
            body: GreetingHeader(l10n: AppL10n.of(context), name: name),
          ),
        ),
      ),
    ),
  );
  await tester.pump();
}

void main() {
  group('GreetingHeader name line (OA-D1)', () {
    testWidgets('keeps an Arabic compound given name whole (عبد الله)',
        (tester) async {
      await _pumpGreeting(tester, 'عبد الله');

      expect(find.text('عبد الله 👋'), findsOneWidget);
      expect(find.text('عبد 👋'), findsNothing);
    });

    testWidgets('keeps عبد الرحمن whole, not just عبد', (tester) async {
      await _pumpGreeting(tester, 'عبد الرحمن السالم');

      expect(find.text('عبد الرحمن السالم 👋'), findsOneWidget);
      expect(find.text('عبد 👋'), findsNothing);
    });

    testWidgets('greets the full multi-part Latin name', (tester) async {
      await _pumpGreeting(
        tester,
        'Ahmed Al Otaibi',
        locale: const Locale('en'),
      );

      expect(find.text('Ahmed Al Otaibi 👋'), findsOneWidget);
      expect(find.text('Ahmed 👋'), findsNothing);
    });

    testWidgets('trims surrounding whitespace', (tester) async {
      await _pumpGreeting(tester, '  أحمد محمد  ');

      expect(find.text('أحمد محمد 👋'), findsOneWidget);
    });

    testWidgets('a name-less account greets with the wave alone',
        (tester) async {
      await _pumpGreeting(tester, '   ');

      expect(find.text('👋'), findsOneWidget);
    });

    testWidgets('the name line stays single-line + ellipsised so a long '
        'compound name degrades gracefully', (tester) async {
      await _pumpGreeting(tester, 'عبد الرحمن بن عبد العزيز بن محمد السالم');

      final nameText = tester.widget<Text>(
        find.text('عبد الرحمن بن عبد العزيز بن محمد السالم 👋'),
      );
      expect(nameText.maxLines, 1);
      expect(nameText.overflow, TextOverflow.ellipsis);
    });
  });
}
