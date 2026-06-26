import 'package:flutter/widgets.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';

import '../core/widgets/coming_soon_screen.dart';
import '../features/auth/email_otp_verify_screen.dart';
import '../features/auth/badge_activation_screen.dart';
import '../features/auth/biometric_step_up_screen.dart';
import '../features/auth/badge_sign_in_screen.dart';
import '../features/auth/forgot_password_screen.dart';
import '../features/auth/reset_password_screen.dart';
import '../features/auth/sign_in_screen.dart';
import '../features/auth/sign_up_email_verify_screen.dart';
import '../features/auth/sign_up_form_screen.dart';
import '../features/about/about_screen.dart';
import '../features/archive/archive_screen.dart';
import '../features/booths/booths_screen.dart';
import '../features/content/terms_screen.dart';
import '../features/faq/faq_screen.dart';
import '../features/feedback/rate_screen.dart';
import '../features/forum_guide/forum_guide_screen.dart';
import '../features/gallery/gallery_screen.dart';
import '../features/home/home_screen.dart';
import '../features/media_partners/media_partners_screen.dart';
import '../features/news/news_screen.dart';
import '../features/accessibility/accessibility_screen.dart';
import '../features/ai_summary/session_summary_list_screen.dart';
import '../features/ai_summary/session_summary_screen.dart';
import '../features/badge/badge_screen.dart';
import '../features/chatbot/chatbot_screen.dart';
import '../features/comments/audience_comments_screen.dart';
import '../features/contact_us/contact_us_screen.dart';
import '../features/contacts/my_contacts_screen.dart';
import '../features/contacts/scan_contact_screen.dart';
import '../features/contacts/share_my_contact_screen.dart';
import '../features/guest/guest_mode_screen.dart';
import '../features/live/live_broadcast_screen.dart';
import '../features/meet/meet_people_screen.dart';
import '../features/more/more_screen.dart';
import '../features/notifications/notifications_screen.dart';
import '../features/questions/send_question_screen.dart';
import '../features/exhibitor/my_visitors_screen.dart';
import '../features/meetings/my_meetings_screen.dart';
import '../features/exhibitor/scan_visitor_screen.dart';
import '../features/gates/gate_scan_screen.dart';
import '../features/moderation/session_moderate_screen.dart';
import '../features/myarea/identity_verification_screen.dart';
import '../features/myarea/my_area_screen.dart';
import '../features/myarea/my_sessions_screen.dart';
import '../features/onboarding/onboarding_screen.dart';
import '../features/venuemap/venue_map_screen.dart';
import '../features/profile/data/profile_models.dart';
import '../features/profile/sign_up_interests_screen.dart';
import '../features/profile/sign_up_visitor_screen.dart';
import '../features/registration/registration_status_screen.dart';
import '../features/registration/registration_success_screen.dart';
import '../features/sessions/join_session_hub_screen.dart';
import '../features/sessions/my_seat_screen.dart';
import '../features/sessions/seat_picker_screen.dart';
import '../features/sessions/session_detail_screen.dart';
import '../features/sessions/session_presentations_screen.dart';
import '../features/sessions/sessions_screen.dart';
import '../features/sponsors/sponsors_screen.dart';
import '../features/speakers/speaker_profile_screen.dart';
import '../features/speakers/speakers_screen.dart';
import '../features/splash/splash_screen.dart';
import 'route_names.dart';

/// Holds the route metadata for one screen: the path, the route name, the
/// mockup screen number, and the Arabic + English label used by the
/// placeholder UI.
class _Route {
  const _Route({
    required this.number,
    required this.name,
    required this.path,
    required this.labelAr,
    required this.labelEn,
  });
  final int number;
  final String name;
  final String path;
  final String labelAr;
  final String labelEn;
}

/// The mockup screens declared once (39 exposed in-app; mockup screen 08
/// exhibitor sign-up + 39 cybersecurity are CP-only / removed — §9 / D-276).
const List<_Route> _routes = <_Route>[
  // Section 1 — Start & entry (11 screens; 08 exhibitor sign-up removed)
  _Route(number: 1, name: RouteNames.splash, path: '/splash', labelAr: 'البداية', labelEn: 'Splash'),
  _Route(number: 2, name: RouteNames.onboarding, path: '/onboarding', labelAr: 'التهيئة', labelEn: 'Onboarding'),
  _Route(number: 3, name: RouteNames.signIn, path: '/sign-in', labelAr: 'تسجيل الدخول', labelEn: 'Sign in'),
  // Screen 04 (sign-up — type) removed — invented; not in the mockup (D-332).
  _Route(number: 5, name: RouteNames.signUpForm, path: '/sign-up', labelAr: 'إنشاء حساب', labelEn: 'Sign up'),
  _Route(number: 6, name: RouteNames.emailOtp, path: '/sign-up/otp', labelAr: 'التحقق بالبريد', labelEn: 'Email verification'),
  _Route(number: 7, name: RouteNames.signUpVisitor, path: '/sign-up/visitor', labelAr: 'إنشاء حساب · زائر', labelEn: 'Sign up — profile'),
  // Page 007‑01 (interests) — mockup 5‑01; split out of #7 (D-332). Sentinel
  // number 701 so it never collides with a mockup screen number; auth-gated.
  _Route(number: 701, name: RouteNames.signUpInterests, path: '/sign-up/interests', labelAr: 'اهتماماتي', labelEn: 'My interests'),
  // Screen 08 (exhibitor self-sign-up) removed — exhibitors are CP-only (D-199 / §9).
  _Route(number: 9, name: RouteNames.terms, path: '/terms', labelAr: 'الشروط والأحكام', labelEn: 'Terms & conditions'),
  _Route(number: 10, name: RouteNames.registrationSuccess, path: '/registration/success', labelAr: 'تم التسجيل بنجاح', labelEn: 'Registration success'),
  _Route(number: 11, name: RouteNames.registrationStatus, path: '/registration/status', labelAr: 'حالة التسجيل', labelEn: 'Registration status'),
  _Route(number: 12, name: RouteNames.guestMode, path: '/guest', labelAr: 'وضع الضيف', labelEn: 'Guest mode'),

  // Section 2 — Core screens (8 screens)
  _Route(number: 13, name: RouteNames.home, path: '/', labelAr: 'الرئيسية', labelEn: 'Home'),
  _Route(number: 14, name: RouteNames.myArea, path: '/my-area', labelAr: 'الملف الشخصى', labelEn: 'Profile'),
  _Route(number: 15, name: RouteNames.venueMap, path: '/map', labelAr: 'الخريطة', labelEn: 'Venue map'),
  // §9 (D-276) — mockup screen 16 renamed Agenda → Sessions (route + path + label).
  _Route(number: 16, name: RouteNames.sessions, path: '/sessions', labelAr: 'الجلسات', labelEn: 'Sessions'),
  _Route(number: 17, name: RouteNames.sessionDetail, path: '/sessions/:sessionId', labelAr: 'تفاصيل الجلسة', labelEn: 'Session detail'),
  _Route(number: 18, name: RouteNames.mySeat, path: '/sessions/:sessionId/my-seat', labelAr: 'مقعدي', labelEn: 'My seat'),
  _Route(number: 19, name: RouteNames.speakers, path: '/speakers', labelAr: 'المتحدثون', labelEn: 'Speakers'),
  _Route(number: 20, name: RouteNames.speakerProfile, path: '/speakers/:speakerId', labelAr: 'القبطان البحري', labelEn: 'Speaker profile'),

  // Section 3 — Content & activities (3 screens; 21 delegations removed — D-277)
  _Route(number: 22, name: RouteNames.booths, path: '/booths', labelAr: 'الأجنحة', labelEn: 'Booths'),
  _Route(number: 23, name: RouteNames.sponsors, path: '/sponsors', labelAr: 'الرعاة', labelEn: 'Sponsors'),
  _Route(number: 24, name: RouteNames.archive, path: '/archive', labelAr: 'الأرشيف', labelEn: 'Archive'),

  // Section 4 — Live & Q&A (3 screens; 27 request-interview removed — D-278)
  _Route(number: 25, name: RouteNames.liveBroadcast, path: '/live', labelAr: 'البث المباشر', labelEn: 'Live broadcast'),
  _Route(number: 26, name: RouteNames.sendQuestion, path: '/live/question', labelAr: 'إرسال سؤال', labelEn: 'Send question'),
  _Route(number: 28, name: RouteNames.audienceComments, path: '/live/comments', labelAr: 'تعليقات الجمهور', labelEn: 'Audience comments'),

  // Section 5 — Media coverage (3 screens)
  _Route(number: 29, name: RouteNames.news, path: '/news', labelAr: 'الأخبار', labelEn: 'News'),
  _Route(number: 30, name: RouteNames.gallery, path: '/media', labelAr: 'معرض الصور والفيديوهات', labelEn: 'Media gallery'),
  _Route(number: 31, name: RouteNames.mediaPartners, path: '/media-partners', labelAr: 'الشركاء الإعلاميون', labelEn: 'Media partners'),

  // Section 6 — Badge & notifications (2 screens)
  _Route(number: 32, name: RouteNames.badge, path: '/badge', labelAr: 'بطاقة الدخول · QR', labelEn: 'Entry badge — QR'),
  _Route(number: 33, name: RouteNames.notifications, path: '/notifications', labelAr: 'الإشعارات', labelEn: 'Notifications'),

  // Section 7 — Smart features (4 screens)
  _Route(number: 34, name: RouteNames.aiSummary, path: '/ai-summary', labelAr: 'ملخص الجلسة بالذكاء الاصطناعي', labelEn: 'AI session summary'),
  _Route(number: 35, name: RouteNames.meetPeople, path: '/meet', labelAr: 'قابل أشخاص مثلك', labelEn: 'Meet people like you'),
  _Route(number: 36, name: RouteNames.chatbot, path: '/chatbot', labelAr: 'المساعد الذكي', labelEn: 'AI chatbot'),
  _Route(number: 37, name: RouteNames.aboutForum, path: '/about', labelAr: 'عن الملتقى — المحاور', labelEn: 'About the forum'),

  // Section 8 — Settings & legal (3 screens; 39 cybersecurity removed)
  _Route(number: 38, name: RouteNames.accessibility, path: '/settings/accessibility', labelAr: 'إمكانية الوصول', labelEn: 'Accessibility'),
  // Screen 39 (cybersecurity policy) removed from the app — §9 / D-276.
  _Route(number: 40, name: RouteNames.rate, path: '/rate', labelAr: 'تقييم', labelEn: 'Rate'),
  _Route(number: 41, name: RouteNames.more, path: '/more', labelAr: 'المزيد', labelEn: 'More'),

  // FDS-014 visitor contact sharing (D-286; additive, not mockup-numbered →
  // sentinel numbers 100+ so they never collide with mockup 1–41 or the aux 0s).
  _Route(number: 100, name: RouteNames.myContacts, path: '/contacts', labelAr: 'جهات اتصالي', labelEn: 'My Contacts'),
  _Route(number: 101, name: RouteNames.shareMyContact, path: '/contacts/share', labelAr: 'شارك جهة اتصالي', labelEn: 'Share my contact'),
  _Route(number: 102, name: RouteNames.scanContact, path: '/contacts/scan', labelAr: 'مسح رمز QR', labelEn: 'Scan QR'),
  _Route(number: 103, name: RouteNames.identityVerification, path: '/my-area/verify-identity', labelAr: 'التحقق من الهوية', labelEn: 'Identity verification'),
  _Route(number: 104, name: RouteNames.sessionModerate, path: '/sessions/:sessionId/moderate', labelAr: 'أسئلة الجلسة', labelEn: 'Session questions'),
  _Route(number: 105, name: RouteNames.gateScanner, path: '/gates/scan', labelAr: 'مسح البوابة', labelEn: 'Gate scanner'),
  // D-426 — exhibitor ("Other") lead capture (approved-only; server 403s visitors).
  _Route(number: 106, name: RouteNames.scanVisitor, path: '/exhibitor/scan', labelAr: 'مسح بطاقة زائر', labelEn: 'Scan visitor badge'),
  _Route(number: 107, name: RouteNames.myVisitors, path: '/exhibitor/visitors', labelAr: 'زواري', labelEn: 'My Visitors'),
  // D-479 (#11 follow-up) — read-only "My meetings" list (approved-only).
  _Route(number: 108, name: RouteNames.myMeetings, path: '/my-meetings', labelAr: 'اجتماعاتي', labelEn: 'My meetings'),
  // D-485 — the session-join flow (approved-only): the seat picker + the hub.
  _Route(number: 109, name: RouteNames.seatPicker, path: '/sessions/:sessionId/pick-seat', labelAr: 'اختر مقعدك', labelEn: 'Select your seat'),
  _Route(number: 110, name: RouteNames.joinSessionHub, path: '/sessions/join', labelAr: 'احجز مقعداً', labelEn: 'Book a seat'),
  // #1/#6 — session-summaries list (public; home tile → list → aiSummary details).
  _Route(number: 111, name: RouteNames.sessionSummaryList, path: '/session-summaries', labelAr: 'ملخص الجلسات', labelEn: 'Session summaries'),
  // #9 — venue map focused on a booth (booth "أرشدني" CTA; public, pushed).
  _Route(number: 112, name: RouteNames.boothMap, path: '/booths/:boothId/map', labelAr: 'الخريطة', labelEn: 'Venue map'),
  // Wave 2 (Figma 1388:9067) — "my sessions" (approved-only; My-Area counter).
  _Route(number: 113, name: RouteNames.myAreaSessions, path: '/my-area/sessions', labelAr: 'تفاصيل الجلسات', labelEn: 'Session details'),

  // D-464 — المزيد hub entries with no screen yet (Figma 1129:17224). Public;
  // they fall through to ComingSoonScreen (sentinel numbers 200+).
  _Route(number: 200, name: RouteNames.forumGuide, path: '/forum-guide', labelAr: 'دليل الملتقى', labelEn: 'Forum guide'),
  _Route(number: 201, name: RouteNames.faq, path: '/faq', labelAr: 'الأسئلة الشائعة', labelEn: 'FAQ'),
  _Route(number: 202, name: RouteNames.sessionPresentations, path: '/session-presentations', labelAr: 'عروض الجلسات', labelEn: 'Session presentations'),
  _Route(number: 203, name: RouteNames.contactUs, path: '/contact-us', labelAr: 'تواصل معنا', labelEn: 'Contact us'),
  // Owner batch (2026-06-21) — entry points for features not yet designed/built;
  // they fall through to ComingSoonScreen (sentinel numbers 200+). #5 bilateral
  // meetings (home tile, undesigned); #8 saved sessions/meetings (My Area stats).
  _Route(number: 204, name: RouteNames.bilateralMeetings, path: '/bilateral-meetings', labelAr: 'اللقاءات الثنائية', labelEn: 'Bilateral meetings'),
  _Route(number: 205, name: RouteNames.savedSessions, path: '/saved-sessions', labelAr: 'الجلسات المحفوظة', labelEn: 'Saved sessions'),
  _Route(number: 206, name: RouteNames.savedMeetings, path: '/saved-meetings', labelAr: 'المقابلات المحفوظة', labelEn: 'Saved meetings'),
];

/// Auxiliary auth routes that aren't numbered in the mockup but live in
/// API-001 §12 (forgot/reset password) and the TOTP step (§12.3).
const List<_Route> _auxRoutes = <_Route>[
  _Route(number: 0, name: RouteNames.forgotPassword, path: '/auth/forgot-password', labelAr: 'استعادة كلمة المرور', labelEn: 'Forgot password'),
  _Route(number: 0, name: RouteNames.resetPassword, path: '/auth/reset-password', labelAr: 'تعيين كلمة مرور جديدة', labelEn: 'Reset password'),
  _Route(number: 0, name: RouteNames.verifyOtp, path: '/auth/verify-otp', labelAr: 'رمز التحقق', labelEn: 'Verify OTP'),
  // Part B (D-430) — badge-QR sign-in / activation (anonymous, pre-login).
  _Route(number: 0, name: RouteNames.badgeSignIn, path: '/auth/badge', labelAr: 'الدخول بالشارة', labelEn: 'Badge sign-in'),
  _Route(number: 0, name: RouteNames.badgeActivation, path: '/auth/badge-activation', labelAr: 'تفعيل الحساب', labelEn: 'Activate account'),
  // #7a — emailed-OTP step-up to ENABLE biometric sign-in (signed-in; backend-
  // enforced, reached from the Face-ID toggle / post-sign-in nudge).
  _Route(number: 0, name: RouteNames.biometricStepUp, path: '/auth/biometric-step-up', labelAr: 'تأكيد بصمة الوجه', labelEn: 'Confirm Face ID'),
];

/// Screen numbers that need a signed-in user (Visitor or higher). Until
/// SIMF-RPM-001 closes (SIMF-MAA-001 OI-3) this list is conservative —
/// Phase 1 only gates the few obvious cases. Phase 2 / Phase 3 may extend.
const Set<int> _authenticatedRoutes = <int>{
  7, // Sign up — visitor profile data (AUTH-only, Page_007 L-1)
  701, // Sign up — interests + the single save (AUTH-only, Page_007-01, D-332)
  10, // Registration success (signed-in, pending; Page_010)
  11, // Registration status (signed-in, not-yet-approved gate; Page_011 L-1)
  14, // My area
  18, // My seat
  26, // Send question
  28, // Audience comments (approved-only, D-319)
  32, // Badge / QR
  33, // Notifications
  35, // Meet people
  40, // Rate (feedback — approved-only, D-310)
  100, // My Contacts (FDS-014, approved-only — D-286)
  101, // Share my contact (FDS-014, approved-only — D-286)
  102, // Scan contact QR (FDS-014, approved-only — D-286)
  103, // Identity verification — avatar liveness (D-404, from My Area)
  104, // Moderator session Q&A desk (D-405; also role-gated below)
  105, // Staff gate scanner (D-406; also role-gated below)
  106, // Exhibitor scan visitor badge (D-426; server 403s visitor-tier callers)
  107, // Exhibitor My Visitors (D-426)
  109, // Seat picker (D-485; approved-only — the seat endpoints 401/403 a guest)
  110, // Join-a-session hub (D-485; approved-only)
  113, // My sessions (Wave 2; approved-only — /app/account/sessions 401/403 a guest)
  202, // Session presentations (Wave 2; approved-only — /app/presentations 401/403 a guest)
};

/// Routes that additionally require a minimum app privilege (D-405/D-406). The
/// server is still the real authority (per-session grant / GateOperator role);
/// this is a UX gate so the wrong role never opens the screen. A signed-in user
/// whose role is below the minimum is redirected home.
const Map<int, AppRole> _roleGatedRoutes = <int, AppRole>{
  104: AppRole.moderator, // Session Q&A desk — moderator (or higher)
  105: AppRole.staff, // Gate scanner — staff
};

/// The five bottom-nav destinations, in reading order. They live inside a
/// persistent [StatefulShellRoute] (an IndexedStack) so switching between them
/// keeps the bottom bar fixed, swaps the body with **no page transition**, and
/// preserves each tab's state — instead of pushing a fresh page each tap (D-422,
/// the owner's "keep the button fixed, pages render inside" requirement).
const List<String> _tabRouteNames = <String>[
  RouteNames.home,
  RouteNames.sessions,
  RouteNames.badge,
  RouteNames.venueMap,
  RouteNames.myArea,
];

/// The screen for a numbered mockup route. Shared by the bottom-nav shell
/// branches and the flat (pushed) routes so both build identically.
Widget _screenFor(BuildContext context, GoRouterState state, _Route r) {
  // Page 001 (splash) is a real screen; every other route still renders the
  // ComingSoonScreen placeholder until it is built (SIMF-MAA-001 §12.1).
  if (r.name == RouteNames.splash) {
    return const SplashScreen();
  }
  if (r.name == RouteNames.onboarding) {
    return const OnboardingScreen();
  }
  if (r.name == RouteNames.signIn) {
    return const SignInScreen();
  }
  if (r.name == RouteNames.signUpForm) {
    return const SignUpFormScreen();
  }
  if (r.name == RouteNames.emailOtp) {
    return SignUpEmailVerifyScreen(
      email: state.uri.queryParameters['email'] ?? '',
    );
  }
  if (r.name == RouteNames.signUpVisitor) {
    return const SignUpVisitorScreen();
  }
  if (r.name == RouteNames.signUpInterests) {
    final extra = state.extra;
    return SignUpInterestsScreen(
      draft: extra is SignUpProfileDraft ? extra : null,
    );
  }
  if (r.name == RouteNames.terms) {
    // `?consent=1` shows the in-flow accept gate; standalone reads omit it.
    return TermsScreen(
      requireConsent: state.uri.queryParameters['consent'] == '1',
    );
  }
  if (r.name == RouteNames.registrationSuccess) {
    // D-373 — the interests screen passes the freshly issued registration
    // reference as the route extra.
    final extra = state.extra;
    return RegistrationSuccessScreen(
      referenceNumber: extra is String ? extra : null,
    );
  }
  if (r.name == RouteNames.registrationStatus) {
    return const RegistrationStatusScreen();
  }
  if (r.name == RouteNames.home) {
    return const HomeScreen();
  }
  if (r.name == RouteNames.myArea) {
    return const MyAreaScreen();
  }
  if (r.name == RouteNames.venueMap) {
    return const VenueMapScreen();
  }
  if (r.name == RouteNames.boothMap) {
    return VenueMapScreen(
      targetBoothId: state.pathParameters['boothId'],
    );
  }
  if (r.name == RouteNames.sessions) {
    return const SessionsScreen();
  }
  if (r.name == RouteNames.sessionDetail) {
    return SessionDetailScreen(
      sessionId: state.pathParameters['sessionId'] ?? '',
    );
  }
  if (r.name == RouteNames.mySeat) {
    return MySeatScreen(
      sessionId: state.pathParameters['sessionId'] ?? '',
    );
  }
  if (r.name == RouteNames.seatPicker) {
    return SeatPickerScreen(
      sessionId: state.pathParameters['sessionId'] ?? '',
    );
  }
  if (r.name == RouteNames.joinSessionHub) {
    return const JoinSessionHubScreen();
  }
  if (r.name == RouteNames.sessionSummaryList) {
    return const SessionSummaryListScreen();
  }
  if (r.name == RouteNames.speakers) {
    return const SpeakersScreen();
  }
  if (r.name == RouteNames.speakerProfile) {
    return SpeakerProfileScreen(
      speakerId: state.pathParameters['speakerId'] ?? '',
    );
  }
  if (r.name == RouteNames.booths) {
    return const BoothsScreen();
  }
  if (r.name == RouteNames.sponsors) {
    return const SponsorsScreen();
  }
  if (r.name == RouteNames.mediaPartners) {
    return const MediaPartnersScreen();
  }
  if (r.name == RouteNames.archive) {
    return const ArchiveScreen();
  }
  if (r.name == RouteNames.news) {
    return const NewsScreen();
  }
  if (r.name == RouteNames.gallery) {
    return const GalleryScreen();
  }
  if (r.name == RouteNames.aboutForum) {
    return const AboutScreen();
  }
  if (r.name == RouteNames.rate) {
    final q = state.uri.queryParameters;
    return RateScreen(
      code: q['code'],
      ratingTypeId: q['ratingTypeId'],
      targetId: q['targetId'],
    );
  }
  if (r.name == RouteNames.notifications) {
    return const NotificationsScreen();
  }
  if (r.name == RouteNames.meetPeople) {
    return const MeetPeopleScreen();
  }
  if (r.name == RouteNames.accessibility) {
    return const AccessibilityScreen();
  }
  if (r.name == RouteNames.more) {
    return const MoreScreen();
  }
  if (r.name == RouteNames.guestMode) {
    return const GuestModeScreen();
  }
  if (r.name == RouteNames.aiSummary) {
    return AiSummaryScreen(
      sessionId: state.uri.queryParameters['sessionId'],
    );
  }
  if (r.name == RouteNames.sendQuestion) {
    return SendQuestionScreen(
      sessionId: state.uri.queryParameters['sessionId'],
    );
  }
  if (r.name == RouteNames.audienceComments) {
    return AudienceCommentsScreen(
      sessionId: state.uri.queryParameters['sessionId'],
    );
  }
  if (r.name == RouteNames.liveBroadcast) {
    return LiveBroadcastScreen(
      sessionId: state.uri.queryParameters['sessionId'],
    );
  }
  if (r.name == RouteNames.badge) {
    return const BadgeScreen();
  }
  if (r.name == RouteNames.chatbot) {
    return const ChatbotScreen();
  }
  if (r.name == RouteNames.myContacts) {
    return const MyContactsScreen();
  }
  if (r.name == RouteNames.shareMyContact) {
    return const ShareMyContactScreen();
  }
  if (r.name == RouteNames.scanContact) {
    return const ScanContactScreen();
  }
  if (r.name == RouteNames.identityVerification) {
    return const IdentityVerificationScreen();
  }
  if (r.name == RouteNames.sessionModerate) {
    return SessionModerateScreen(
      sessionId: state.pathParameters['sessionId'] ?? '',
    );
  }
  if (r.name == RouteNames.gateScanner) {
    return const GateScanScreen();
  }
  if (r.name == RouteNames.scanVisitor) {
    return const ScanVisitorScreen();
  }
  if (r.name == RouteNames.myVisitors) {
    return const MyVisitorsScreen();
  }
  if (r.name == RouteNames.myMeetings) {
    return const MyMeetingsScreen();
  }
  if (r.name == RouteNames.forumGuide) {
    return const ForumGuideScreen();
  }
  if (r.name == RouteNames.faq) {
    return const FaqScreen();
  }
  if (r.name == RouteNames.contactUs) {
    return const ContactUsScreen();
  }
  if (r.name == RouteNames.sessionPresentations) {
    return const SessionPresentationsScreen();
  }
  if (r.name == RouteNames.myAreaSessions) {
    return const MySessionsScreen();
  }
  return ComingSoonScreen(
    screenNumber: r.number,
    screenLabelAr: r.labelAr,
    screenLabelEn: r.labelEn,
  );
}

/// The screen for an auxiliary auth route (forgot / reset / verify-OTP).
Widget _auxScreenFor(BuildContext context, GoRouterState state, _Route r) {
  if (r.name == RouteNames.forgotPassword) {
    return const ForgotPasswordScreen();
  }
  if (r.name == RouteNames.resetPassword) {
    return ResetPasswordScreen(
      email: state.uri.queryParameters['email'] ?? '',
    );
  }
  if (r.name == RouteNames.verifyOtp) {
    return const EmailOtpVerifyScreen();
  }
  if (r.name == RouteNames.biometricStepUp) {
    return const BiometricStepUpScreen();
  }
  // Part B (D-430) — badge-QR sign-in / activation.
  if (r.name == RouteNames.badgeSignIn) {
    return const BadgeSignInScreen();
  }
  if (r.name == RouteNames.badgeActivation) {
    final q = state.uri.queryParameters;
    return BadgeActivationScreen(
      qrId: q['qrId'] ?? '',
      needsEmail: q['needsEmail'] == '1',
      maskedEmail: q['masked'],
    );
  }
  return ComingSoonScreen(
    screenNumber: r.number,
    screenLabelAr: r.labelAr,
    screenLabelEn: r.labelEn,
  );
}

_Route _routeByName(String name) => _routes.firstWhere((r) => r.name == name);

/// Builds the go_router instance.
///
/// The redirect logic implements the auth gate (SIMF-MAA-001 §8): a request
/// for a protected route while signed out gets redirected to sign-in. The
/// router refreshes on every auth-state change ([refreshListenable]) so the
/// gate re-runs when the cold-start restore resolves or the session ends.
GoRouter buildRouter(Ref ref) {
  final authRefresh = _AuthRefreshNotifier(ref);

  return GoRouter(
    initialLocation: '/splash',
    refreshListenable: authRefresh,
    redirect: (context, state) {
      final authState = ref.read(authControllerProvider);
      final goingTo = state.matchedLocation;
      final isSignedIn = authState is AuthStateSignedIn;

      // The app no longer remembers the last screen to resume to on cold start
      // (D-431, owner request) — launch always lands on the splash → Home.

      return redirectDecision(
        isInitial: authState is AuthStateInitial,
        isSignedIn: isSignedIn,
        goingTo: goingTo,
        fullPath: state.fullPath,
        appRole: authState is AuthStateSignedIn
            ? authState.session.user.appRole
            : null,
      );
    },
    routes: <RouteBase>[
      // The five bottom-nav destinations share one persistent shell: an
      // IndexedStack of branches. Switching tabs swaps the visible branch with
      // no transition and keeps every tab's state alive — the bottom bar stays
      // fixed (each branch renders the same bar via KsaPage, so it never
      // animates). Sub-pages stay flat routes (pushed full-screen) below.
      StatefulShellRoute.indexedStack(
        builder: (context, state, navigationShell) => navigationShell,
        branches: <StatefulShellBranch>[
          for (final r in _tabRouteNames.map(_routeByName))
            StatefulShellBranch(
              routes: <RouteBase>[
                GoRoute(
                  name: r.name,
                  path: r.path,
                  builder: (context, state) => _screenFor(context, state, r),
                ),
              ],
            ),
        ],
      ),
      for (final r in _routes)
        if (!_tabRouteNames.contains(r.name))
          GoRoute(
            name: r.name,
            path: r.path,
            builder: (context, state) => _screenFor(context, state, r),
          ),
      for (final r in _auxRoutes)
        GoRoute(
          name: r.name,
          path: r.path,
          builder: (context, state) => _auxScreenFor(context, state, r),
        ),
    ],
  );
}

/// Bridges the Riverpod auth state to go_router's [Listenable]-based refresh:
/// every change to [authControllerProvider] re-runs the router's redirect, so
/// the cold-start restore resolving (and a later sign-out) is reflected in the
/// gate without a manual navigation.
class _AuthRefreshNotifier extends ChangeNotifier {
  _AuthRefreshNotifier(Ref ref) {
    ref.listen<AuthState>(authControllerProvider, (_, __) {
      notifyListeners();
    });
  }
}

/// The mockup screen number for a matched route **pattern** — go_router's
/// `state.fullPath` (e.g. `/sessions/:sessionId/my-seat`), or null. Matching the
/// pattern exactly (not a prefix of the concrete location) is what stops a
/// sub-route like My-seat (#18) from being shadowed by its parent
/// `/sessions/:sessionId` (#17) and silently losing its auth gate.
int? routeNumberForPath(String? fullPath) {
  if (fullPath == null) {
    return null;
  }
  for (final r in _routes) {
    if (r.path == fullPath) {
      return r.number;
    }
  }
  return null;
}

/// Whether the matched route pattern needs a signed-in user (the auth gate).
bool routePathRequiresAuth(String? fullPath) =>
    _authenticatedRoutes.contains(routeNumberForPath(fullPath));

/// The minimum app privilege a route pattern requires (D-405), or null when the
/// route has no role gate.
AppRole? requiredRoleForPath(String? fullPath) =>
    _roleGatedRoutes[routeNumberForPath(fullPath)];

/// The pure auth-gate redirect decision (testable in isolation, like
/// [routePathRequiresAuth]). Returns the path to redirect to, or null to allow
/// the navigation. [goingTo] is the matched location; [fullPath] is the matched
/// route *pattern*.
///
/// A signed-in user landing on `/sign-in` is intentionally **not** bounced here
/// (D-295). Post-sign-in routing belongs to `SignInScreen._routeAfterSignIn`,
/// which sends a profile-incomplete visitor to the profile screen (Page_007). A
/// blunt `/sign-in -> /` redirect fired on the auth-state change, disposed the
/// SignInScreen before its post-sign-in router could run, and stranded
/// incomplete profiles on Home.
String? redirectDecision({
  required bool isInitial,
  required bool isSignedIn,
  required String goingTo,
  required String? fullPath,
  AppRole? appRole,
}) {
  // Hold *protected* routes on the splash while the cold-start restore resolves
  // (Page_001 L-5 / D-295); the splash routes itself out to a public entry
  // screen once auth resolves, so a public route is never pinned here —
  // otherwise a stalled restore would strand the user on the splash (L-6).
  if (isInitial && goingTo != '/splash' && routePathRequiresAuth(fullPath)) {
    return '/splash';
  }
  // The auth gate (SIMF-MAA-001 §8): a protected route while signed out goes to
  // the sign-in entry.
  if (routePathRequiresAuth(fullPath) && !isSignedIn) {
    return '/sign-in';
  }
  // The role gate (D-405): a signed-in user whose privilege is below the route's
  // minimum is sent home. The server's per-session / GateOperator grant is the
  // real authority; this just keeps the wrong role out of the screen.
  final required = requiredRoleForPath(fullPath);
  if (isSignedIn &&
      required != null &&
      (appRole == null || !appRole.isAtLeast(required))) {
    return '/';
  }
  return null;
}

final routerProvider = Provider<GoRouter>(buildRouter);
