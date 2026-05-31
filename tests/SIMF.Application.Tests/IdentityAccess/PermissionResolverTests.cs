// Issue-1 — the resolver baked into the JWT `perm` claim. Administrator
// short-circuits to the wildcard without a DB query; every other role resolves
// to its granted codes.
using SIMF.Application.IdentityAccess;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Domain.IdentityAccess;
using Xunit;

namespace SIMF.Application.Tests.IdentityAccess;

public sealed class PermissionResolverTests
{
    private sealed class FakePermissionRepository : IPermissionRepository
    {
        public List<string>? LastRoleNames;
        public IReadOnlyList<string> CodesToReturn = [];

        public Task<IReadOnlyList<string>> GetCodesForRolesAsync(
            IEnumerable<string> roleNames, CancellationToken cancellationToken = default)
        {
            LastRoleNames = roleNames.ToList();
            return Task.FromResult(CodesToReturn);
        }

        public Task<IReadOnlyList<Permission>> GetAllAsync(CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<Permission?> GetByCodeAsync(string code, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task AddAsync(Permission permission, CancellationToken ct = default) =>
            throw new NotImplementedException();
    }

    [Fact]
    public async Task Administrator_resolves_to_the_wildcard_without_querying_the_repository()
    {
        var repo = new FakePermissionRepository { CodesToReturn = ["must.not.be.used"] };
        var resolver = new PermissionResolver(repo);

        var result = await resolver.ResolveForRolesAsync([ "GateOperator", AppRoles.Administrator ]);

        Assert.Equal([PermissionCatalog.Wildcard], result);
        Assert.Null(repo.LastRoleNames); // short-circuited before the DB lookup
    }

    [Fact]
    public async Task Non_admin_roles_resolve_to_their_granted_codes()
    {
        var repo = new FakePermissionRepository
        {
            CodesToReturn = [PermissionCatalog.Gates.Operate, PermissionCatalog.Gates.ViewOwnReports],
        };
        var resolver = new PermissionResolver(repo);

        var result = await resolver.ResolveForRolesAsync([AppRoles.GateOperator]);

        Assert.Equal(
            [PermissionCatalog.Gates.Operate, PermissionCatalog.Gates.ViewOwnReports], result);
        Assert.Equal([AppRoles.GateOperator], repo.LastRoleNames);
    }

    [Fact]
    public async Task No_roles_resolves_to_no_permissions()
    {
        var repo = new FakePermissionRepository { CodesToReturn = [] };
        var resolver = new PermissionResolver(repo);

        var result = await resolver.ResolveForRolesAsync([]);

        Assert.Empty(result);
    }
}
