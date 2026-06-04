import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/route_names.dart';

void main() {
  group('RouteNames', () {
    test('declares the 39 numbered routes + 3 auxiliary auth routes', () {
      // Sanity check on the public constants — guards against accidental
      // removal during Phase 2 / Phase 3.
      const allNumbered = <String>[
        RouteNames.splash,
        RouteNames.onboarding,
        RouteNames.signIn,
        RouteNames.signUpType,
        RouteNames.signUpForm,
        RouteNames.emailOtp,
        RouteNames.signUpVisitor,
        RouteNames.terms,
        RouteNames.registrationSuccess,
        RouteNames.registrationStatus,
        RouteNames.guestMode,
        RouteNames.home,
        RouteNames.myArea,
        RouteNames.venueMap,
        RouteNames.sessions,
        RouteNames.sessionDetail,
        RouteNames.mySeat,
        RouteNames.speakers,
        RouteNames.speakerProfile,
        RouteNames.booths,
        RouteNames.sponsors,
        RouteNames.archive,
        RouteNames.liveBroadcast,
        RouteNames.sendQuestion,
        RouteNames.requestInterview,
        RouteNames.audienceComments,
        RouteNames.news,
        RouteNames.gallery,
        RouteNames.mediaPartners,
        RouteNames.badge,
        RouteNames.notifications,
        RouteNames.aiSummary,
        RouteNames.meetPeople,
        RouteNames.chatbot,
        RouteNames.aboutForum,
        RouteNames.accessibility,
        RouteNames.rate,
        RouteNames.more,
      ];

      expect(allNumbered.length, equals(38));
      expect(
        allNumbered.toSet().length,
        equals(38),
        reason: 'No two route names should collide.',
      );
    });

    test('auxiliary auth routes are declared', () {
      expect(RouteNames.forgotPassword, isNotEmpty);
      expect(RouteNames.resetPassword, isNotEmpty);
      expect(RouteNames.verifyOtp, isNotEmpty);
    });
  });
}
