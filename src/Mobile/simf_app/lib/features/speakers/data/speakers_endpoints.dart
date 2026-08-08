/// App API routes owned by the speakers feature.
///
/// The repository still owns its own paths (SIMF-MAA-001 sections 5, 6
/// and 9.1, decision D-545); naming them here keeps the literal off the
/// call site without making every feature depend on one shared file.
///
/// These paths are the shipped wire contract (D-219) and are copied
/// exactly. Changing one breaks installed builds.
abstract final class SpeakersEndpoints {
  static const String list = '/app/speakers';
  static String byId(String id) => '/app/speakers/$id';
  static String availableSlots(String speakerId) => '/app/speakers/$speakerId/available-slots';
  static String meetingRequests(String speakerId) => '/app/speakers/$speakerId/meeting-requests';
}
