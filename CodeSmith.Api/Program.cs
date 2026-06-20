// == CodeSmith API Entry Point == //
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using CodeSmith.Api.Middleware;
using CodeSmith.Api.Middleware.ExceptionMappers;
using CodeSmith.Api.Services;
using CodeSmith.Core.Interfaces;
using CodeSmith.Infrastructure.DependencyInjection;
using Microsoft.AspNetCore.HttpOverrides;

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

// Forwarded headers so RemoteIpAddress reflects the real client (Azure / proxies)
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});
builder.Services.AddSingleton<IExceptionMapper, ChallengeNotFoundExceptionMapper>();
builder.Services.AddSingleton<IExceptionMapper, ScenarioNotFoundExceptionMapper>();
builder.Services.AddSingleton<IExceptionMapper, AiServiceExceptionMapper>();
builder.Services.AddSingleton<IExceptionMapper, CodeExecutionExceptionMapper>();
builder.Services.AddSingleton<IExceptionMapper, OperationCancelledExceptionMapper>();
builder.Services.AddSingleton<IExceptionMapper, InsufficientQuotaExceptionMapper>();
builder.Services.AddExceptionHandler<AppExceptionHandler>();

// HttpContext + current user (for usage enforcement seam + dev bypass)
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();

// Minimal auth skeleton so [Authorize] on LLM actions works. Full Entra wiring (AddMicrosoftIdentityWebApi etc.) comes in next seam work.
builder.Services.AddAuthentication();
builder.Services.AddAuthorization();

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

app.UseForwardedHeaders();
app.UseHttpsRedirection();
app.UseRateLimiter();
app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

// Make Program class accessible for integration tests
public partial class Program { }
