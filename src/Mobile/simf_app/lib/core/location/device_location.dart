import 'package:flutter_riverpod/flutter_riverpod.dart';

/// A single device fix — the latitude/longitude pair the hall-arrival endpoint
/// (`POST /app/sessions/{id}/arrival`) checks against the hall geofence. The
/// server never persists the raw coordinates; only the derived enter/leave
/// times are stored (FDS-003 §10, sensitive PII).
class DevicePosition {
  const DevicePosition({required this.lat, required this.lon});

  final double lat;
  final double lon;
}

/// Why a position could not be read. The caller renders a different message per
/// case, so an unavailable capability never reads as "you are outside the hall".
enum DeviceLocationOutcome {
  /// A fix was obtained — [DeviceLocationReading.position] is non-null.
  granted,

  /// The user (or the OS) refused the location permission.
  denied,

  /// This build has no location capability at all — the platform plugin is not
  /// yet part of the app (see [DeviceLocation]).
  unavailable,
}

/// The result of one location read: an outcome plus the fix when there is one.
class DeviceLocationReading {
  const DeviceLocationReading(this.outcome, [this.position]);

  final DeviceLocationOutcome outcome;
  final DevicePosition? position;
}

/// The device-location seam for the geofence self check-in (FR-305/506, D-241).
///
/// The default implementation reports [DeviceLocationOutcome.unavailable]: this
/// build ships **no** location plugin, and adding one is not a code decision —
/// it adds `ACCESS_FINE_LOCATION` to the Android manifest and an
/// `NSLocationWhenInUseUsageDescription` to `Info.plist`, both of which are
/// store-review and NCA-disclosure surface. The check-in action is written
/// against this seam so the whole path — request, wire call, every server
/// outcome, every rendered state — is built and tested now; supplying a real
/// reader later is a single [deviceLocationProvider] override and nothing else
/// changes.
///
/// Kept as a plain class (not an interface) to match the app's other
/// overridable seams, e.g. `SeatShare`.
class DeviceLocation {
  const DeviceLocation();

  Future<DeviceLocationReading> read() async =>
      const DeviceLocationReading(DeviceLocationOutcome.unavailable);
}

final deviceLocationProvider =
    Provider<DeviceLocation>((ref) => const DeviceLocation());
