import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/features/content/data/content_models.dart';
import 'package:simf_app/features/content/data/content_repository.dart';
import 'package:simf_app/features/content/terms_screen.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// A fake content repo returning a canned block or throwing — so the Terms
/// screen's load → state glue (loaded / empty / error / gate) is testable.
class _FakeContentRepository implements ContentRepository {
  _FakeContentRepository({this.block, this.failure});

  ContentBlock? block;
  ApiFailure? failure;
  int calls = 0;

  @override
  Future<ContentBlock> getContentBlock(String key) async {
    calls++;
    final f = failure;
    if (f != null) {
      throw f;
    }
    return block!;
  }
}

Future<void> _pump(
  WidgetTester tester,
  _FakeContentRepository repo, {
  bool requireConsent = false,
}) async {
  await tester.pumpWidget(
    ProviderScope(
      overrides: <Override>[
        contentRepositoryProvider.overrideWithValue(repo),
      ],
      child: MaterialApp(
        locale: const Locale('en'),
        supportedLocales: AppL10n.supportedLocales,
        localizationsDelegates: const <LocalizationsDelegate<dynamic>>[
          ...AppL10n.localizationsDelegates,
          GlobalMaterialLocalizations.delegate,
          GlobalWidgetsLocalizations.delegate,
          GlobalCupertinoLocalizations.delegate,
        ],
        home: TermsScreen(requireConsent: requireConsent),
      ),
    ),
  );
  await tester.pumpAndSettle();
}

ContentBlock _block() => ContentBlock(
      key: 'terms',
      content: 'These are the terms.',
      contentArabic: 'هذه هي الشروط.',
      lastUpdatedAt: DateTime.utc(2026, 9),
    );

void main() {
  group('TermsScreen (Page 009)', () {
    testWidgets(
        'standalone mode renders the body, no last-updated line, and the '
        'always-visible Agree button (D-375 — frame 505:1553)',
        (tester) async {
      await _pump(tester, _FakeContentRepository(block: _block()));

      expect(find.byType(SelectableText), findsOneWidget);
      expect(find.text('Last updated · 2026-09-01'), findsNothing);
      expect(find.text('I accept the terms and conditions'), findsNothing);
      final agree = find.widgetWithText(FilledButton, 'Agree');
      expect(tester.widget<FilledButton>(agree).onPressed, isNotNull);
    });

    testWidgets(
        'consent mode shows the always-enabled Agree button (D-367 — the '
        'explicit tap IS the consent; the interim checkbox is gone)',
        (tester) async {
      await _pump(
        tester,
        _FakeContentRepository(block: _block()),
        requireConsent: true,
      );

      expect(find.text('I accept the terms and conditions'), findsNothing);
      final agree = find.widgetWithText(FilledButton, 'Agree');
      expect(tester.widget<FilledButton>(agree).onPressed, isNotNull);
    });

    testWidgets('an empty body shows the empty state with retry',
        (tester) async {
      await _pump(
        tester,
        _FakeContentRepository(
          block: const ContentBlock(
            key: 'terms',
            content: '',
            contentArabic: '',
          ),
        ),
      );

      expect(find.text('No content'), findsOneWidget);
      expect(find.widgetWithText(FilledButton, 'Retry'), findsOneWidget);
    });

    testWidgets('a 404 is treated as the empty state', (tester) async {
      await _pump(
        tester,
        _FakeContentRepository(
          failure: const ApiFailure(
            code: 'CONTENT_BLOCK_NOT_FOUND',
            message: 'Content block not found.',
            httpStatus: 404,
          ),
        ),
      );

      expect(find.text('No content'), findsOneWidget);
    });

    testWidgets('a transport error shows the message + retry, which reloads',
        (tester) async {
      final repo = _FakeContentRepository(
        block: _block(),
        failure: const ApiFailure(
          code: 'X',
          message: 'Server exploded.',
          httpStatus: 500,
        ),
      );
      await _pump(tester, repo);

      expect(find.text('Server exploded.'), findsOneWidget);

      repo.failure = null; // recover
      await tester.tap(find.widgetWithText(FilledButton, 'Retry'));
      await tester.pumpAndSettle();

      expect(find.byType(SelectableText), findsOneWidget);
      expect(repo.calls, greaterThanOrEqualTo(2));
    });
  });
}
