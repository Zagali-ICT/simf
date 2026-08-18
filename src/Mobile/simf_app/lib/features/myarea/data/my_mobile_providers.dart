import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/features/account/data/profile_models.dart';
import 'package:simf_app/features/account/data/profile_repository.dart';

/// The signed-in user's profile, for the stored mobile number.
/// The My-mobile screen's OWN profile read — deliberately not the shared
/// `myProfileProvider` from `profile_repository.dart`.
///
/// The shared cache swallows an `ApiFailure` into null, which is right for the
/// selectors that read it (Badge, My Area, the speaker profile) and wrong here:
/// this screen shows the SERVER'S reason on a failed load, and null cannot
/// carry one. Main's own version of this screen read the repository directly
/// for the same reason.
///
/// The save still invalidates the SHARED cache as well, so the rest of the app
/// does not serve the pre-save row.
final myMobileProfileProvider = FutureProvider.autoDispose<UserProfileResponse>(
  (ref) => ref.watch(profileRepositoryProvider).getMyProfile(),
);
