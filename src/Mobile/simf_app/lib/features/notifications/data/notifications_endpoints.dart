/// App API routes owned by the notifications feature.
///
/// The repository still owns its own paths (SIMF-MAA-001 sections 5, 6
/// and 9.1, decision D-545); naming them here keeps the literal off the
/// call site without making every feature depend on one shared file.
///
/// These paths are the shipped wire contract (D-219) and are copied
/// exactly. Changing one breaks installed builds.
abstract final class NotificationsEndpoints {
  static const String unreadCount = '/app/account/notifications/unread-count';
  static const String list = '/app/account/notifications/list';
  static String markRead(String id) => '/app/account/notifications/$id/read';
  static const String markAllRead = '/app/account/notifications/read-all';
}
