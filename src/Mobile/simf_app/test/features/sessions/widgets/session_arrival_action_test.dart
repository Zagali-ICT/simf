import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/core/location/device_location.dart';
import 'package:simf_app/features/sessions/data/hall_attendance_repository.dart';
import 'package:simf_app/features/sessions/widgets/session_arrival_action.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// `geofence-self-checkin` — the backend arrival/departure endpoints shipped
/// with D-241 but nothing in the app ever called them. These cover the whole
/// attendee half: the position report, every server outcome, and the rendered
/// state. Every case fails against the pre-fix tree (no such widget, no such
/// repository).
const String _sessionId = 's-1';

/// A location seam that answers with a fixed reading — no plugin, no platform
/// channel. The shipped default is [DeviceLocationOutcome.unavailable].
class _FixedLocation implements DeviceLocation {
  const _FixedLocation(this._reading);

  final DeviceLocationReading _reading;

  @override
  Future<DeviceLocationReading> read() async => _reading;
}

/// Records the calls the action makes and answers with a canned status or a
/// canned [ApiFailure].
class _FakeAttendance implements HallAttendanceRepository {
  _FakeAttendance({
    this.status = const HallAttendanceStatus(arrived: false),
    this.arrivalFailure,
  });

  final HallAttendanceStatus status;
  final ApiFailure? arrivalFailure;

  int arrivalCalls = 0;
  int departureCalls = 0;
  double? lastLat;
  double? lastLon;

  @override
  Future<HallAttendanceStatus> getStatus(String sessionId) async => status;

  @override
  Future<HallAttendanceStatus> recordArrival(
    String sessionId, {
    required double lat,
    required double lon,
  }) async {
    arrivalCalls++;
    lastLat = lat;
    lastLon = lon;
    final failure = arrivalFailure;
    if (failure != null) {
      throw failure;
    }
    return HallAttendanceStatus(
      arrived: true,
      enter: DateTime(2026, 11, 23, 10, 30),
      method: HallAttendanceMethod.geofence,
    );
  }

  @override
  Future<HallAttendanceStatus> recordDeparture(String sessionId) async {
    departureCalls++;
    return HallAttendanceStatus(
      arrived: false,
      enter: DateTime(2026, 11, 23, 10, 30),
      leave: DateTime.utc(2026, 11, 23, 8, 15),
      method: HallAttendanceMethod.geofence,
    );
  }
}

Future<void> _pumpAction(
  WidgetTester tester, {
  required _FakeAttendance repository,
  required DeviceLocationReading reading,
  Locale locale = const Locale('en'),
}) async {
  await tester.pumpWidget(
    ProviderScope(
      overrides: <Override>[
        hallAttendanceRepositoryProvider.overrideWithValue(repository),
        deviceLocationProvider.overrideWithValue(_FixedLocation(reading)),
      ],
      child: MaterialApp(
        locale: locale,
        supportedLocales: AppL10n.supportedLocales,
        localizationsDelegates: const <LocalizationsDelegate<dynamic>>[
          ...AppL10n.localizationsDelegates,
          GlobalMaterialLocalizations.delegate,
          GlobalWidgetsLocalizations.delegate,
          GlobalCupertinoLocalizations.delegate,
        ],
        home: Builder(
          builder: (context) => Scaffold(
            body: SessionArrivalAction(
              sessionId: _sessionId,
              l10n: AppL10n.of(context),
            ),
          ),
        ),
      ),
    ),
  );
  await tester.pumpAndSettle();
}

const DeviceLocationReading _atTheHall = DeviceLocationReading(
  DeviceLocationOutcome.granted,
  DevicePosition(lat: 24.7136, lon: 46.6753),
);

void main() {
  group('SessionArrivalAction — geofence self check-in (FR-305/506, D-241)',
      () {
    testWidgets('"I\'m here" posts the device position and renders the '
        'returned arrival', (tester) async {
      final repository = _FakeAttendance();
      await _pumpAction(
        tester,
        repository: repository,
        reading: _atTheHall,
      );

      expect(find.text("I'm here"), findsOneWidget);
      await tester.tap(find.text("I'm here"));
      await tester.pumpAndSettle();

      expect(repository.arrivalCalls, 1);
      expect(repository.lastLat, 24.7136);
      expect(repository.lastLon, 46.6753);
      // The returned status flips the action to the check-out affordance and
      // shows the recorded arrival time on the Saudi clock (07:30 UTC → 10:30).
      expect(find.text('Check out'), findsOneWidget);
      expect(
        find.textContaining('Your hall arrival is recorded · 10:30 AM'),
        findsOneWidget,
      );
    });

    testWidgets('an already-arrived session opens on the check-out action',
        (tester) async {
      final repository = _FakeAttendance(
        status: HallAttendanceStatus(
          arrived: true,
          enter: DateTime(2026, 11, 23, 10, 30),
          method: HallAttendanceMethod.geofence,
        ),
      );
      await _pumpAction(
        tester,
        repository: repository,
        reading: _atTheHall,
      );

      expect(find.text('Check out'), findsOneWidget);
      expect(find.text("I'm here"), findsNothing);

      await tester.tap(find.text('Check out'));
      await tester.pumpAndSettle();

      expect(repository.departureCalls, 1);
      expect(find.text("I'm here"), findsOneWidget);
    });

    testWidgets('a hall with no boundary reads as "not configured", never as '
        'an error (inert until the CP sets one)', (tester) async {
      final repository = _FakeAttendance(
        arrivalFailure: const ApiFailure(
          code: HallAttendanceRepository.hallGeofenceNotConfigured,
          message: 'This hall has no geofence.',
          httpStatus: 400,
        ),
      );
      await _pumpAction(
        tester,
        repository: repository,
        reading: _atTheHall,
      );

      await tester.tap(find.text("I'm here"));
      await tester.pumpAndSettle();

      expect(
        find.text('This hall has no boundary configured yet.'),
        findsOneWidget,
      );
      // No arrival was recorded, so the CTA stays.
      expect(find.text("I'm here"), findsOneWidget);
    });

    testWidgets('standing outside the radius says so', (tester) async {
      final repository = _FakeAttendance(
        arrivalFailure: const ApiFailure(
          code: HallAttendanceRepository.notAtVenue,
          message: 'You are not inside the hall yet.',
          httpStatus: 403,
        ),
      );
      await _pumpAction(
        tester,
        repository: repository,
        reading: _atTheHall,
      );

      await tester.tap(find.text("I'm here"));
      await tester.pumpAndSettle();

      expect(
        find.text('You are outside the hall boundary. Move closer and try again.'),
        findsOneWidget,
      );
    });

    testWidgets('any other server refusal shows the server message verbatim',
        (tester) async {
      final repository = _FakeAttendance(
        arrivalFailure: const ApiFailure(
          code: HallAttendanceRepository.sessionNotLive,
          message: 'This session is not open for arrivals right now.',
          httpStatus: 409,
        ),
      );
      await _pumpAction(
        tester,
        repository: repository,
        reading: _atTheHall,
      );

      await tester.tap(find.text("I'm here"));
      await tester.pumpAndSettle();

      expect(
        find.text('This session is not open for arrivals right now.'),
        findsOneWidget,
      );
    });

    testWidgets('no location capability never posts a position, and never '
        'reads as "outside the hall"', (tester) async {
      final repository = _FakeAttendance();
      await _pumpAction(
        tester,
        repository: repository,
        reading: const DeviceLocationReading(
          DeviceLocationOutcome.unavailable,
        ),
      );

      await tester.tap(find.text("I'm here"));
      await tester.pumpAndSettle();

      expect(repository.arrivalCalls, 0);
      expect(
        find.text('Location permission is required to check in.'),
        findsOneWidget,
      );
    });

    testWidgets('a refused location permission is reported, not swallowed',
        (tester) async {
      final repository = _FakeAttendance();
      await _pumpAction(
        tester,
        repository: repository,
        reading: const DeviceLocationReading(DeviceLocationOutcome.denied),
      );

      await tester.tap(find.text("I'm here"));
      await tester.pumpAndSettle();

      expect(repository.arrivalCalls, 0);
      expect(
        find.text('Location permission is required to check in.'),
        findsOneWidget,
      );
    });

    testWidgets('the Arabic action renders the Arabic wording', (tester) async {
      final repository = _FakeAttendance();
      await _pumpAction(
        tester,
        repository: repository,
        reading: _atTheHall,
        locale: const Locale('ar'),
      );

      expect(find.text('أنا هنا'), findsOneWidget);
    });
  });

  group('HallAttendanceStatus wire decoding', () {
    test('decodes the arrival envelope, method int and all', () {
      final status = HallAttendanceStatus.fromJson(<String, dynamic>{
        'arrived': true,
        'enter': '2026-11-23T07:30:00Z',
        'leave': null,
        'method': 1,
      });

      expect(status.arrived, isTrue);
      expect(status.enter, DateTime(2026, 11, 23, 10, 30));
      expect(status.leave, isNull);
      expect(status.method, HallAttendanceMethod.geofence);
    });

    test('accepts the method by name as well as by int', () {
      expect(
        HallAttendanceStatus.fromJson(<String, dynamic>{'method': 'QrScan'})
            .method,
        HallAttendanceMethod.qrScan,
      );
    });

    test('an absent / unknown method degrades to null rather than throwing',
        () {
      expect(
        HallAttendanceStatus.fromJson(const <String, dynamic>{}).method,
        isNull,
      );
      expect(
        HallAttendanceStatus.fromJson(<String, dynamic>{'method': 99}).method,
        isNull,
      );
    });
  });
}
