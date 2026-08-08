/// App API routes owned by the staff feature.
///
/// The repository still owns its own paths (SIMF-MAA-001 sections 5, 6
/// and 9.1, decision D-545); naming them here keeps the literal off the
/// call site without making every feature depend on one shared file.
///
/// These paths are the shipped wire contract (D-219) and are copied
/// exactly. Changing one breaks installed builds.
abstract final class StaffEndpoints {
  static const String registerOnsite = '/app/staff/visitors/register-onsite';
  static String visitorIdDocument(String userId) => '/app/staff/visitors/$userId/id-document';
  static String visitorAvatar(String userId) => '/app/staff/visitors/$userId/avatar';
  static String seatingByBadge(String sessionId) => '/app/staff/sessions/$sessionId/seating/by-badge';
  static String seatingSeat(String sessionId) => '/app/staff/sessions/$sessionId/seating/seat';
  static String seatingOccupantPhoto(String sessionId, String userId) => '/app/staff/sessions/$sessionId/seating/occupant/$userId/photo';
}
