@Tags(<String>['golden'])
library;

import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/misc.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/theme/app_theme.dart';
import 'package:simf_app/features/questions/data/questions_repository.dart';
import 'package:simf_app/features/questions/send_question_screen.dart';
import 'package:simf_app/features/sessions/data/my_seat.dart';
import 'package:simf_app/features/sessions/data/session_detail_repository.dart';
import 'package:simf_app/features/sessions/data/session_models.dart';
import 'package:simf_app/features/sessions/data/session_speaker.dart';

import '../support/simf_test_scope.dart';
import 'golden_fonts.dart';

/// Golden render of the About-session / send-a-question screen against Figma
/// frame **934:3636** (معلومات عن الجلسة). Compare to the frame: flutter test
/// --update-goldens test/golden/send_question_golden_test.dart
///
/// Frame parity: the "بيانات الجلسة" numbered data block, the "الاسئلة" label
/// over the fixed 100px tinted composer box, and the bottom-pinned gold submit
/// + centred reviewed-before-air note (a wide empty gap separates the composer
/// from the bottom action). RTL.

SessionDetail _detail() => SessionDetail(
      id: 's1',
      code: 'S1',
      title: 'Opening',
      titleArabic: 'الجلسة الافتتاحية',
      hallId: 'h1',
      hallName: 'Main Hall',
      hallNameArabic: 'قاعة الملك فهد',
      start: DateTime.utc(2026, 6, 20, 6),
      end: DateTime.utc(2026, 6, 20, 7),
      speakers: const <SessionSpeaker>[],
      descriptionArabic:
          'منصة دولية جمعت قادة القوات البحرية والخبراء لمناقشة مستقبل الأمن '
          'البحري وحماية الممرات الملاحية.\n'
          'تبادل التجارب الدولية في تأمين الموانئ والبنية التحتية البحرية.\n'
          'بناء شراكات استراتيجية لدعم الاستقرار في الممرات المائية.',
    );

class _FakeDetailRepo implements SessionDetailRepository {
  @override
  Future<SessionDetail> getDetail(String sessionId) async => _detail();

  @override
  Future<MySeat?> getMySeat(String sessionId) async => null;
}

class _FakeQuestionsRepo implements QuestionsRepository {
  @override
  Future<void> submitQuestion(
    String sessionId, {
    required String questionText,
    required int recipientIndex,
  }) async {}
}

void main() {
  setUpAll(loadGoldenFonts);

  testWidgets('Send-question @375x880 — Figma 934:3636 (Arabic)',
      (tester) async {
    tester.view.physicalSize = const Size(375, 880);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

    final router = GoRouter(
      initialLocation: '/live/question',
      routes: <RouteBase>[
        GoRoute(
          path: '/live/question',
          name: RouteNames.sendQuestion,
          builder: (_, __) => const SendQuestionScreen(sessionId: 's1'),
        ),
      ],
    );

    await tester.pumpWidget(
      simfTestScope(
        overrides: <Override>[
          sessionDetailRepositoryProvider.overrideWithValue(_FakeDetailRepo()),
          questionsRepositoryProvider.overrideWithValue(_FakeQuestionsRepo()),
        ],
        child: MaterialApp.router(
          debugShowCheckedModeBanner: false,
          theme: SimfTheme.dark(),
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

    await expectLater(
      find.byType(SendQuestionScreen),
      matchesGoldenFile('goldens/send_question_934-3636.png'),
    );
  });
}
