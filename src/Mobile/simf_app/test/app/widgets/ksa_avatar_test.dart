import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/widgets/ksa_shell.dart';
import 'package:simf_app/app/widgets/simf_logo.dart';

void main() {
  group('KsaAvatar default fallback (D-402 — SIMF logo, not initials)', () {
    testWidgets('renders the SIMF brand mark when there is no photo',
        (tester) async {
      await tester.pumpWidget(
        const MaterialApp(
          home: Scaffold(
            body: KsaAvatar(name: 'Raed Al-Salem', size: 64),
          ),
        ),
      );

      // The fallback is the brand mark on a navy box — never name initials.
      expect(find.byType(SimfLogo), findsOneWidget);
      expect(find.byType(Text), findsNothing);
    });

    testWidgets('exposes the name as the accessibility label', (tester) async {
      await tester.pumpWidget(
        const MaterialApp(
          home: Scaffold(
            body: KsaAvatar(name: 'Raed Al-Salem'),
          ),
        ),
      );

      expect(
        find.bySemanticsLabel('Raed Al-Salem'),
        findsOneWidget,
      );
    });

    testWidgets('still shows the brand mark even for an empty name',
        (tester) async {
      await tester.pumpWidget(
        const MaterialApp(
          home: Scaffold(body: KsaAvatar(name: '')),
        ),
      );

      expect(find.byType(SimfLogo), findsOneWidget);
    });
  });
}
