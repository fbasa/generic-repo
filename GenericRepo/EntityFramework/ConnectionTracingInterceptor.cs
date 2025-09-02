using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;

namespace GenericRepo.EntityFramework;

/*
 * 6 ways tracing Connection Open/Close in an IIS-Hosted .NET App
 * 1. Leverage EF Core’s Connection Interceptors
 * 
 */

/// <summary>
/// EF Core provides a built-in mechanism to hook into connection events. 
/// You implement IDbConnectionInterceptor and register it in DI.
/// Every time EF Core opens or closes a SqlConnection, you’ll see a log entry.
/// </summary>
public class ConnectionTracingInterceptor : DbConnectionInterceptor
{
    private readonly ILogger<ConnectionTracingInterceptor> _log;

    public ConnectionTracingInterceptor(ILogger<ConnectionTracingInterceptor> log)
    {
        _log = log;
    }

    public override InterceptionResult ConnectionOpening(
        DbConnection connection,
        ConnectionEventData eventData,
        InterceptionResult result)
    {
        _log.LogInformation("Opening connection {ConnectionId}", eventData.ConnectionId);
        return base.ConnectionOpening(connection, eventData, result);
    }

    public override void ConnectionClosed(
        DbConnection connection,
        ConnectionEndEventData eventData)
    {
        _log.LogInformation("Closed connection {ConnectionId}", eventData.ConnectionId);
        base.ConnectionClosed(connection, eventData);
    }
}



/*
 
2. Enable ADO.NET Tracing via Configuration
.NET’s System.Data namespace exposes trace switches and listeners that emit wire-level events:

- Add to your appsettings.json or web.config:

<configuration>
  <system.diagnostics>
    <switches>
      <add name="System.Data" value="Verbose"/>
    </switches>
    <sources>
      <source name="System.Data"
              switchName="System.Data"
              switchType="System.Diagnostics.SourceSwitch">
        <listeners>
          <add name="dataTrace"/>
        </listeners>
      </source>
    </sources>
    <sharedListeners>
      <add name="dataTrace"
           type="System.Diagnostics.TextWriterTraceListener"
           initializeData="C:\logs\AdoNetTrace.log"/>
    </sharedListeners>
    <trace autoflush="true"/>
  </system.diagnostics>
</configuration>

- Recycle your IIS app pool.
- Check AdoNetTrace.log for Connection.Open(), Connection.Close(), parameter binding, etc.



3. Use ETW + PerfView or Windows Performance Recorder
Event Tracing for Windows (ETW) captures low-level SQL Client events.
- PerfView
- Download PerfView from Microsoft GitHub.
- Enable “Providers → Microsoft-System.Data → SqlClient”.
- Run a collection during your IIS-driven scenario.
- Look under “Events → SqlClient” for OpenConnectionStart, OpenConnectionStop, and ConnectionClosed.
- Windows Performance Recorder (WPR)
- Launch WPR with the SqlClient profile.
- Collect while your app runs.
- View in Windows Performance Analyzer (WPA).
This approach has zero impact on your code and tracks every ADO.NET call.



4. SQL Server-Side Extended Events
On the database side, you can catch login/logout events:
CREATE EVENT SESSION [ConnectionTracking] ON SERVER
ADD EVENT sqlserver.login( ACTION(sqlserver.client_app_name, sqlserver.username) ),
    EVENT sqlserver.logout( ACTION(sqlserver.client_app_name, sqlserver.username) )
ADD TARGET package0.event_file( SET filename=N'C:\XEL\ConnectionTracking.xel' );
ALTER EVENT SESSION [ConnectionTracking] ON SERVER STATE = START;

- Login → connection opened.
- Logout → connection closed.
Trace these XEL files with SQL Server Management Studio to see which client opened or dropped a session.




5. Application Insights / OpenTelemetry
If you’re deployed to Azure or any environment where you can inject telemetry:
- Application Insights
- Turn on SQL client collection in your AddApplicationInsightsTelemetry() setup.
- You’ll get automatic dependency calls for each SQL client operation, including timings.
- OpenTelemetry (.NET)
builder.Services.AddOpenTelemetryTracing(tracerProviderBuilder =>
   tracerProviderBuilder
     .AddSqlClientInstrumentation()
     .AddConsoleExporter());

Every DB call (including open/close under the hood) becomes a span you can visualize.




6. Wrap IDbConnection in a Custom Decorator
For maximum control, register your own IDbConnection factory:
public class TracingConnection : IDbConnection
{
    private readonly SqlConnection _inner;
    private readonly ILogger<TracingConnection> _log;
    // implement properties and methods, delegating to _inner...
    public void Open()
    {
        _log.LogInformation("Opening connection");
        _inner.Open();
    }
    public void Close()
    {
        _log.LogInformation("Closing connection");
        _inner.Close();
    }
    public void Dispose()
    {
        _log.LogInformation("Disposing connection");
        _inner.Dispose();
    }
    // ...rest of IDbConnection members...
}

// In DI registration
services.AddTransient<IDbConnection>(sp =>
{
    var connStr = sp.GetRequiredService<IConfiguration>()
                    .GetConnectionString("Default");
    var sqlConn = new SqlConnection(connStr);
    return new TracingConnection(sqlConn, sp.GetRequiredService<ILogger<TracingConnection>>());
});


All opens, closes, and disposes emit your own logs.

Next Steps & Teaching Points
- If you teach this to students, contrast interceptor-based vs ETW-based tracing.
- Show how log levels (Information vs Debug vs Verbose) affect production performance.
- Demonstrate a live PerfView capture to illustrate the “wire” events vs what your code sees.
Want sample slides or a live demo script for your next class? I can help you craft that too.


 
*/