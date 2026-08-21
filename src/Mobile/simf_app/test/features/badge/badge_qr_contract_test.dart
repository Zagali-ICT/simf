import 'dart:typed_data';

import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/misc.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:qr_flutter/qr_flutter.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/features/account/data/profile_repository.dart'
    show referenceNumberProvider;
import 'package:simf_app/features/badge/badge_screen.dart';
import 'package:simf_app/features/badge/widgets/badge_qr_card.dart';
import 'package:simf_app/features/myarea/data/myarea_models.dart';
import 'package:simf_app/features/myarea/data/myarea_repository.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';

import '../../support/simf_test_scope.dart';

/// The two things about the entry badge that a render cannot show, and that
/// the badge screen's own tests therefore cannot catch.
///
/// 1. **Module shape.** A round-styled badge QR is a banked SIMF incident: it
///    rendered perfectly and `flutter_zxing` - the gate scanner's decoder -
///    could not read it. The fix was square modules. A golden certifies the
///    picture, and the round picture is a *valid-looking* picture, so a golden
///    stays green straight through the defect. Decoding the render back
///    in-test would be the strongest check and is not available here:
///    `flutter_zxing` is an FFI binding that opens a native ZXing library
///    (`DynamicLibrary.open('libflutter_zxing.so' / 'flutter_zxing.dll')` in
///    `flutter_zxing/lib/src/logic/bindings.dart`), and that library ships
///    only in a built app bundle, never under `flutter_tester`. So instead
///    these tests compare the badge's rendered PIXELS against a canonical
///    square render of the same payload, and separately pin the style fields
///    that select `drawRect` over `drawRRect` inside `QrPainter`.
///
/// 2. **Which identifier the QR encodes.** The badge carries two ids and the
///    page docs warn they are different things: the opaque `qrId` (what the
///    gate scanner resolves an attendee from) and the human reference number
///    `SIMF-...` (what the identity strip prints). Encoding the reference
///    number produces an equally valid QR that no gate can identify anyone
///    from.

CurrentUser _user() => CurrentUser(
      id: 'u1',
      email: 'v@example.sa',
      displayName: 'Raed Al-Salem',
      appRole: AppRole.visitor,
      preferredLanguage: PreferredLanguage.fromJson('ar'),
      registrationStatus: RegistrationStatus.approved,
    );

class _AuthController extends AuthController {
  @override
  AuthState build() => AuthStateSignedIn(
        Session(
          accessToken: 'A',
          refreshToken: 'R',
          accessTokenExpiresAt: DateTime.now().add(const Duration(minutes: 30)),
          user: _user(),
        ),
      );
}

MyAreaDashboard _dashboard(String qrId) => MyAreaDashboard(
      identity: MyAreaIdentity(
        fullNameAr: 'رائد السالم',
        fullNameEn: 'Raed Al-Salem',
        qrId: qrId,
        tierNameEn: 'VIP',
        tierNameAr: 'VIP',
      ),
      counters: const MyAreaCounters(bookedSessionsCount: 0, meetingsCount: 0),
      todaySchedule: const <MyAreaScheduleItem>[],
    );

class _FakeMyAreaRepository implements MyAreaRepository {
  _FakeMyAreaRepository(this.dashboard);

  final MyAreaDashboard dashboard;

  @override
  Future<MyAreaDashboard> getDashboard() async => dashboard;

  @override
  Future<String> getContactCardVcf() async => '';

  @override
  Future<String> getCalendarIcs() async => '';

  @override
  Future<bool> uploadAvatar({
    required List<int> bytes,
    required String filename,
  }) async =>
      true;
}

/// The badge with an issued [qrId] and a loaded [referenceNumber]. Both must
/// be present and DIFFERENT: with a null reference number, "encode the
/// reference number" and "encode the qrId" are the same thing, and the test
/// would certify nothing.
Future<void> _pumpBadge(
  WidgetTester tester, {
  required String qrId,
  required String referenceNumber,
}) async {
  final router = GoRouter(
    initialLocation: '/badge',
    routes: <RouteBase>[
      GoRoute(
        name: RouteNames.badge,
        path: '/badge',
        builder: (c, s) => const BadgeScreen(),
      ),
      for (final (name, path) in <(String, String)>[
        (RouteNames.scanContact, '/contacts/scan'),
        (RouteNames.shareMyContact, '/contacts/share'),
        (RouteNames.scanVisitor, '/exhibitor/scan'),
        (RouteNames.myVisitors, '/exhibitor/visitors'),
        (RouteNames.home, '/'),
        (RouteNames.sessions, '/sessions'),
        (RouteNames.venueMap, '/map'),
        (RouteNames.myArea, '/my-area'),
        (RouteNames.signIn, '/sign-in'),
        (RouteNames.signUpForm, '/sign-up'),
      ])
        GoRoute(
          name: name,
          path: path,
          builder: (c, s) => const Scaffold(),
        ),
    ],
  );

  await tester.pumpWidget(
    simfTestScope(
      overrides: <Override>[
        authControllerProvider.overrideWith(_AuthController.new),
        myAreaRepositoryProvider
            .overrideWithValue(_FakeMyAreaRepository(_dashboard(qrId))),
        referenceNumberProvider.overrideWith((ref) async => referenceNumber),
      ],
      child: MaterialApp.router(
        routerConfig: router,
        locale: const Locale('en'),
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
}

/// Paints [qr] on its own and returns the raw RGBA bytes. Rendering it in a
/// bare boundary rather than reading the badge screen's own layer keeps the
/// comparison to the QR alone - the card around it is not what scans.
Future<Uint8List> _renderQr(
  WidgetTester tester,
  Widget qr,
  double side,
) async {
  final boundaryKey = GlobalKey();
  await tester.pumpWidget(
    MaterialApp(
      home: Center(
        child: RepaintBoundary(
          key: boundaryKey,
          child: SizedBox(width: side, height: side, child: qr),
        ),
      ),
    ),
  );
  await tester.pumpAndSettle();

  late Uint8List pixels;
  await tester.runAsync(() async {
    final render = boundaryKey.currentContext!.findRenderObject()!;
    final boundary = render as RenderRepaintBoundary;
    final image = await boundary.toImage();
    // Defaults to rawRgba - one byte per channel, no encoding in the way.
    final data = await image.toByteData();
    pixels = Uint8List.fromList(data!.buffer.asUint8List());
  });
  return pixels;
}

void main() {
  group('the badge QR is drawn with SQUARE modules (banked incident)', () {
    testWidgets(
        'its pixels equal a canonical square render of the same payload',
        (tester) async {
      await _pumpBadge(
        tester,
        qrId: 'OPAQUE123',
        referenceNumber: 'SIMF-2026-9876',
      );

      final rendered = tester.widget<QrImageView>(find.byType(QrImageView));
      final side = rendered.size!;
      // qr_flutter's defaults are square modules and square eyes, which is the
      // shape the gate scanner was proven against.
      final canonicalSquare = QrImageView(data: 'OPAQUE123', size: side);
      final roundedVariant = QrImageView(
        data: 'OPAQUE123',
        size: side,
        dataModuleStyle: const QrDataModuleStyle(
          dataModuleShape: QrDataModuleShape.circle,
          color: Colors.black,
        ),
      );

      final badgePixels = await _renderQr(tester, rendered, side);
      final squarePixels = await _renderQr(tester, canonicalSquare, side);
      final roundPixels = await _renderQr(tester, roundedVariant, side);

      expect(badgePixels, squarePixels);
      // Without this, the equality above could pass on a comparison blind to
      // module shape - which is the failure mode that let the round QR ship.
      expect(roundPixels, isNot(squarePixels));
    });

    testWidgets('the style fields that select drawRect stay square',
        (tester) async {
      await _pumpBadge(
        tester,
        qrId: 'OPAQUE123',
        referenceNumber: 'SIMF-2026-9876',
      );

      final rendered = tester.widget<QrImageView>(find.byType(QrImageView));
      // QrPainter branches on exactly these two: square -> drawRect, anything
      // else -> drawRRect with a full-module radius, i.e. round modules.
      expect(
        rendered.dataModuleStyle.dataModuleShape,
        QrDataModuleShape.square,
      );
      expect(rendered.eyeStyle.eyeShape, QrEyeShape.square);
    });
  });

  group('the badge QR encodes the opaque qrId, not the reference number', () {
    testWidgets('the QR takes the qrId while the strip prints the reference',
        (tester) async {
      await _pumpBadge(
        tester,
        qrId: 'OPAQUE123',
        referenceNumber: 'SIMF-2026-9876',
      );

      final card = tester.widget<BadgeQrCard>(find.byType(BadgeQrCard));
      // The scan value: what the gate resolves the attendee from.
      expect(card.qrId, 'OPAQUE123');
      // The printed value: the human reference, masked. Asserting both pins
      // the pair in BOTH directions, so swapping them cannot stay green.
      expect(card.maskedId, '•••• 9876');
      expect(find.text('ID · •••• 9876'), findsOneWidget);
    });

    testWidgets('rendering the qrId is what actually reaches the painter',
        (tester) async {
      // The widget field alone could be right while the QR itself was handed
      // something else, so drive the render too: a QR of the qrId and a QR of
      // the reference number are different pictures.
      await _pumpBadge(
        tester,
        qrId: 'OPAQUE123',
        referenceNumber: 'SIMF-2026-9876',
      );

      final rendered = tester.widget<QrImageView>(find.byType(QrImageView));
      final side = rendered.size!;

      final badgePixels = await _renderQr(tester, rendered, side);
      final ofQrId = await _renderQr(
        tester,
        QrImageView(data: 'OPAQUE123', size: side),
        side,
      );
      final ofReference = await _renderQr(
        tester,
        QrImageView(data: 'SIMF-2026-9876', size: side),
        side,
      );

      expect(badgePixels, ofQrId);
      expect(badgePixels, isNot(ofReference));
    });
  });
}
