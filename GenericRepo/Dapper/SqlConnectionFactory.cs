using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System;

namespace GenericRepo.Dapper;

public interface ISqlConnectionFactory
{
    SqlConnection Create();
}

/// <summary>
/// Connection Pooling for SQL Server.
/// Pooling makes Open() cheap. With pooling on (default), Open() usually just 
/// grabs a pooled connection; no full TCP/TDS handshake. 
/// The heavy work is your query, network RTT, and SQL’s execution—not the open/close.
/// Safety beats micro-optimizing opens. Per-call open/close avoids thread-safety issues, 
/// data reader conflicts, and pool starvation that happen when multiple services share one live connection in the scope.
/// Sequential service calls are fine. Each service opens, executes, disposes.
/// The pool reuses physical connections across those calls.
/// When to share one connection: only if you need a single transaction across multiple 
/// operations, need temp tables or session settings to persist between calls, or you’re 
/// doing a tight loop of many tiny commands where microseconds matter. 
/// In those cases, explicitly create/own a connection for that unit of work and pass it down.
/// </summary>
public sealed class SqlConnectionFactory : ISqlConnectionFactory
{
    private readonly string _connectionString;

    public SqlConnectionFactory(IConfiguration cfg)
    {
        _connectionString = cfg.GetConnectionString("PSM_WebConnectionString")
            ?? throw new InvalidOperationException("Missing connection string.");
    }

    public SqlConnection Create() => new SqlConnection(_connectionString);
}
