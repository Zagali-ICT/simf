/// App API routes owned by the contacts feature.
///
/// The repository still owns its own paths (SIMF-MAA-001 sections 5, 6
/// and 9.1, decision D-545); naming them here keeps the literal off the
/// call site without making every feature depend on one shared file.
///
/// These paths are the shipped wire contract (D-219) and are copied
/// exactly. Changing one breaks installed builds.
abstract final class ContactsEndpoints {
  static const String shareToken = '/app/account/share-token';
  static const String rotateShareToken = '/app/account/share-token/rotate';
  static const String resolve = '/app/contacts/resolve';
  static const String save = '/app/contacts/save';
  static const String list = '/app/contacts';
  static String byId(String id) => '/app/contacts/$id';
  static String vcard(String id) => '/app/contacts/$id/vcard';
}
