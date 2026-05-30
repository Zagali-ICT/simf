using SIMF.Common;
using SIMF.Contracts.Companies;

namespace SIMF.Application.Companies.Abstractions;

/// <summary>D-199 #3 — admin CRUD over exhibitor / sponsor companies plus
/// account provisioning. The owner model: create the company name first,
/// then provision login accounts under it.</summary>
public interface IAdminCompanyService
{
    Task<GridPage<AdminCompanySummary>> ListAllAsync(
        GridQuery query, CancellationToken cancellationToken = default);

    Task<AdminCompanyDetail?> GetAsync(
        Guid id, CancellationToken cancellationToken = default);

    Task<AdminCompanyDetail> CreateAsync(
        Guid actorUserId, CreateCompanyRequest request,
        CancellationToken cancellationToken = default);

    Task<AdminCompanyDetail> UpdateAsync(
        Guid actorUserId, Guid id, UpdateCompanyRequest request,
        CancellationToken cancellationToken = default);

    Task DeactivateAsync(
        Guid actorUserId, Guid id,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CompanyAccountSummary>> ListAccountsAsync(
        Guid companyId, CancellationToken cancellationToken = default);

    Task<CompanyAccountSummary> ProvisionAccountAsync(
        Guid actorUserId, Guid companyId, ProvisionCompanyAccountRequest request,
        CancellationToken cancellationToken = default);
}
