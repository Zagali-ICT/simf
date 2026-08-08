/// App API routes owned by the sessions feature.
///
/// The repository still owns its own paths (SIMF-MAA-001 sections 5, 6
/// and 9.1, decision D-545); naming them here keeps the literal off the
/// call site without making every feature depend on one shared file.
///
/// These paths are the shipped wire contract (D-219) and are copied
/// exactly. Changing one breaks installed builds.
abstract final class SessionsEndpoints {
  static const String programme = '/app/programme/sessions';
  static const String programmeDays = '/app/programme/days';
  static String detail(String sessionId) => '/app/programme/sessions/$sessionId';
  static String attendance(String sessionId) => '/app/sessions/$sessionId/attendance';
  static String arrival(String sessionId) => '/app/sessions/$sessionId/arrival';
  static String departure(String sessionId) => '/app/sessions/$sessionId/departure';
  static const String presentations = '/app/presentations';
  static String presentationFile(String presentationId) => '/app/presentations/$presentationId/file';
  static String seats(String sessionId) => '/app/sessions/$sessionId/seats';
  static String reserveSeat(String sessionId) => '/app/sessions/$sessionId/seats/reserve';
  static String reserveRandomSeat(String sessionId) => '/app/sessions/$sessionId/seats/reserve-random';
  static String joinSeat(String sessionId) => '/app/sessions/$sessionId/seats/join';
  static String moveSeat(String sessionId) => '/app/sessions/$sessionId/seats/move';
  static String mySeat(String sessionId) => '/app/sessions/$sessionId/seats/mine';
  static const String favourites = '/app/sessions/favourites';
  static String favourite(String sessionId) => '/app/sessions/$sessionId/favourite';
}
