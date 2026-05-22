// == CodeSmith API Entry Point == //
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using CodeSmith.Api.Middleware;
using CodeSmith.Api.Middleware.ExceptionMappers;
using CodeSmith.Infrastructure.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// == Service Registration == //

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();
builder.Services.AddSingleton<IExceptionMapper, SessionNotFoundExceptionMapper>();
builder.Services.AddSingleton<IExceptionMapper, ChallengeNotFoundExceptionMapper>();
builder.Services.AddSingleton<IExceptionMapper, ScenarioNotFoundExceptionMapper>();
builder.Services.AddSingleton<IExceptionMapper, AiServiceExceptionMapper>();
builder.Services.AddSingleton<IExceptionMapper, CodeExecutionExceptionMapper>();
builder.Services.AddSingleton<IExceptionMapper, OperationCancelledExceptionMapper>();
builder.Services.AddExceptionHandler<AppExceptionHandler>();

// Register CodeSmith Infrastructure services (Anthropic client, session store)
builder.Services.AddCodeSmithInfrastructure(builder.Configuration);

// == Rate Limiting == //
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

// == CORS == //
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("AllowedCorsOrigins").Get<string[]>()
            ?? ["https://localhost:7111", "http://localhost:5175"];

        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// == Middleware Pipeline == //

app.UseExceptionHandler();
app.UseRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseRateLimiter();
app.UseCors();

app.MapControllers();

app.Run();

// Make Program class accessible for integration tests
public partial class Program { }
