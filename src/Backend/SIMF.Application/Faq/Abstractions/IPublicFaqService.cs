using SIMF.Contracts.Faq;

namespace SIMF.Application.Faq.Abstractions;

/// <summary>
/// Public, anonymous read of the FAQ for the app's الأسئلة الشائعة accordion
/// (Figma 1388:7567). Returns only active groups (with at least one active
/// entry), each carrying its active entries ordered by <c>DisplayOrder</c>.
/// Read-only projection over the same <c>SimfAppDbContext</c> tables the admin
/// FAQ module manages (D-211); no schema change.
/// </summary>
public interface IPublicFaqService
{
    Task<IReadOnlyList<PublicFaqGroup>> GetAsync(
        CancellationToken cancellationToken = default);
}
