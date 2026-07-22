import 'package:flutter/widgets.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';

import '../core/widgets/coming_soon_screen.dart';
import 'widgets/simf_app_shell.dart';
import '../features/account/email_otp_verify_screen.dart';
import '../features/account/badge_activation_screen.dart';
import '../features/account/badge_password_screen.dart';
import '../features/account/biometric_step_up_screen.dart';
import '../features/account/change_email_screen.dart';
import '../features/account/badge_sign_in_screen.dart';
import '../features/account/forgot_password_screen.dart';
import '../features/account/reset_password_screen.dart';
import '../features/account/sign_in_screen.dart';
import '../features/account/sign_up_email_verify_screen.dart';
import '../features/account/sign_up_form_screen.dart';
import '../features/about/about_app_screen.dart';
import '../features/about/about_screen.dart';
import '../features/archive/archive_screen.dart';
import '../features/booths/booths_screen.dart';
import '../features/booths/exhibitor_detail_screen.dart';
import '../features/content/terms_screen.dart';
import '../features/delegations/delegations_screen.dart';
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
import '../features/meetings/meeting_confirm_screen.dart';
import '../features/meetings/meetings_screen.dart';
import '../features/requests/requests_screen.dart';
import '../features/exhibitor/scan_visitor_screen.dart';
import '../features/gates/gate_scan_screen.dart';
import '../features/moderation/session_moderate_screen.dart';
import '../features/myarea/identity_verification_screen.dart';
import '../features/myarea/my_area_screen.dart';
import '../features/myarea/my_sessions_screen.dart';
import '../features/onboarding/onboarding_screen.dart';
import '../features/venuemap/venue_map_screen.dart';
import '../features/account/data/profile_models.dart';
import '../features/account/sign_up_interests_screen.dart';
import '../features/account/sign_up_visitor_screen.dart';
import '../features/registration/registration_status_screen.dart';
import '../features/registration/registration_success_screen.dart';
import '../features/sessions/join_session_hub_screen.dart';
import '../features/sessions/my_seat_screen.dart';
import '../features/sessions/seat_picker_screen.dart';
import '../features/sessions/session_detail_screen.dart';
import '../features/sessions/session_presentations_screen.dart';
import '../features/sessions/sessions_screen.dart';
import '../features/staff/register_visitor_screen.dart';
import '../features/sponsors/sponsor_detail_screen.dart';
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
  // #14 — the standalone "My interests" EDIT surface (opened from My-Area); the
  // same interests page in edit mode. Sentinel 702 (never collides with a
  // mockup screen number); auth-gated.
  _Route(number: 702, name: RouteNames.myInterests, path: '/my-area/interests', labelAr: 'اهتماماتي', labelEn: 'My interests'),
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

  // Section 3 — Content & activities. D-499 restored delegations (#21).
  _Route(number: 21, name: RouteNames.delegations, path: '/delegations', labelAr: 'الوفود', labelEn: 'Delegations'),
  _Route(number: 22, name: RouteNames.booths, path: '/booths', labelAr: 'الأجنحة', labelEn: 'Booths'),
  _Route(number: 23, name: RouteNames.sponsors, path: '/sponsors', labelAr: 'الرعاة', labelEn: 'Sponsors'),
  // Wave 3 (Figma 1439:11881 / 11826) — exhibitor + sponsor detail (public, pushed).
  _Route(number: 220, name: RouteNames.exhibitorDetail, path: '/exhibitors/:boothId', labelAr: 'العارض', labelEn: 'Exhibitor'),
  _Route(number: 221, name: RouteNames.sponsorDetail, path: '/sponsors/:sponsorId', labelAr: 'الراعي', labelEn: 'Sponsor'),
  _Route(number: 24, name: RouteNames.archive, path: '/archive', labelAr: 'الأرشيف', labelEn: 'Archive'),

  // Section 4 — Live & Q&A (3 screens; 27 request-interview removed — D-278)
  _Route(number: 25, name: RouteNames.liveBroadcast, path: '/live', labelAr: 'البث المباشر', labelEn: 'Live broadcast'),
  _Route(number: 26, name: RouteNames.sendQuestion, path: '/live/question', labelAr: 'إرسال سؤال', labelEn: 'Send question'),

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
  // D-509 — staff walk-in visitor registration (approved Staff; server enforces
  // Visitors.RegisterOnsite). Figma 1467:12357.
  _Route(number: 114, name: RouteNames.staffRegisterVisitor, path: '/staff/register-visitor', labelAr: 'تسجيل زائر', labelEn: 'Register visitor'),
  // D-500 (Wave 5, الطلبات) — the unified requests feed (approved-only), retitled
  // "طلباتي" once the meetings page split off (D-745).
  _Route(number: 108, name: RouteNames.requests, path: '/requests', labelAr: 'طلباتي', labelEn: 'My requests'),
  // D-745 — the VIP bilateral-meetings page (اللقاءات الثنائية, Figma 1408:9726).
  _Route(number: 116, name: RouteNames.meetings, path: '/meetings', labelAr: 'اللقاءات الثنائية', labelEn: 'Bilateral meetings'),
  // Bi-Meeting rework — the other-party confirm screen (deep-link from a notification).
  _Route(number: 117, name: RouteNames.meetingConfirm, path: '/meeting-confirm', labelAr: 'تأكيد الاجتماع', labelEn: 'Confirm meeting'),
  // (D-609: route 115 My-meetings removed — screen backed up as `.bk`.)
  // D-485 — the session-join flow (approved-only): the seat picker + the hub.
  _Route(number: 109, name: RouteNames.seatPicker, path: '/sessions/:sessionId/pick-seat', labelAr: 'اختر مقعدك', labelEn: 'Select your seat'),
  _Route(number: 110, name: RouteNames.joinSessionHub, path: '/sessions/join', labelAr: 'احجز مقعداً', labelEn: 'Book a seat'),
  // #1/#6 — session-summaries list (public; home tile → list → aiSummary details).
  _Route(number: 111, name: RouteNames.sessionSummaryList, path: '/session-summaries', labelAr: 'ملخص الجلسات', labelEn: 'Session summaries'),
  // #9 — venue map focused on a booth (booth "أرشدني" CTA; public, pushed).
  _Route(number: 112, name: RouteNames.boothMap, path: '/booths/:boothId/map', labelAr: 'الخريطة', labelEn: 'Venue map'),
  // #5 (D-710) — My sessions (عروض الجلسات, Figma 1388:9067), approved-attendee;
  // restored + linked from the More menu (owner reversed the D-609 removal).
  _Route(number: 113, name: RouteNames.myAreaSessions, path: '/my-sessions', labelAr: 'عروض الجلسات', labelEn: 'My sessions'),

  // D-464 — المزيد hub entries with no screen yet (Figma 1129:17224). Public;
  // they fall through to ComingSoonScreen (sentinel numbers 200+).
  _Route(number: 200, name: RouteNames.forumGuide, path: '/forum-guide', labelAr: 'دليل الملتقى', labelEn: 'Forum guide'),
  _Route(number: 201, name: RouteNames.faq, path: '/faq', labelAr: 'الأسئلة الشائعة', labelEn: 'FAQ'),
  _Route(number: 202, name: RouteNames.sessionPresentations, path: '/session-presentations', labelAr: 'الجلسات', labelEn: 'Sessions'),
  _Route(number: 203, name: RouteNames.contactUs, path: '/contact-us', labelAr: 'تواصل معنا', labelEn: 'Contact us'),
  // D-668 — About-the-app page (version / release date / organizer + links),
  // reached from the end of the side drawer. Public.
  _Route(number: 207, name: RouteNames.aboutApp, path: '/about-app', labelAr: 'عن التطبيق', labelEn: 'About the app'),
  // Owner batch (2026-06-21) — entry points for features not yet designed/built;
  // they fall through to ComingSoonScreen (sentinel numbers 200+). #5 bilateral
  // meetings (home tile, undesigned); #8 saved meetings (My Area stat).
  // (D-609: route 205 Saved-sessions removed — screen backed up as `.bk`.)
  _Route(number: 204, name: RouteNames.bilateralMeetings, path: '/bilateral-meetings', labelAr: 'اللقاءات الثنائية', labelEn: 'Bilateral meetings'),
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
  // D-738 — the password step after a has-password badge resolves.
  _Route(number: 0, name: RouteNames.badgePassword, path: '/auth/badge-password', labelAr: 'إكمال تسجيل الدخول', labelEn: 'Badge password'),
  // #7a — emailed-OTP step-up to ENABLE biometric sign-in (signed-in; backend-
  // enforced, reached from the Face-ID toggle / post-sign-in nudge).
  _Route(number: 0, name: RouteNames.biometricStepUp, path: '/auth/biometric-step-up', labelAr: 'تأكيد بصمة الوجه', labelEn: 'Confirm Face ID'),
  // #24 — self-service change of the login email (signed-in; reached from More →
  // الإعدادات). OTP to the new address; confirm rolls the stamp → forced re-login.
  _Route(number: 0, name: RouteNames.changeEmail, path: '/auth/change-email', labelAr: 'تغيير البريد الإلكتروني', labelEn: 'Change email'),
];

/// Screen numbers that need a signed-in user of **any** role (including a
/// pending/unapproved account, which resolves to [AppRole.guest]). These are the
/// universal onboarding + account routes everyone signed-in shares; role-specific
/// pages live in [_routeRoles] instead (a route there is also auth-gated).
const Set<int> _authenticatedRoutes = <int>{
  7, // Sign up — visitor profile data (AUTH-only, Page_007 L-1)
  701, // Sign up — interests + the single save (AUTH-only, Page_007-01, D-332)
  702, // My interests — edit from My-Area (AUTH-only, #14)
  10, // Registration success (signed-in, pending; Page_010)
  11, // Registration status (signed-in, not-yet-approved gate; Page_011 L-1)
  14, // My area / profile — every signed-in role
  // D-750 (owner 2026-07-20) — REVERSES D-576: the agenda (Sessions, 16) and
  // session detail (17) are PUBLIC again, so a guest can browse the programme
  // and open a session without signing in (restoring the D-199 public design).
  // They are intentionally NOT in this set. The join / ask sections stay hidden
  // for a guest (session_detail_body only builds them when the seat map is
  // non-null, which needs an approved account), and My seat (18) stays
  // attendee-gated. Live (25) likewise stays public with an in-screen "need
  // login" prompt on the live screen itself (D-577), not a redirect.
  32, // Badge / QR — every signed-in role's own entry pass (a bottom-nav tab, so
  // it must not bounce for Staff/Moderator; the server returns their own badge)
  33, // Notifications — every signed-in role
  // D-694 (owner 2026-07-08) — face-capture / avatar-liveness moved here from the
  // attendee-only role gate. Since D-666 a pending sign-up account presents as
  // [AppRole.guest], so an attendee-gated 103 bounced EVERY sign-up user (all
  // pending) to Home the moment they tapped "capture face photo" — sign-up was
  // functionally broken. The screen is on-device only (camera + ML Kit, no
  // network) and `POST /app/account/avatar` is not role-gated, so any signed-in
  // account may safely reach it. This also fixes the staff/moderator My-Area
  // avatar-change dead-bounce.
  103, // Identity verification — avatar liveness (was attendee-gated, D-404)
};

/// The clean role→page model (D-519): the explicit set of [AppRole]s allowed to
/// open each role-restricted route. A route here is **also** auth-gated (it needs
/// sign-in). A signed-in user whose role is **not in the set** is redirected
/// home. The server stays the real authority (per-session grant / GateOperator /
/// Visitors.RegisterOnsite); this is the UX gate that keeps the wrong role out
/// of the screen AND lets the nav surfaces show only the role's own pages. This
/// replaced the old min-role `isAtLeast` ladder, which could not express
/// "Exhibitor = Visitor + extras" or "Staff/Moderator are focused, NOT a
/// visitor superset".
const Set<AppRole> _attendee = <AppRole>{AppRole.visitor, AppRole.exhibitor};
const Map<int, Set<AppRole>> _routeRoles = <int, Set<AppRole>>{
  // Attendee features — Visitor + Exhibitor (NOT Staff/Moderator: D-519 focused).
  18: _attendee, // My seat
  26: _attendee, // Send question
  35: _attendee, // Meet people
  40: _attendee, // Rate / feedback (D-310)
  100: _attendee, // My Contacts (FDS-014)
  101: _attendee, // Share my contact (FDS-014)
  102: _attendee, // Scan contact QR (FDS-014)
  // 103 (identity verification / avatar liveness) moved to _authenticatedRoutes —
  // it must be reachable by a pending sign-up account (D-694).
  108: _attendee, // Requests feed (D-500, approved-only)
  116: _attendee, // Bilateral meetings (D-745) — role gate keeps guest/staff/
  // moderator out; VIP-only is enforced in-screen + server-side, not here.
  117: _attendee, // Meeting confirm (Bi-Meeting) — the other-party confirm screen;
  // eligibility (target-delegation member) is enforced server-side.
  109: _attendee, // Seat picker (D-485)
  110: _attendee, // Join-a-session hub (D-485)
  113: _attendee, // My sessions (D-710, restored — owner reversed the D-609 removal)
  202: _attendee, // Session presentations (Wave 2)
  // (D-609: routes 115 My-meetings, 205 Saved-sessions removed — screens backed
  // up as `.bk`; 113 My-sessions restored by D-710.)
  // Exhibitor-only — lead capture (D-426).
  106: <AppRole>{AppRole.exhibitor}, // Scan visitor badge
  107: <AppRole>{AppRole.exhibitor}, // My Visitors
  // Staff-only — the gate operations (D-406 / D-509).
  105: <AppRole>{AppRole.staff}, // Gate scanner
  114: <AppRole>{AppRole.staff}, // Walk-in visitor registration
  // Moderator-only — the session Q&A desk (D-405). Moderator-EXCLUSIVE now
  // (D-519): Staff no longer inherits it (the old isAtLeast made Staff >= Moderator).
  104: <AppRole>{AppRole.moderator}, // Session Q&A desk
};

/// The five bottom-nav destinations. They render inside [SimfAppShell]'s
/// IndexedStack, not as separate GoRouter branches. Tab switching is purely
/// internal (no go_router) to avoid Flutter's `_debugCheckDuplicatedPageKeys`
/// assertion with `StatefulShellRoute`'s stable internal page keys.

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
  if (r.name == RouteNames.myInterests) {
    // #14 — the same interests page in EDIT mode: self-loads the profile,
    // pre-selects the saved interests, saves in place and pops back.
    return const SignUpInterestsScreen(editMode: true);
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
      targetBoothId: state.pathParameters[RouteParams.boothId],
    );
  }
  if (r.name == RouteNames.sessions) {
    return const SessionsScreen();
  }
  if (r.name == RouteNames.sessionDetail) {
    return SessionDetailScreen(
      sessionId: state.pathParameters[RouteParams.sessionId] ?? '',
    );
  }
  if (r.name == RouteNames.mySeat) {
    return MySeatScreen(
      sessionId: state.pathParameters[RouteParams.sessionId] ?? '',
    );
  }
  if (r.name == RouteNames.seatPicker) {
    return SeatPickerScreen(
      sessionId: state.pathParameters[RouteParams.sessionId] ?? '',
    );
  }
  if (r.name == RouteNames.joinSessionHub) {
    return const JoinSessionHubScreen();
  }
  if (r.name == RouteNames.sessionSummaryList) {
    return const SessionSummaryListScreen();
  }
  if (r.name == RouteNames.myAreaSessions) {
    return const MySessionsScreen();
  }
  if (r.name == RouteNames.speakers) {
    return const SpeakersScreen();
  }
  if (r.name == RouteNames.speakerProfile) {
    return SpeakerProfileScreen(
      speakerId: state.pathParameters[RouteParams.speakerId] ?? '',
    );
  }
  if (r.name == RouteNames.delegations) {
    return const DelegationsScreen();
  }
  if (r.name == RouteNames.booths) {
    return const BoothsScreen();
  }
  if (r.name == RouteNames.exhibitorDetail) {
    return ExhibitorDetailScreen(
      boothId: state.pathParameters[RouteParams.boothId] ?? '',
    );
  }
  if (r.name == RouteNames.sponsors) {
    return const SponsorsScreen();
  }
  if (r.name == RouteNames.sponsorDetail) {
    return SponsorDetailScreen(
      sponsorId: state.pathParameters[RouteParams.sponsorId] ?? '',
    );
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
  if (r.name == RouteNames.aboutApp) {
    return const AboutAppScreen();
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
      sessionId: state.uri.queryParameters[RouteParams.sessionId],
    );
  }
  if (r.name == RouteNames.sendQuestion) {
    return SendQuestionScreen(
      sessionId: state.uri.queryParameters[RouteParams.sessionId],
    );
  }
  if (r.name == RouteNames.liveBroadcast) {
    return LiveBroadcastScreen(
      sessionId: state.uri.queryParameters[RouteParams.sessionId],
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
      sessionId: state.pathParameters[RouteParams.sessionId] ?? '',
    );
  }
  if (r.name == RouteNames.gateScanner) {
    return const GateScanScreen();
  }
  if (r.name == RouteNames.staffRegisterVisitor) {
    return const StaffRegisterVisitorScreen();
  }
  if (r.name == RouteNames.scanVisitor) {
    return const ScanVisitorScreen();
  }
  if (r.name == RouteNames.myVisitors) {
    return const MyVisitorsScreen();
  }
  if (r.name == RouteNames.requests) {
    return const RequestsScreen();
  }
  if (r.name == RouteNames.meetings) {
    return const MeetingsScreen();
  }
  if (r.name == RouteNames.meetingConfirm) {
    return MeetingConfirmScreen(
      requestId: state.uri.queryParameters['requestId'] ?? '',
    );
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
  return ComingSoonScreen(
    screenNumber: r.number,
    screenLabelAr: r.labelAr,
    screenLabelEn: r.labelEn,
  );
}

/// The screen for an auxiliary auth route (forgot / reset / verify-OTP).
Widget _auxScreenFor(BuildContext context, GoRouterState state, _Route r) {
  if (r.name == RouteNames.forgotPassword) {
    // The email is pre-filled when a signed-in user opens this from their
    // profile (D-659); null/absent for the normal signed-out entry.
    return ForgotPasswordScreen(email: state.uri.queryParameters['email']);
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
  if (r.name == RouteNames.changeEmail) {
    return const ChangeEmailScreen();
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
  if (r.name == RouteNames.badgePassword) {
    final q = state.uri.queryParameters;
    return BadgePasswordScreen(
      qrId: q['qrId'] ?? '',
      displayName: q['name'],
      maskedEmail: q['masked'],
    );
  }
  return ComingSoonScreen(
    screenNumber: r.number,
    screenLabelAr: r.labelAr,
    screenLabelEn: r.labelEn,
  );
}

/// Incrementing counter so every page key is unique — Flutter 3.44.5's
/// `_debugCheckDuplicatedPageKeys` builds a reservation set from existing
/// overlay routes, and a static key (even a unique one) still collides when
/// `router.refresh()` triggers `Navigator.didUpdateWidget`.
var _pageKeyCounter = 0;

/// A page key that is unique for the lifetime of the process.
String _nextPageKey(String prefix) => '$prefix:${_pageKeyCounter++}';

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
        // A signed-in but not-yet-approved account presents as guest
        // (effectiveAppRole, D-666), so the role-gate keeps it out of the
        // attendee/approved routes exactly like the menus do — its only
        // signed-in destinations are the universal-auth routes (home, my-area,
        // badge, notifications, sessions, registration-status).
        appRole: authState is AuthStateSignedIn
            ? authState.session.user.effectiveAppRole
            : null,
      );
    },
    routes: <RouteBase>[
      // Shell route — replaces StatefulShellRoute.indexedStack. Renders
      // SimfAppShell with an IndexedStack of all five tabs. Tab switching is
      // purely internal (no go_router), so there is only ever ONE page in the
      // parent Navigator — no key-reservation collision on router.refresh().
      GoRoute(
        name: RouteNames.home,
        path: '/',
        pageBuilder: (context, state) => NoTransitionPage(
          key: ValueKey(_nextPageKey('shell')),
          child: const SimfAppShell(),
        ),
      ),
      // Flat (pushed) routes — every non-shell screen.
      for (final r in _routes)
        if (r.name != RouteNames.home)
          GoRoute(
            name: r.name,
            path: r.path,
            pageBuilder: (context, state) => NoTransitionPage(
              key: ValueKey(_nextPageKey('route:${r.name}')),
              child: _screenFor(context, state, r),
            ),
          ),
      for (final r in _auxRoutes)
        GoRoute(
          name: r.name,
          path: r.path,
          pageBuilder: (context, state) => NoTransitionPage(
            key: ValueKey(_nextPageKey('aux:${r.name}')),
            child: _auxScreenFor(context, state, r),
          ),
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

/// Whether the matched route pattern needs a signed-in user (the auth gate). A
/// route is auth-gated if it is in [_authenticatedRoutes] (universal signed-in)
/// OR in [_routeRoles] (role-restricted routes are signed-in by definition).
bool routePathRequiresAuth(String? fullPath) {
  final number = routeNumberForPath(fullPath);
  return _authenticatedRoutes.contains(number) ||
      _routeRoles.containsKey(number);
}

/// The set of [AppRole]s allowed on a route *pattern* (D-519), or null when the
/// route has no role restriction (public or universal-signed-in).
Set<AppRole>? allowedRolesForPath(String? fullPath) =>
    _routeRoles[routeNumberForPath(fullPath)];

/// The set of [AppRole]s allowed on a route by its **name** (D-519), or null
/// when the route has no role restriction.
Set<AppRole>? allowedRolesForRouteName(String name) {
  for (final r in _routes) {
    if (r.name == name) {
      return _routeRoles[r.number];
    }
  }
  return null;
}

/// Whether [role] may open/see the route [name] (D-519). A route with no role
/// restriction returns true for everyone (including a null/guest role); a
/// role-restricted route returns true only when [role] is in its allowed set.
/// The nav surfaces (home tiles, side drawer) use this so a role sees only its
/// own pages. (This is a role check only — auth/sign-in is handled separately.)
bool routeAllowsRole(String name, AppRole? role) {
  final allowed = allowedRolesForRouteName(name);
  if (allowed == null) {
    return true;
  }
  return role != null && allowed.contains(role);
}

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
  // The role gate (D-519): a signed-in user whose role is not in the route's
  // allowed set is sent home (Home is role-aware, so a focused Staff/Moderator
  // lands on their own home). The server's per-session / GateOperator /
  // Visitors.RegisterOnsite grant is the real authority; this keeps the wrong
  // role out of the screen and lets the nav show only the role's own pages.
  final allowed = allowedRolesForPath(fullPath);
  if (isSignedIn &&
      allowed != null &&
      (appRole == null || !allowed.contains(appRole))) {
    return '/';
  }
  return null;
}

final routerProvider = Provider<GoRouter>(buildRouter);
