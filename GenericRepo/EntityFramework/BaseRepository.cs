using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GenericRepo.EntityFramework;

public interface IBaseRepository
{
    Task<(IEnumerable<T>, string, int)> SqlQueryRaw<T>(string storedProcName, params SqlParameter[] parameters) where T : class;
    Task<(int,string, int)> ExecuteSqlRawAsync(string storedProcName, SqlParameter[] parameters);
}

public class BaseRepository : IBaseRepository
{
    private readonly DbContext _context;

    public BaseRepository(DbContext context)
    {
        _context = context;
    }

    public async Task<(IEnumerable<T>, string, int)> SqlQueryRaw<T>(string storedProcName, params SqlParameter[] parameters) where T : class
    {
        ArgumentNullException.ThrowIfNull(storedProcName);

        var (sql, allParams) = storedProcName.BuildAllParameters(parameters);
        var items = await _context.Database.SqlQueryRaw<T>(sql, allParams)
            .ToListAsync()
            .ConfigureAwait(false);

        var errorMsg = allParams[^2].GetValueOrDefault();
        var errorNo = allParams[^1].GetValueOrDefault(0);

        return (items, errorMsg, errorNo);
    }

    public async Task<(int, string, int)> ExecuteSqlRawAsync(string storedProcName, params SqlParameter[] parameters)
    {
        ArgumentNullException.ThrowIfNull(storedProcName);

        var (sql, allParams) = storedProcName.BuildAllParameters(parameters);
        var rows = await _context.Database.ExecuteSqlRawAsync(sql, parameters)
                        .ConfigureAwait(false);

        var errorMsg = allParams[^2].GetValueOrDefault();
        var errorNo = allParams[^1].GetValueOrDefault(0);

        return (rows, errorMsg, errorNo);
    }
}
