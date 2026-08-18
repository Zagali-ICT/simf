import 'package:flutter/foundation.dart';
import 'package:simf_app/features/account/data/profile_models.dart';

/// The in-memory sign-up draft carried from the profile-data screen (Page 007)
/// to the interests screen (Page 007‑01), which adds the interests and fires
/// the single `POST /app/account/user-profile` save (D-332). [request] is built
/// with an empty `interestIds`; the interests screen replaces it via
/// `copyWith`.
@immutable
class SignUpProfileDraft {
  const SignUpProfileDraft({
    required this.request,
    this.idImageBytes,
    this.idImageName,
    this.faceImageBytes,
    this.faceImageName,
  });

  final UpsertUserProfileRequest request;

  /// The ID-document image (picked from the gallery) — uploaded to the
  /// id-image endpoint before the profile save. Mandatory for every
  /// registrant.
  final Uint8List? idImageBytes;
  final String? idImageName;

  /// The face photo (live capture) — uploaded as the avatar before the
  /// profile save. Mandatory for men, optional for women.
  final Uint8List? faceImageBytes;
  final String? faceImageName;
}
