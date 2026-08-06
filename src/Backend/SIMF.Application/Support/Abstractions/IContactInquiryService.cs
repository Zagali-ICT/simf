using SIMF.Common;
using SIMF.Contracts.Support;

namespace SIMF.Application.Support.Abstractions;

/// <summary>
/// "تواصل معنا / Contact us" inquiries: the public app submit (anonymous or
/// signed-in) plus the Control Panel inbox read + handled toggle. Built on
/// <c>SimfAppDbContext</c> as an additive table; there is no cross-DB
/// relation — the submitter
/// is a bare Guid resolved on read.
/// </summary>
public interface IContactInquiryService
{
    /// <summary>Persist a submitted inquiry; returns its new id.</summary>
    Task<Guid> SubmitAsync(
        SubmitContactInquiryRequest request,
        Guid? submittedByUserId,
        CancellationToken cancellationToken = default);

    /// <summary>The CP inbox grid (open first, newest first).</summary>
    Task<GridPage<AdminContactInquiryRow>> ListAsync(
        GridQuery query, CancellationToken cancellationToken = default);

    /// <summary>Mark an inquiry handled / reopened.</summary>
    Task MarkHandledAsync(
        Guid actorUserId, Guid id, bool handled,
        CancellationToken cancellationToken = default);
}
