using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GenericRepo.EntityFramework;


/// <summary>
/// Compiled-Query Pattern for "Hot" Paths
/// If you invoke the same SP hundreds or thousands of times per second, 
/// you can eliminate even the SQL‐to‐expression compilation overhead
/// Benefits:
///     -One‐time compilation of the LINQ pipeline.
///     - Ideal for extreme throughput scenarios where every microsecond counts.
/// </summary>
public static class RawSqlCompiled<T> where T : class
{
    public static readonly Func<DbContext, string, SqlParameter[], IAsyncEnumerable<T>> SqlQueryRaw =
        EF.CompileQuery(
            (DbContext ctx, string sql, SqlParameter[] ps) =>
                ctx.Database
                   .SqlQueryRaw<T>(sql, ps)
                   .AsAsyncEnumerable()
        );
}

public class CompiledQueryRepository(DbContext dbContext)
{
    public async Task<(IEnumerable<T>, string, int)>
        SqlQueryRaw<T>(string storedProcName, params SqlParameter[] parameters)
        where T : class
    {

        ArgumentNullException.ThrowIfNull(storedProcName);

        var (sql, allParams) = storedProcName.BuildAllParameters(parameters);

        var stream = RawSqlCompiled<T>.SqlQueryRaw(dbContext, sql, allParams);

        var items = await stream
                        .ToListAsync();

        var errorMsg = allParams[^2].GetValueOrDefault();
        var errorNo = allParams[^1].GetValueOrDefault(0);

        return (items, errorMsg, errorNo);
    }
}