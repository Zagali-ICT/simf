import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/features/account/data/profile_models.dart';
import 'package:simf_app/features/account/data/profile_repository.dart';

/// The interests lookup, plus the current profile in #14 EDIT mode.
@immutable
class InterestsSetup {
  const InterestsSetup({required this.interests, this.profile});

  final List<InterestItem> interests;

  /// Non-null only in edit mode, where the save is a lossless full re-POST and
  /// so needs the profile it is re-sending.
  final UserProfileResponse? profile;
}

/// Keyed on `editMode` because the two modes fetch DIFFERENT things: edit mode
/// needs the profile as well, sign-up only the lookup. The third mode — no
/// draft and not editing — never watches this at all.
final interestsSetupProvider =
    FutureProvider.autoDispose.family<InterestsSetup, bool>((ref, edit) async {
  final repo = ref.watch(profileRepositoryProvider);
  final profile = edit ? await repo.getMyProfile() : null;
  final interests = await repo.getInterests();
  return InterestsSetup(interests: interests, profile: profile);
});
