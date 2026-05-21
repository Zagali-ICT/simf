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

    public async Task AddAsync(Permission permission, CancellationToken cancellationToken = default)
    {
        dbContext.Permissions.Add(permission);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
