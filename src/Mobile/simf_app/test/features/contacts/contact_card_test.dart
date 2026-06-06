import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/features/contacts/widgets/contact_card.dart';

Future<void> _pump(WidgetTester tester, ContactCard card) async {
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
      home: Scaffold(body: card),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  group('ContactCard (FDS-014)', () {
    testWidgets('renders the name + only the channels that are set',
        (tester) async {
      await _pump(
        tester,
        const ContactCard(
          name: 'Sara Ahmed',
          available: true,
          jobTitle: 'Captain',
          organisation: 'RSNF',
          email: 'sara@example.com',
          // No phones / country — those rows must not render.
        ),
      );

      expect(find.text('Sara Ahmed'), findsOneWidget);
      expect(find.text('Captain'), findsOneWidget);
      expect(find.text('RSNF'), findsOneWidget);
      expect(find.text('sara@example.com'), findsOneWidget);
      expect(find.byIcon(Icons.email_outlined), findsOneWidget);
      // No phone set → no phone row.
      expect(find.byIcon(Icons.phone_outlined), findsNothing);
    });

    testWidgets('an unavailable subject shows only the unavailable note',
        (tester) async {
      await _pump(
        tester,
        const ContactCard(
          name: '',
          available: false,
          organisation: 'RSNF',
        ),
      );

      expect(
        find.text('This contact is no longer available'),
        findsOneWidget,
      );
      // Channels are hidden when unavailable.
      expect(find.text('RSNF'), findsNothing);
      expect(find.byIcon(Icons.business_outlined), findsNothing);
    });

    testWidgets('shows the note block when a note is present', (tester) async {
      await _pump(
        tester,
        const ContactCard(
          name: 'Sara Ahmed',
          available: true,
          note: 'met at booth 3',
        ),
      );

      expect(find.text('Note'), findsOneWidget);
      expect(find.text('met at booth 3'), findsOneWidget);
    });
  });
}
