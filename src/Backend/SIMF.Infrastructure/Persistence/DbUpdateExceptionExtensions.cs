using Microsoft.EntityFrameworkCore;

namespace SIMF.Infrastructure.Persistence;

/// <summary>Narrow translation of a SQL Server duplicate-key
/// <see cref="DbUpdateException"/>: the duplicate-key message carries the
/// offending index name, so a caller lists the filtered UNIQUE indexes it wants
/// to translate and any other violation is left to rethrow unchanged.</summary>
internal static class DbUpdateExceptionExtensions
{
    public static bool ViolatesAnyIndex(this DbUpdateException ex, params string[] indexNames)
    {
        var message = ex.InnerException?.Message ?? ex.Message;
        foreach (var indexName in indexNames)
        {
            if (message.Contains(indexName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }
}
