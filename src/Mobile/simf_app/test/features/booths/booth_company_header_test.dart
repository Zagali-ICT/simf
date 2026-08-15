import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/features/booths/widgets/booth_company_header.dart';
import 'package:simf_app/features/venuemap/data/venue_map_models.dart';

BoothSummary _booth({required String name, String? exhibitorName}) =>
    BoothSummary(
      id: 'b1',
      code: 'A-01',
      name: name,
      nameArabic: '',
      exhibitorName: exhibitorName,
    );

/// The exhibitor (full-name) line only — matched on its own beige style so the
/// gold short name and the logo tile's short-name fallback cannot be mistaken
/// for it.
Finder _exhibitorLine(String text) => find.byWidgetPredicate(
      (widget) =>
          widget is Text &&
          widget.data == text &&
          widget.style == SimfTokens.bodyBeigeSm,
    );

/// The company short name (the gold line above the exhibitor line).
Finder _shortName(String text) => find.byWidgetPredicate(
      (widget) =>
          widget is Text &&
          widget.data == text &&
          widget.style == SimfTokens.labelGoldMedium,
    );

Future<void> _pumpHeader(WidgetTester tester, BoothSummary booth) async {
  await tester.pumpWidget(
    MaterialApp(
      home: Scaffold(
        body: BoothCompanyHeader(
          booth: booth,
          isArabic: false,
          baseUrl: 'http://test.local/api/v1',
        ),
      ),
    ),
  );
  await tester.pump();
}

void main() {
  // PAR-B4 — the seeded booths carry the same string in `name` and
  // `exhibitorName`, which rendered the company name twice on the card.
  group('BoothCompanyHeader duplicate exhibitor line (PAR-B4)', () {
    testWidgets('an exhibitor name IDENTICAL to the short name is not repeated',
        (tester) async {
      const company = 'Advanced Naval Technologies';
      await _pumpHeader(
        tester,
        _booth(name: company, exhibitorName: company),
      );

      expect(_shortName(company), findsOneWidget);
      expect(_exhibitorLine(company), findsNothing);
    });

    testWidgets(
        'an exhibitor name differing only by whitespace is not repeated',
        (tester) async {
      await _pumpHeader(
        tester,
        _booth(
          name: 'Advanced Naval Technologies',
          exhibitorName: '  Advanced Naval Technologies  ',
        ),
      );

      expect(_shortName('Advanced Naval Technologies'), findsOneWidget);
      expect(_exhibitorLine('  Advanced Naval Technologies  '), findsNothing);
    });

    testWidgets('a DISTINCT exhibitor name still renders both lines',
        (tester) async {
      await _pumpHeader(
        tester,
        _booth(
          name: 'SAMI',
          exhibitorName: 'Saudi Arabian Military Industries',
        ),
      );

      expect(_shortName('SAMI'), findsOneWidget);
      expect(
        _exhibitorLine('Saudi Arabian Military Industries'),
        findsOneWidget,
      );
    });

    testWidgets('a booth with no exhibitor name renders only the short name',
        (tester) async {
      await _pumpHeader(tester, _booth(name: 'SAMI'));

      expect(_shortName('SAMI'), findsOneWidget);
      expect(_exhibitorLine('SAMI'), findsNothing);
    });
  });
}
