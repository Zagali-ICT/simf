/// App API routes owned by the venuemap feature.
///
/// The repository still owns its own paths (SIMF-MAA-001 sections 5, 6
/// and 9.1, decision D-545); naming them here keeps the literal off the
/// call site without making every feature depend on one shared file.
///
/// These paths are the shipped wire contract (D-219) and are copied
/// exactly. Changing one breaks installed builds.
abstract final class VenueMapEndpoints {
  static const String venueMap = '/app/venue-map';
  static const String booths = '/app/booths';
  static String boothById(String id) => '/app/booths/$id';
}
