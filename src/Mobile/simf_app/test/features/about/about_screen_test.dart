import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/features/about/about_screen.dart';
import 'package:simf_app/features/content/data/content_models.dart';
import 'package:simf_app/features/content/data/content_repository.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

class _FakeContentRepo implements ContentRepository {
  _FakeContentRepo({this.block, this.status});

  final ContentBlock? block;
  final int? status;

  @override
  Future<ContentBlock> getContentBlock(String key) async {
    if (status != null) {
      throw ApiFailure(
        code: ApiErrorCodes.clientNetwork,
        message: 'x',
        httpStatus: status,
      );
    }
    return block!;
  }
}

Future<void> _pump(WidgetTester tester, ContentRepository repo) async {
  await tester.pumpWidget(
    ProviderScope(
      overrides: <Override>[contentRepositoryProvider.overrideWithValue(repo)],
      child: MaterialApp(
        locale: const Locale('en'),
        supportedLocales: AppL10n.supportedLocales,
        localizationsDelegates: const <LocalizationsDelegate<dynamic>>[
          ...AppL10n.localizationsDelegates,
          GlobalMaterialLocalizations.delegate,
          GlobalWidgetsLocalizations.delegate,
          GlobalCupertinoLocalizations.delegate,
        ],
        home: const AboutScreen(),
      ),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  group('AboutScreen (Page 037)', () {
    testWidgets('renders the localized body', (tester) async {
      await _pump(
        tester,
        _FakeContentRepo(
          block: const ContentBlock(
            key: 'about',
            content: 'About the maritime forum.',
            contentArabic: '',
          ),
        ),
      );
      expect(find.text('About the maritime forum.'), findsOneWidget);
    });

    testWidgets('a 404 (unseeded key) shows the coming-soon state',
        (tester) async {
      await _pump(tester, _FakeContentRepo(status: 404));
      expect(find.text('Content coming soon'), findsOneWidget);
    });

    testWidgets('a server error shows error + retry', (tester) async {
      await _pump(tester, _FakeContentRepo(status: 500));
      expect(find.text('Could not load the content.'), findsOneWidget);
      expect(find.widgetWithText(FilledButton, 'Retry'), findsOneWidget);
    });
  });
}
