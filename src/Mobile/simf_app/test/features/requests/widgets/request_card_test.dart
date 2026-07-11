import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/features/requests/data/request_models.dart';
import 'package:simf_app/features/requests/widgets/request_card.dart';

AppRequestItem _item({String? responseNote}) => AppRequestItem(
      kind: AppRequestKind.participationDocument,
      id: '1',
      title: 'Official attendance certificate',
      titleArabic: 'شهادة حضور رسمية',
      status: AppRequestStatus.rejected,
      createdAt: DateTime.utc(2026, 1, 10),
      canCancel: false,
      responseNote: responseNote,
    );

Future<void> _pumpCard(WidgetTester tester, AppRequestItem item) async {
  await tester.pumpWidget(
    MaterialApp(
      locale: const Locale('en'),
      supportedLocales: AppL10n.supportedLocales,
      localizationsDelegates: const <LocalizationsDelegate<dynamic>>[
        ...AppL10n.localizationsDelegates,
        GlobalMaterialLocalizations.delegate,
        GlobalWidgetsLocalizations.delegate,
        GlobalCupertinoLocalizations.delegate,
      ],
      home: Scaffold(
        body: Builder(
          builder: (context) => RequestCard(
            item: item,
            isArabic: false,
            l10n: AppL10n.of(context),
            onCancel: () {},
          ),
        ),
      ),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  group('RequestCard responseNote (R-3)', () {
    testWidgets('shows the response note when present, once expanded',
        (tester) async {
      await _pumpCard(tester, _item(responseNote: 'Missing passport copy.'));
      // Collapsed: the note lives in the expandable detail.
      expect(find.text('Missing passport copy.'), findsNothing);

      // Expand the card.
      await tester.tap(find.byType(InkWell).first);
      await tester.pumpAndSettle();
      expect(find.text('Missing passport copy.'), findsOneWidget);
    });

    testWidgets('omits the response note when null', (tester) async {
      await _pumpCard(tester, _item(responseNote: null));
      await tester.tap(find.byType(InkWell).first);
      await tester.pumpAndSettle();
      // No note text is rendered; only the status label remains in the detail.
      expect(find.text('Missing passport copy.'), findsNothing);
    });
  });
}
