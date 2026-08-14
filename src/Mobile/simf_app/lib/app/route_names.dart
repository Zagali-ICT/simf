/// The mockup screens as named go_router routes.
///
/// Names are stable — the router declares them once here; widgets and the
/// Phase 3 `mkp_*` screens reference `RouteNames.xxx` rather than literal
/// strings. The mapping screen-number → route-name follows `Mockup.html`.
///
/// §9 (D-276): mockup screens 08 (exhibitor self-sign-up) and 39 (cybersecurity
/// policy) are NOT exposed in the app — exhibitors are a Control-Panel concept
/// (D-199), so the app declares 39 numbered routes (the screen numbers keep
/// their mockup values; 08 and 39 are simply absent). Mockup screen 16 was
/// renamed Agenda → Sessions.
class RouteNames {
  RouteNames._();

  // Section 1 — Start & entry (11 screens; 08 exhibitor sign-up removed)
  static const String splash = 'splash';
  static const String onboarding = 'onboarding';
  static const String signIn = 'signIn';
  // signUpType removed (D-332) — the invented account-type screen is not in the
  // mockup; sign-up goes signIn → signUpForm directly.
  static const String signUpForm = 'signUpForm';
  static const String emailOtp = 'emailOtp';
  static const String signUpVisitor = 'signUpVisitor';
  // Page 007‑01 — the interests step, split out of signUpVisitor (D-332). Owns
  // the single profile-upsert save.
  static const String signUpInterests = 'signUpInterests';

  /// #14 — the standalone "My interests" edit surface, opened from My-Area. The
  /// same screen as [signUpInterests] rendered in edit mode.
  static const String myInterests = 'myInterests';

  /// Owner 2026-07-26 — the standalone "My mobile number" add / edit surface,
  /// opened from My-Area. Validation only: no OTP, no verification step.
  static const String myMobile = 'myMobile';
  static const String terms = 'terms';
  static const String registrationSuccess = 'registrationSuccess';
  static const String registrationStatus = 'registrationStatus';
  static const String guestMode = 'guestMode';

  // Section 2 — Core screens (8 screens)
  static const String home = 'home';
  static const String myArea = 'myArea';
  static const String venueMap = 'venueMap';
  // #9 — the venue map opened focused on a specific booth (the booth card's
  // "أرشدني" CTA). A pushed route (not the kept-alive map tab) so it loads a
  // fresh map that selects + centres the booth's node.
  static const String boothMap = 'boothMap';
  // §9 (D-276) — mockup screen 16 renamed Agenda → Sessions.
  static const String sessions = 'sessions';
  static const String sessionDetail = 'sessionDetail';
  static const String mySeat = 'mySeat';
  static const String speakers = 'speakers';
  static const String speakerProfile = 'speakerProfile';

  // Section 3 — Content & activities. D-499 (الوفود, Figma 1426:10771) restored
  // delegations (#21) as a real screen — the invited countries + their heads of
  // delegation, public (anonymous GET /app/delegations).
  static const String booths = 'booths';
  static const String sponsors = 'sponsors';
  static const String delegations = 'delegations';
  static const String archive = 'archive';

  // Wave 3 (Figma 1439:11881 / 11826) — the exhibitor + sponsor detail screens
  // (shared template), opened by tapping a booth / sponsor in their lists.
  static const String exhibitorDetail = 'exhibitorDetail';
  static const String sponsorDetail = 'sponsorDetail';

  // Section 4 — Live & Q&A (3 screens; 27 request-interview removed — D-278)
  static const String liveBroadcast = 'liveBroadcast';
  static const String sendQuestion = 'sendQuestion';

  // Section 5 — Media coverage (3 screens)
  static const String news = 'news';

  /// The single news article, opened from the news list or the home highlights
  /// carousel. Routed (not an imperative push) so it deep-links like every
  /// other detail screen and appears in the page index.
  static const String newsArticle = 'newsArticle';
  static const String gallery = 'gallery';
  static const String mediaPartners = 'mediaPartners';

  // Section 6 — Badge & notifications (2 screens)
  static const String badge = 'badge';
  static const String notifications = 'notifications';

  // Section 7 — Smart features (4 screens)
  static const String aiSummary = 'aiSummary';
  // #1/#6 — the session-summaries LIST (home tile → list → the aiSummary details
  // page). Additive, public (Guest+, like the summary it lists).
  static const String sessionSummaryList = 'sessionSummaryList';
  // #5 (D-710) — My sessions (عروض الجلسات, Figma 1388:9067): the attendee's
  // own booked/attended sessions. Restored + linked from the More menu (owner
  // reversed the D-609 removal).
  static const String myAreaSessions = 'myAreaSessions';
  static const String meetPeople = 'meetPeople';
  static const String chatbot = 'chatbot';
  static const String aboutForum = 'aboutForum';
  static const String aboutApp = 'aboutApp';

  // Section 8 — Settings & legal (3 screens; 39 cybersecurity removed)
  static const String accessibility = 'accessibility';
  static const String rate = 'rate';
  static const String more = 'more';

  // المزيد hub entries that have no screen yet (D-464; Figma 1129:17224) — they
  // render the ComingSoon placeholder until built. FAQ + presentations have
  // backend (D-218 / D-228); owner chose "parity now, ComingSoon for unbuilt".
  static const String forumGuide = 'forumGuide';
  static const String faq = 'faq';
  static const String sessionPresentations = 'sessionPresentations';
  static const String contactUs = 'contactUs';

  // D-500 (Wave 5, الطلبات 1408:9726) — the unified requests feed (reached from
  // My Area; Approved account). Supersedes the D-479 read-only My-meetings
  // route. Retitled "طلباتي" (My requests) once the meetings page split off
  // (D-745).
  static const String requests = 'requests';

  // D-745 (owner 2026-07-11) — the VIP bilateral-meetings page (اللقاءات
  // الثنائية, Figma 1408:9726), split from the requests-history feed above.
  // Reached from the Home VIP-only tile; the full history stays on [requests]
  // in My-Area.
  static const String meetings = 'meetings';

  // Bi-Meeting rework — the other-party confirm screen, reached by tapping a
  // "MeetingRequested" notification (deep-link /meeting-confirm?requestId=…).
  static const String meetingConfirm = 'meetingConfirm';

  // (D-609: myMeetings + myAreaSessions route names removed — their screens are
  // backed up as `.bk` and their routes + My-Area/More entry points are gone.)

  // (B18: bilateralMeetings + savedMeetings removed with routes 204 / 206 —
  //  ComingSoon sentinels with no screen and no caller left. The Home
  //  "اللقاءات الثنائية" tile opens the real VIP [meetings] page (D-745); the
  //  My-Area saved-meetings stat went with the D-609 screen deletion, which had
  //  already removed savedSessions the same way.)

  // FDS-014 visitor contact sharing (D-286 API; additive, not mockup-numbered).
  // Reached from More / My Area; all three require an Approved account.
  static const String shareMyContact = 'shareMyContact';
  static const String scanContact = 'scanContact';
  static const String myContacts = 'myContacts';
  // D-426 exhibitor ("Other") lead capture: scan a visitor badge → My Visitors.
  static const String scanVisitor = 'scanVisitor';
  static const String myVisitors = 'myVisitors';
  // Guided face-capture / liveness for the avatar (D-404; additive, reached from
  // My Area; signed-in + Approved).
  static const String identityVerification = 'identityVerification';
  // Moderator (محاور) per-session Q&A desk (D-405; additive, reached from a
  // session's detail; role-gated to Moderator+, server enforces per-session
  // grant).
  static const String sessionModerate = 'sessionModerate';
  // Staff gate-operator scanner (D-406; additive, drawer entry; role-gated to
  // Staff, server enforces the Gates.Operate grant).
  static const String gateScanner = 'gateScanner';
  // Staff walk-in visitor registration (D-509; additive, drawer entry;
  // role-gated to Staff, server enforces the Visitors.RegisterOnsite grant).
  // Figma 1467:12357.
  static const String staffRegisterVisitor = 'staffRegisterVisitor';
  // D-771 — the staff seating desk (owner 2026-07-26; additive, role-gated to
  // Staff, server enforces the Seating.Assist grant). Entered from the session
  // detail header when the signed-in user is Staff.
  static const String staffSeating = 'staffSeating';
  // D-485 — the in-app session-join flow (additive, approved Visitor): the seat
  // picker (assigned-seat sessions) + the standalone "Join a session" hub (the
  // owner's "both" entry — the other is the Join CTA on the session page).
  static const String seatPicker = 'seatPicker';
  static const String joinSessionHub = 'joinSessionHub';

  // Auxiliary auth routes (not numbered in the mockup but in API-001 §12)
  static const String forgotPassword = 'forgotPassword';
  static const String resetPassword = 'resetPassword';

  // Part B (D-430) — badge-QR sign-in / activation: scan the printed-badge QR
  // at login; a passwordless account sets its first password.
  static const String badgeSignIn = 'badgeSignIn';
  static const String badgeActivation = 'badgeActivation';
  // D-738 — the password step for a resolved has-password badge holder.
  static const String badgePassword = 'badgePassword';

  /// Visitor email-OTP second factor at sign-in (the app has no TOTP).
  static const String verifyOtp = 'verifyOtp';

  // #7a — emailed-OTP step-up confirming the user wants to ENABLE biometric
  // (Face-ID) sign-in. A pushed route reached from the Face-ID toggle (and the
  // post-sign-in enrol nudge); a signed-in approved caller. Backend
  // POST /app/auth/device-keys/step-up + the gated register.
  static const String biometricStepUp = 'biometricStepUp';

  /// S10 — the enrolled biometric device keys on the account, with a per-row
  /// revoke. Pushed from the Face-ID toggle's row in the profile / side menu.
  /// Backend GET /app/auth/device-keys + DELETE /app/auth/device-keys/{id}.
  /// Built to the house style, not to a Figma node: none exists, and the owner
  /// asked for the existing style rather than a new design (2026-08-14).
  static const String myDevices = 'myDevices';
}

/// The go_router path / query parameter KEYS (the names inside a route's `:param`
/// slot). Centralised next to [RouteNames] so the producers (`pushNamed`
/// pathParameters / queryParameters) and the router readers
/// (`state.pathParameters[...]` / `state.uri.queryParameters[...]`) reference one
/// const and can never drift; the string values are unchanged, so navigation
/// resolves identically. NOTE: these are NOT the JSON wire field names (D-219
/// frozen) — those live in the feature `data/` models and must not be touched.
class RouteParams {
  RouteParams._();

  static const String sessionId = 'sessionId';
  static const String speakerId = 'speakerId';
  static const String boothId = 'boothId';
  static const String sponsorId = 'sponsorId';
  static const String liveUrl = 'liveUrl';
  static const String newsId = 'newsId';
}
