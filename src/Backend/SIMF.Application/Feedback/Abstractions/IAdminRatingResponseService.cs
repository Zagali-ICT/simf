using SIMF.Common;
using SIMF.Contracts.Feedback;

namespace SIMF.Application.Feedback.Abstractions;

/// <summary>Read-only admin view over submitted rating responses: the grid (with
/// the overall-average headline) and the per-type / per-question KPI aggregates.</summary>
public interface IAdminRatingResponseService
{
    /// <summary>Admin grid of responses plus the average-overall aggregate over
    /// the filtered active set.</summary>
    Task<AdminRatingResponsesPage> ListResponsesAsync(
        GridQuery query, CancellationToken cancellationToken = default);

    /// <summary>Rating KPIs: per type the response count, overall average and the
    /// per-question averages.</summary>
    Task<AdminRatingKpiView> GetKpiAsync(
        CancellationToken cancellationToken = default);
}
