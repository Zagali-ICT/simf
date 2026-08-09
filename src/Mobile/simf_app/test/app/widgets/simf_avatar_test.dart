import 'dart:typed_data';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/widgets/simf_logo.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/features/account/data/profile_repository.dart';

/// A minimal valid 1×1 PNG so the `currentUser` bytes path decodes without
/// tripping the error builder.
final Uint8List _onePixelPng = Uint8List.fromList(<int>[
  0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D,
  0x49, 0x48, 0x44, 0x52, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
  0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4, 0x89, 0x00, 0x00, 0x00,
  0x0A, 0x49, 0x44, 0x41, 0x54, 0x78, 0x9C, 0x63, 0x00, 0x01, 0x00, 0x00,
  0x05, 0x00, 0x01, 0x0D, 0x0A, 0x2D, 0xB4, 0x00, 0x00, 0x00, 0x00, 0x49,
  0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82,
]);

Widget _host(Widget child, {List<Override> overrides = const <Override>[]}) {
  return ProviderScope(
    overrides: overrides,
    child: MaterialApp(home: Scaffold(body: child)),
  );
}

void main() {
  group('SimfAvatar default fallback (D-402 — SIMF logo, not initials)', () {
    testWidgets('renders the SIMF brand mark when there is no photo',
        (tester) async {
      await tester.pumpWidget(_host(const SimfAvatar(name: 'Raed Al-Salem', size: 64)));

      // The fallback is the brand mark on a navy box — never name initials.
      expect(find.byType(SimfLogo), findsOneWidget);
      expect(find.byType(Text), findsNothing);
    });

    testWidgets('exposes the name as the accessibility label', (tester) async {
      await tester.pumpWidget(_host(const SimfAvatar(name: 'Raed Al-Salem')));

      expect(find.bySemanticsLabel('Raed Al-Salem'), findsOneWidget);
    });

    testWidgets('still shows the brand mark even for an empty name',
        (tester) async {
      await tester.pumpWidget(_host(const SimfAvatar(name: '')));

      expect(find.byType(SimfLogo), findsOneWidget);
    });
  });

  group('SimfAvatar current-user photo (D-422 — authenticated bytes)', () {
    testWidgets('a non-current-user avatar never fetches — always the fallback',
        (tester) async {
      // currentUser:false (e.g. a question submitter) must not read the
      // signed-in user's avatar; it stays the brand mark.
      await tester.pumpWidget(
        _host(
          const SimfAvatar(name: 'Other Person', size: 40),
          overrides: <Override>[
            myAvatarBytesProvider.overrideWith((ref) async => _onePixelPng),
          ],
        ),
      );

      // The fallback brand mark is shown — the overridden photo bytes are not
      // leaked onto a non-current-user avatar (SimfLogo present ⇒ fallback path).
      expect(find.byType(SimfLogo), findsOneWidget);
    });

    testWidgets('the current user with bytes shows the photo, not the fallback',
        (tester) async {
      await tester.pumpWidget(
        _host(
          const SimfAvatar(name: 'Me', currentUser: true),
          overrides: <Override>[
            myAvatarBytesProvider.overrideWith((ref) async => _onePixelPng),
          ],
        ),
      );
      await tester.pump(); // let the FutureProvider resolve

      expect(find.byType(Image), findsOneWidget);
      expect(find.byType(SimfLogo), findsNothing);
    });

    testWidgets('the current user with no photo falls back to the brand mark',
        (tester) async {
      await tester.pumpWidget(
        _host(
          const SimfAvatar(name: 'Me', currentUser: true),
          overrides: <Override>[
            myAvatarBytesProvider.overrideWith((ref) async => null),
          ],
        ),
      );
      await tester.pump();

      expect(find.byType(SimfLogo), findsOneWidget);
    });
  });
}
