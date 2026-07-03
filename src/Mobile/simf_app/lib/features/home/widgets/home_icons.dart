/// The home tile glyphs — the exact iconify SVGs from KSA frames 758:1134
/// (signed-in) and 758:2910 (guest); no 1:1 Material equivalent, bundled and
/// tinted to the tile colour. Shared by the guest + visitor home layouts.
class HomeIcons {
  const HomeIcons._();

  // "عن الملتقى" group (758:1216/1220/1224 + 1052:12856) — the exact Figma glyphs.
  static const String aboutSessions = 'assets/icons/home_about_sessions.svg';
  // ^ streamline-ultimate:team-meeting (node 1327:3446).
  static const String delegations =
      'assets/icons/home_delegations.svg'; // formkit:people (node 1408:10399)
  static const String booths = 'assets/icons/home_booths.svg'; // solar:chart
  static const String people = 'assets/icons/home_people.svg'; // bi:people
  static const String askModerator =
      'assets/icons/home_ask_moderator.svg'; // solar:user-outline
  // News + smart-feature tiles (758:1230/1234 + 758:1164/1170/1175/1179).
  static const String archive = 'assets/icons/home_archive.svg';
  static const String bilateral = 'assets/icons/home_bilateral.svg';
  static const String meetPeople = 'assets/icons/home_meet_people.svg';
  static const String aiAssistant = 'assets/icons/home_ai_assistant.svg';
  static const String sessionSummary = 'assets/icons/home_session_summary.svg';
  static const String badge = 'assets/icons/home_badge.svg';
  // Guest-home tile glyphs (frame 758:2910) — extracted line icons.
  static const String speakers = 'assets/icons/home_speakers.svg';
  static const String sessions = 'assets/icons/home_sessions.svg';
  static const String venueMap = 'assets/icons/home_map.svg';
  static const String exhibition = 'assets/icons/home_exhibition.svg';
}
