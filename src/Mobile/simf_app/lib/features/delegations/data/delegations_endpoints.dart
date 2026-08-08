/// App API routes owned by the delegations feature.
///
/// The repository still owns its own paths (SIMF-MAA-001 sections 5, 6
/// and 9.1, decision D-545); naming them here keeps the literal off the
/// call site without making every feature depend on one shared file.
///
/// These paths are the shipped wire contract (D-219) and are copied
/// exactly. Changing one breaks installed builds.
abstract final class DelegationsEndpoints {
  static const String list = '/app/delegations';
  /// `countryId` is an `int` here, matching the repository's signature.
  static String availableSlots(int countryId) =>
      '/app/countries/$countryId/available-slots';
  static const String meetingRequests = '/app/delegation-meeting-requests';
  static String confirmMeeting(String requestId) => '/app/delegation-meeting-requests/$requestId/confirm';
  static String declineMeeting(String requestId) => '/app/delegation-meeting-requests/$requestId/decline';
}
