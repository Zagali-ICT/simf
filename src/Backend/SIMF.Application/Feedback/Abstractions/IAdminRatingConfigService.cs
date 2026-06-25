using SIMF.Common;
using SIMF.Contracts.Feedback;

namespace SIMF.Application.Feedback.Abstractions;

/// <summary>Admin CRUD over the rating configuration (types → question groups →
/// questions). Built on <c>SimfAppDbContext</c>; one audit row per mutation;
/// soft-delete via <c>IsActive</c>. Mirrors <c>IAdminFaqService</c>.</summary>
public interface IAdminRatingConfigService
{
    // -- Types --
    Task<GridPage<AdminRatingTypeSummary>> ListTypesAsync(
        GridQuery query, CancellationToken cancellationToken = default);

    Task<AdminRatingTypeSummary?> GetTypeAsync(
        Guid id, CancellationToken cancellationToken = default);

    Task<AdminRatingTypeSummary> CreateTypeAsync(
        Guid actorUserId, CreateRatingTypeRequest request, CancellationToken cancellationToken = default);

    Task<AdminRatingTypeSummary> UpdateTypeAsync(
        Guid actorUserId, Guid id, UpdateRatingTypeRequest request, CancellationToken cancellationToken = default);

    Task DeactivateTypeAsync(
        Guid actorUserId, Guid id, CancellationToken cancellationToken = default);

    // -- Question groups --
    Task<GridPage<AdminRatingQuestionGroupSummary>> ListGroupsAsync(
        Guid ratingTypeId, GridQuery query, CancellationToken cancellationToken = default);

    Task<AdminRatingQuestionGroupSummary?> GetGroupAsync(
        Guid id, CancellationToken cancellationToken = default);

    Task<AdminRatingQuestionGroupSummary> CreateGroupAsync(
        Guid actorUserId, CreateRatingQuestionGroupRequest request, CancellationToken cancellationToken = default);

    Task<AdminRatingQuestionGroupSummary> UpdateGroupAsync(
        Guid actorUserId, Guid id, UpdateRatingQuestionGroupRequest request, CancellationToken cancellationToken = default);

    Task DeactivateGroupAsync(
        Guid actorUserId, Guid id, CancellationToken cancellationToken = default);

    // -- Questions --
    Task<GridPage<AdminRatingQuestionSummary>> ListQuestionsAsync(
        Guid ratingTypeId, GridQuery query, CancellationToken cancellationToken = default);

    Task<AdminRatingQuestionSummary?> GetQuestionAsync(
        Guid id, CancellationToken cancellationToken = default);

    Task<AdminRatingQuestionSummary> CreateQuestionAsync(
        Guid actorUserId, CreateRatingQuestionRequest request, CancellationToken cancellationToken = default);

    Task<AdminRatingQuestionSummary> UpdateQuestionAsync(
        Guid actorUserId, Guid id, UpdateRatingQuestionRequest request, CancellationToken cancellationToken = default);

    Task DeactivateQuestionAsync(
        Guid actorUserId, Guid id, CancellationToken cancellationToken = default);
}
