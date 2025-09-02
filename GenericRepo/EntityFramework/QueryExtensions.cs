using Microsoft.Data.SqlClient;
using System.Data;

namespace GenericRepo.EntityFramework;

public static class QueryExtensions
{
    public static (string, SqlParameter[]) BuildAllParameters(
        this string storedProcName, 
        SqlParameter[] parameters
        )
    {
        var allParams = new List<SqlParameter>
        {
            new SqlParameter("@ErrorMsg", SqlDbType.NVarChar, 4000) { Direction = ParameterDirection.Output },
            new SqlParameter("", SqlDbType.Int) { Direction = ParameterDirection.ReturnValue }
        };
        allParams.AddRange(parameters);
        var sql = $"{storedProcName} {string.Join(", ", allParams.Where(p => !string.IsNullOrEmpty(p.ParameterName)).Select(p => p.ParameterName))}";
        return (sql, allParams.ToArray());
    }

    public static T GetValueOrDefault<T>(
        this SqlParameter parameter,
        T defaultValue = default!
    )
    {
        if (parameter is null)
            throw new ArgumentNullException(nameof(parameter));

        var val = parameter.Value;
        if (val == null || val == DBNull.Value)
            return defaultValue;

        // If already the target type, cast directly
        if (val is T variable)
            return variable;

        // Fallback to ChangeType for primitives, strings, etc.
        return (T)Convert.ChangeType(val, typeof(T));
    }

    /// <summary>
    /// Convenience overload for string parameters.
    /// Returns empty string if null/DBNull.
    /// </summary>
    public static string GetValueOrDefault(
        this SqlParameter parameter
    ) => parameter.GetValueOrDefault<string>(string.Empty);

    public static async Task<List<T>> ToListAsync<T>(this IAsyncEnumerable<T> source,
    CancellationToken cancellationToken = default)
    {
        var list = new List<T>();
        await foreach (var item in source.WithCancellation(cancellationToken))
            list.Add(item);

        return list;
    }
}
