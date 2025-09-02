using Dapper;
using Polly;
using System.Data;
using Microsoft.Extensions.Options;

namespace GenericRepo.Dapper;

public interface IDapperBaseService
{
    Task<IEnumerable<T>> SqlQueryAsync<T>(
        string sql,
        DynamicParameters parameters,
        CancellationToken ct = default
        );

    Task<int> ExecuteCommandAsync(
        string sql,
        DynamicParameters parameters,
        CancellationToken ct = default
        );
}

/// <summary>
/// Provides basic Dapper-based data access for stored procedures.
/// </summary>
public class DapperBaseService(
        ISqlConnectionFactory factory,
        IOptionsMonitor<DapperOptions> options,
        ResiliencePipeline pipeline
        ) : IDapperBaseService
{

    private int CommandTimeout =>
        Math.Max(30, options.CurrentValue.CommandTimeoutSeconds);

    /// <summary>
    /// Executes a stored procedure that returns a sequence of T.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="sql"></param>
    /// <param name="parameters"></param>
    /// <returns></returns>
    public async Task<IEnumerable<T>> SqlQueryAsync<T>(string sql, DynamicParameters parameters, CancellationToken ct = default)
    {
        return await factory.QueryWithRetryAsync<T>(
            commandText: sql,
            parameters: parameters,
            commandType: CommandType.StoredProcedure,
            commandTimeout: CommandTimeout,
            pipeline: pipeline,
            cancellationToken: ct);
    }

    /// <summary>
    /// Executes a stored procedure that does not return rows.
    /// </summary>
    /// <param name="sql"></param>
    /// <param name="parameters"></param>
    /// <returns></returns>
    public async Task<int> ExecuteCommandAsync(string sql, DynamicParameters parameters, CancellationToken ct = default)
    {
        return await factory.ExecuteWithRetryAsync(
            commandText: sql,
            parameters: parameters,
            commandType: CommandType.StoredProcedure,
            commandTimeout: CommandTimeout,
            cancellationToken: ct
        );
    }
}
