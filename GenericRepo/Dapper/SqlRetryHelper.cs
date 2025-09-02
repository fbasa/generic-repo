using Dapper;
using System;
using System.Data;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Data.SqlClient;
using System.Data.Common;
using Polly;
using Polly.Retry;

namespace GenericRepo.Dapper;

public static partial class SqlRetryHelper
{
    /// <summary>
    /// Create a retry pipeline that triggers only on command timeouts.
    /// </summary>
    public static ResiliencePipeline CreateTimeoutRetryPipeline(SqlRetryOptions? opts = null)
    {
        opts ??= new SqlRetryOptions();

        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<Exception>(IsTimeout),
                MaxRetryAttempts = opts.MaxRetryAttempts,
                Delay = opts.BaseDelay,
                BackoffType = opts.Backoff,
                UseJitter = opts.UseJitter,
                OnRetry = args =>
                {
                    //logger?.LogWarning(
                    //    "SQL timeout retry {Attempt} after {Delay}. Error: {Message}",
                    //    args.AttemptNumber, args.RetryDelay, args.Outcome.Exception?.Message);
                    return default;
                }
            })
            .Build();
    }

    /// <summary>
    /// Executes a custom DB operation with a fresh connection per retry attempt.
    /// </summary>
    private static ValueTask<T> WithSqlRetryAsync<T>(
        this ISqlConnectionFactory factory,
        Func<DbConnection, CancellationToken, Task<T>> action,
        ResiliencePipeline? pipeline = null,
        CancellationToken ct = default)
    {
        var pipe = pipeline ?? CreateTimeoutRetryPipeline();
        return pipe.ExecuteAsync(async token =>
        {
            using var conn = factory.Create();
            await conn.OpenAsync(token);
            return await action(conn, token);
        }, ct);
    }

    public static ValueTask<IEnumerable<T>> QueryWithRetryAsync<T>(
        this ISqlConnectionFactory factory,
        string commandText,
        object? parameters = null,
        CommandType commandType = CommandType.StoredProcedure,
        int? commandTimeout = null,
        ResiliencePipeline? pipeline = null,
        CancellationToken cancellationToken = default)
        => factory.WithSqlRetryAsync(async (conn, token) =>
        {
            var cmd = new CommandDefinition(
                commandText: commandText,
                parameters: parameters,
                commandType: commandType,
                commandTimeout: commandTimeout ?? 30,
                cancellationToken: token);
            return await conn.QueryAsync<T>(cmd);
        }, pipeline, cancellationToken);

    public static ValueTask<int> ExecuteWithRetryAsync(
        this ISqlConnectionFactory factory,
        string commandText,
        object? parameters = null,
        CommandType commandType = CommandType.StoredProcedure,
        int? commandTimeout = null,
        ResiliencePipeline? pipeline = null,
        CancellationToken cancellationToken = default)
        => factory.WithSqlRetryAsync(async (conn, token) =>
        {
            var cmd = new CommandDefinition(
                commandText: commandText,
                parameters: parameters,
                commandType: commandType,
                commandTimeout: commandTimeout ?? 30,
                cancellationToken: token);
            return await conn.ExecuteAsync(cmd);
        }, pipeline, cancellationToken);

    // ---------- Timeout detection ----------

    private static bool IsTimeout(Exception ex)
    {
        if (ex is TimeoutException) return true;

        if (ex is SqlException se)
        {
            // SQL Server command timeout
            foreach (SqlError err in se.Errors)
                if (err.Number == -2) return true; // -2 = timeout
        }

        if (ex is DbException dbEx && dbEx.ErrorCode == -2) return true;

        // Some providers wrap timeouts as OperationCanceledException.
        // Consider it a timeout only if the token wasn't user-cancelled.
        if (ex is OperationCanceledException oce &&
            (oce.CancellationToken == default || !oce.CancellationToken.IsCancellationRequested))
            return true;

        return false;
    }
}
