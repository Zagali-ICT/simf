import 'package:simf_app/app/theme/app_assets.dart';

/// The home tile glyphs — the exact iconify SVGs from KSA frames 758:1134
/// (signed-in) and 758:2910 (guest); no 1:1 Material equivalent, bundled and
/// tinted to the tile colour. Shared by the guest + visitor home layouts.
class HomeIcons {
  const HomeIcons._();

  // "عن الملتقى" group (758:1216/1220/1224 + 1052:12856) — the exact Figma glyphs.
  static const String aboutSessions = AppAssets.homeAboutSessions;
  // ^ streamline-ultimate:team-meeting (node 1327:3446).
  static const String delegations =
      AppAssets.homeDelegations; // formkit:people (node 1408:10399)
  static const String booths = AppAssets.homeBooths; // solar:chart
  static const String people = AppAssets.homePeople; // bi:people
  static const String askModerator =
      AppAssets.homeAskModerator; // solar:user-outline
  // News + smart-feature tiles (758:1230/1234 + 758:1164/1170/1175/1179).
  static const String archive = AppAssets.homeArchive;
  static const String bilateral = AppAssets.homeBilateral;
  static const String meetPeople = AppAssets.homeMeetPeople;
  static const String aiAssistant = AppAssets.homeAiAssistant;
  static const String sessionSummary = AppAssets.homeSessionSummary;
  static const String badge = AppAssets.homeBadge;
  // Guest-home tile glyphs (frame 758:2910) — extracted line icons.
  static const String speakers = AppAssets.homeSpeakers;
  static const String sessions = AppAssets.homeSessions;
  static const String venueMap = AppAssets.homeMap;
  static const String exhibition = AppAssets.homeExhibition;
}
