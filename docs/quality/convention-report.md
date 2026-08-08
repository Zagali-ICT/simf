# SIMF convention report

Generated 2026-08-08 by `dart run tool/conventions`.

## Summary

| Rule | Findings |
|------|----------|
| SIMF-C3 | 35 |
| **Total** | **35** |

## account feature


Issue file : src/Mobile/simf_app/lib/features/account/sign_in_screen.dart
Issue : _buildCard() returning Widget in a 418-line file (limit 400)  (line 319, SIMF-C3)
Fix : split the file; move this and its state into a widget

Issue file : src/Mobile/simf_app/lib/features/account/sign_up_interests_screen.dart
Issue : _buildBody() returning Widget in a 470-line file (limit 400)  (line 303, SIMF-C3)
Fix : split the file; move this and its state into a widget
Issue : _buildLoadError() returning Widget in a 470-line file (limit 400)  (line 411, SIMF-C3)
Fix : split the file; move this and its state into a widget
Issue : _buildChips() returning Widget in a 470-line file (limit 400)  (line 440, SIMF-C3)
Fix : split the file; move this and its state into a widget

Issue file : src/Mobile/simf_app/lib/features/account/sign_up_visitor_screen.dart
Issue : _buildBody() returning Widget in a 1393-line file (limit 400)  (line 738, SIMF-C3)
Fix : split the file; move this and its state into a widget
Issue : _buildLoadError() returning Widget in a 1393-line file (limit 400)  (line 921, SIMF-C3)
Fix : split the file; move this and its state into a widget
Issue : _buildProfileTypeField() returning Widget in a 1393-line file (limit 400)  (line 955, SIMF-C3)
Fix : split the file; move this and its state into a widget
Issue : _buildNationalityField() returning Widget in a 1393-line file (limit 400)  (line 994, SIMF-C3)
Fix : split the file; move this and its state into a widget
Issue : _buildDocumentFields() returning List<Widget> in a 1393-line file (limit 400)  (line 1129, SIMF-C3)
Fix : split the file; move this and its state into a widget
Issue : _buildPlaceOfBirthField() returning Widget in a 1393-line file (limit 400)  (line 1170, SIMF-C3)
Fix : split the file; move this and its state into a widget
Issue : _buildPlateField() returning Widget in a 1393-line file (limit 400)  (line 1191, SIMF-C3)
Fix : split the file; move this and its state into a widget
Issue : _buildIdImageField() returning Widget in a 1393-line file (limit 400)  (line 1282, SIMF-C3)
Fix : split the file; move this and its state into a widget
Issue : _buildFacePhotoField() returning Widget in a 1393-line file (limit 400)  (line 1302, SIMF-C3)
Fix : split the file; move this and its state into a widget
Issue : _buildOrganisationField() returning Widget in a 1393-line file (limit 400)  (line 1331, SIMF-C3)
Fix : split the file; move this and its state into a widget

## app (shared shell) feature


Issue file : src/Mobile/simf_app/lib/app/widgets/simf_identity_cell.dart
Issue : private widget _LogoOrInitials extends StatelessWidget  (line 137, SIMF-C3)
Fix : its own file under widgets/
Issue : private widget _InitialsAvatar extends StatelessWidget  (line 171, SIMF-C3)
Fix : its own file under widgets/

## contacts feature


Issue file : src/Mobile/simf_app/lib/features/contacts/scan_contact_screen.dart
Issue : private widget _ContactPreviewSheet extends ConsumerStatefulWidget  (line 147, SIMF-C3)
Fix : its own file under widgets/

## delegations feature


Issue file : src/Mobile/simf_app/lib/features/delegations/widgets/delegations_stats_strip.dart
Issue : private widget _FlagSpot extends StatelessWidget  (line 105, SIMF-C3)
Fix : its own file under widgets/

## home feature


Issue file : src/Mobile/simf_app/lib/features/home/widgets/home_hero_banner.dart
Issue : private widget _HeroImage extends StatelessWidget  (line 165, SIMF-C3)
Fix : its own file under widgets/

## live feature


Issue file : src/Mobile/simf_app/lib/features/live/live_broadcast_screen.dart
Issue : _buildBody() returning Widget in a 508-line file (limit 400)  (line 275, SIMF-C3)
Fix : split the file; move this and its state into a widget

## myarea feature


Issue file : src/Mobile/simf_app/lib/features/myarea/my_sessions_screen.dart
Issue : private widget _TabbedList extends StatelessWidget  (line 123, SIMF-C3)
Fix : its own file under widgets/
Issue : private widget _MySessionCard extends StatelessWidget  (line 178, SIMF-C3)
Fix : its own file under widgets/

## questions feature


Issue file : src/Mobile/simf_app/lib/features/questions/widgets/send_question_content.dart
Issue : private widget _NumberedLine extends StatelessWidget  (line 240, SIMF-C3)
Fix : its own file under widgets/

## sessions feature


Issue file : src/Mobile/simf_app/lib/features/sessions/join_session_hub_screen.dart
Issue : private widget _HubList extends StatelessWidget  (line 70, SIMF-C3)
Fix : its own file under widgets/

Issue file : src/Mobile/simf_app/lib/features/sessions/session_detail_screen.dart
Issue : _buildBody() returning Widget in a 510-line file (limit 400)  (line 424, SIMF-C3)
Fix : split the file; move this and its state into a widget

Issue file : src/Mobile/simf_app/lib/features/sessions/session_presentations_screen.dart
Issue : private widget _Body extends StatelessWidget  (line 103, SIMF-C3)
Fix : its own file under widgets/
Issue : private widget _PresentationCard extends StatelessWidget  (line 198, SIMF-C3)
Fix : its own file under widgets/

Issue file : src/Mobile/simf_app/lib/features/sessions/widgets/hall_seat_map.dart
Issue : private widget _SeatGridRow extends StatelessWidget  (line 227, SIMF-C3)
Fix : its own file under widgets/
Issue : private widget _SeatBox extends StatelessWidget  (line 380, SIMF-C3)
Fix : its own file under widgets/

Issue file : src/Mobile/simf_app/lib/features/sessions/widgets/programme_day_strip.dart
Issue : private widget _DayCell extends StatelessWidget  (line 145, SIMF-C3)
Fix : its own file under widgets/

Issue file : src/Mobile/simf_app/lib/features/sessions/widgets/session_header_card.dart
Issue : private widget _MetaRow extends StatelessWidget  (line 144, SIMF-C3)
Fix : its own file under widgets/

Issue file : src/Mobile/simf_app/lib/features/sessions/widgets/session_timeline_row.dart
Issue : private widget _TimeRail extends StatelessWidget  (line 134, SIMF-C3)
Fix : its own file under widgets/

## staff feature


Issue file : src/Mobile/simf_app/lib/features/staff/register_visitor_screen.dart
Issue : _buildBody() returning Widget in a 1264-line file (limit 400)  (line 644, SIMF-C3)
Fix : split the file; move this and its state into a widget
Issue : _buildLoadError() returning Widget in a 1264-line file (limit 400)  (line 679, SIMF-C3)
Fix : split the file; move this and its state into a widget
Issue : _buildForm() returning Widget in a 1264-line file (limit 400)  (line 709, SIMF-C3)
Fix : split the file; move this and its state into a widget

