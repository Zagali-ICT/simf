/// App API routes owned by the account feature.
///
/// The repository still owns its own paths (SIMF-MAA-001 sections 5, 6 and 9.1,
/// decision D-545); they are named here rather than written at the call site so
/// a route is stated once and is greppable. This is deliberately NOT a shared
/// cross-feature endpoint file: that would make every feature depend on a file
/// that changes whenever an unrelated feature changes.
///
/// These paths are the shipped wire contract (D-219). Installed builds call
/// them, so they are copied exactly and must not be reshaped.
abstract final class AccountEndpoints {
  static const String userProfile = '/app/account/user-profile';
  static const String countries = '/app/account/user-profile/countries';
  static const String profileTypes = '/app/account/profile-types';
  static const String interests = '/app/account/interests';
  static const String organisations = '/app/organisations';
  static const String idImage = '/app/account/user-profile/id-image';
  static const String avatar = '/app/account/avatar';
  static const String regions = '/app/regions';

  /// Another user's avatar bytes, by user id.
  static String avatarOf(String userId) => '/app/account/avatar/$userId';
}
