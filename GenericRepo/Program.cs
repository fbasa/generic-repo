using GenericRepo.EntityFramework;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var problem = new ValidationProblemDetails(context.ModelState)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation errors occurred"
            };
            return new BadRequestObjectResult(problem);
        };
    });



//Trace connection open/close events
builder.Services.AddSingleton<ConnectionTracingInterceptor>();
builder.Services.AddDbContextPool<TempDbContext>((sp, options) =>
{
    var cs = builder.Configuration.GetConnectionString("SqlServer");
    options.UseSqlServer(cs, sql =>
    {
        sql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(5), errorNumbersToAdd: null);
        sql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
    });

    /*
    Note: If you wrap several operations in your own transaction, 
    run them under the execution strategy so the whole unit can retry:

    var strategy = db.Database.CreateExecutionStrategy();
    await strategy.ExecuteAsync(async () =>
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // multiple ops
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    });


    */
    options.AddInterceptors(
       sp.GetRequiredService<ConnectionTracingInterceptor>()
   );

});


// Rate Limiter service to limit number of requests per user/IP address
// This is a global rate limiter that applies to all requests
// It uses a fixed window strategy where each user/IP can make a maximum of 10 requests every 15 seconds
// This is useful to prevent abuse of the API and ensure fair usage among users.
// The rate limiter will reject requests that exceed the limit with a 429 status code and a custom error message.
// Also, preventing DDoS Attacks
builder.Services.AddRateLimiter(options =>
{
    //Add Custom Response for Throttled Requests
    options.RejectionStatusCode = 429;
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsync(
            "{\"error\": \"Too many requests. Please try again later.\"}", token);
    };

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: key => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10, // max 10 requests
            Window = TimeSpan.FromSeconds(15), // per 15 seconds
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 2 // allow 2 queued requests
        }));
});



//By setting to null, It will preserve property's name casing on an object/class 
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = null;
        options.JsonSerializerOptions.DictionaryKeyPolicy = null;
    });



var app = builder.Build();

//custom middleware to handle exceptions globally
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseRateLimiter(); // Enable rate limiting middleware

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();


public sealed class TempDbContext : DbContext
{
}