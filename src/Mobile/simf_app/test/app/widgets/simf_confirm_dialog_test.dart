import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/widgets/simf_confirm_dialog.dart';

const _title = 'هل تريد إلغاء حجز المقعد في هذه الجلسة؟';
const _confirm = 'نعم، إلغاء الحجز الآن';
const _cancel = 'لا، الاحتفاظ بالحجز';

/// Pumps a host whose button opens the dialog; the resolved bool is appended to
/// [results] when the dialog closes.
Future<void> _pumpHost(
  WidgetTester tester, {
  required List<bool> results,
  String? message,
}) async {
  await tester.pumpWidget(
    MaterialApp(
      locale: const Locale('ar'),
      supportedLocales: AppL10n.supportedLocales,
      localizationsDelegates: const <LocalizationsDelegate<dynamic>>[
        ...AppL10n.localizationsDelegates,
        GlobalMaterialLocalizations.delegate,
        GlobalWidgetsLocalizations.delegate,
        GlobalCupertinoLocalizations.delegate,
      ],
      home: Scaffold(
        body: Builder(
          builder: (context) => TextButton(
            onPressed: () async {
              final r = await SimfConfirmDialog.show(
                context,
                title: _title,
                message: message,
                confirmLabel: _confirm,
                cancelLabel: _cancel,
                isDestructive: true,
              );
              results.add(r);
            },
            child: const Text('open'),
          ),
        ),
      ),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  group('SimfConfirmDialog', () {
    testWidgets('lays the two long-label buttons side by side (same row)',
        (tester) async {
      await _pumpHost(
        tester,
        results: <bool>[],
        message: 'لا يمكن التراجع عن هذا الإجراء.',
      );
      await tester.tap(find.text('open'));
      await tester.pumpAndSettle();

      final cancel = find.widgetWithText(TextButton, _cancel);
      final confirm = find.widgetWithText(FilledButton, _confirm);
      expect(cancel, findsOneWidget);
      expect(confirm, findsOneWidget);

      // The whole point: both buttons share one row — equal vertical centre,
      // different horizontal centre — never stacked into a column.
      final cancelCentre = tester.getCenter(cancel);
      final confirmCentre = tester.getCenter(confirm);
      expect((cancelCentre.dy - confirmCentre.dy).abs(), lessThan(1.0));
      expect((cancelCentre.dx - confirmCentre.dx).abs(), greaterThan(1.0));
    });

    testWidgets('renders without a body when message is omitted',
        (tester) async {
      await _pumpHost(tester, results: <bool>[]);
      await tester.tap(find.text('open'));
      await tester.pumpAndSettle();
      expect(find.text(_title), findsOneWidget);
      expect(find.byType(FilledButton), findsOneWidget);
    });

    testWidgets('tapping confirm resolves true', (tester) async {
      final results = <bool>[];
      await _pumpHost(tester, results: results);
      await tester.tap(find.text('open'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, _confirm));
      await tester.pumpAndSettle();
      expect(results.single, isTrue);
    });

    testWidgets('tapping cancel resolves false', (tester) async {
      final results = <bool>[];
      await _pumpHost(tester, results: results);
      await tester.tap(find.text('open'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(TextButton, _cancel));
      await tester.pumpAndSettle();
      expect(results.single, isFalse);
    });
  });
}
