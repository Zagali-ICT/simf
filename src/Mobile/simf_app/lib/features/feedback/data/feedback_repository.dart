import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/features/feedback/data/feedback_endpoints.dart';
import 'package:simf_app/features/feedback/data/rating_models.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// Data layer for the dynamic rating form (Page_040).
///
/// `GET /app/feedback/form` returns the form for a rating type (resolved by
/// `code` e.g. "App"/"Session", or by `ratingTypeId`) and optional `targetId`
/// (a session id for a per-session type), including the attendee's existing
/// submission for prefill. `POST /app/feedback/submit` upserts the submission
/// (overall stars + per-question answers + comment). Both require an approved
/// account.
class FeedbackRepository {
  FeedbackRepository(this._client);

  final SimfApiClient _client;

  Future<RatingFormView> getForm({
    String? code,
    String? ratingTypeId,
    String? targetId,
  }) {
    return _client.get<RatingFormView>(
      FeedbackEndpoints.form,
      queryParameters: <String, dynamic>{
        if (code != null) 'code': code,
        if (ratingTypeId != null) 'ratingTypeId': ratingTypeId,
        if (targetId != null) 'targetId': targetId,
      },
      decodeData: RatingFormView.fromData,
    );
  }

  Future<void> submit({
    required String ratingTypeId,
    required Map<String, int> answers, String? targetId,
    int? overallStars,
    String? comment,
  }) {
    return _client.post<bool>(
      FeedbackEndpoints.submit,
      body: <String, dynamic>{
        'ratingTypeId': ratingTypeId,
        'targetId': targetId,
        'overallStars': overallStars,
        'comment': comment,
        'answers': answers.entries
            .map((e) => <String, dynamic>{'questionId': e.key, 'stars': e.value})
            .toList(growable: false),
      },
      decodeData: (_) => true,
    );
  }
}

final feedbackRepositoryProvider = Provider<FeedbackRepository>((ref) {
  return FeedbackRepository(ref.watch(simfApiClientProvider));
});
