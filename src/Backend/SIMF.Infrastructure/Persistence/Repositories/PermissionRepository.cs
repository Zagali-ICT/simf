using Microsoft.EntityFrameworkCore;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Domain.IdentityAccess;

namespace SIMF.Infrastructure.Persistence.Repositories;

internal sealed class PermissionRepository(SimfIdentityDbContext dbContext) : IPermissionRepository
{
    public async Task<IReadOnlyList<Permission>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Permissions.AsNoTracking().ToListAsync(cancellationToken);

    public Task<Permission?> GetByCodeAsync(string code, CancellationToken cancellationToken = default) =>
        dbContext.Permissions
            .SingleOrDefaultAsync(permission => permission.Code == code, cancellationToken);

    public async Task<IReadOnlyList<string>> GetCodesForRolesAsync(
        IEnumerable<string> roleNames, CancellationToken cancellationToken = default)
    {
        var names = roleNames as string[] ?? roleNames.ToArray();
        if (names.Length == 0)
        {
            return [];
        }

        return await (
            from rolePermission in dbContext.RolePermissions
            join role in dbContext.Roles on rolePermission.RoleId equals role.Id
            join permission in dbContext.Permissions on rolePermission.PermissionId equals permission.Id
            where role.Name != null && names.Contains(role.Name)
            select permission.Code)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Permission permission, CancellationToken cancellationToken = default)
    {
        dbContext.Permissions.Add(permission);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
