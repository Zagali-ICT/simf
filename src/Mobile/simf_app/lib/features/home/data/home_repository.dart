import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/features/myarea/data/myarea_models.dart';
import 'package:simf_app/features/myarea/data/myarea_repository.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// Best-effort signed-in profile for the greeting (the real name + avatar live
/// App-side on the dashboard, not in the Identity-issued auth token). Resolves
/// to null while loading / on error (e.g. a not-yet-approved 403), in which
/// case the greeting falls back to a name-less salute (never the email).
final homeProfileProvider =
    FutureProvider.autoDispose<MyAreaDashboard?>((ref) async {
  try {
    return await ref.watch(myAreaRepositoryProvider).getDashboard();
  } on ApiFailure {
    return null;
  }
});
