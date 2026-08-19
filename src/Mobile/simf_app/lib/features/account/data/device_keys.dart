import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';

/// The enrolled device keys, plus which one is THIS device.
@immutable
class DeviceKeyList {
  const DeviceKeyList({required this.devices, required this.localDeviceKeyId});

  final List<DeviceKeyEntryDto> devices;

  /// The id this device enrolled under, so the list can mark its own row.
  final String? localDeviceKeyId;
}

/// The two reads and the sort that turns them into what the screen renders.
///
/// Active first, then most recently added. A revoked row still shows, because
/// "this device was removed on the 3rd" is the useful half of an audit trail
/// the user can actually read.
final deviceKeysProvider = FutureProvider.autoDispose<DeviceKeyList>(
  (ref) async {
    final notifier = ref.watch(authControllerProvider.notifier);
    final devices = await notifier.listDeviceKeys();
    final localId = await notifier.enrolledDeviceKeyId();
    return DeviceKeyList(
      devices: devices.toList()
        ..sort((a, b) {
          if (a.isActive != b.isActive) {
            return a.isActive ? -1 : 1;
          }
          final left = b.createdAt ?? DateTime(0);
          final right = a.createdAt ?? DateTime(0);
          return left.compareTo(right);
        }),
      localDeviceKeyId: localId,
    );
  },
);
