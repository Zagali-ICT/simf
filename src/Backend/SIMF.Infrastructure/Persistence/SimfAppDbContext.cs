using Microsoft.EntityFrameworkCore;

namespace SIMF.Infrastructure.Persistence;

/// <summary>
/// The application database context — the SIMF business entities. It shares one
/// physical database with <see cref="SimfIdentityDbContext"/> (decision C-1) but
/// keeps its own migration history table. It holds no entities yet; the
/// business entities are added with their feature sprints.
/// </summary>
public class SimfAppDbContext(DbContextOptions<SimfAppDbContext> options) : DbContext(options)
{
}
