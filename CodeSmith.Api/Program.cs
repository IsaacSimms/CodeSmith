// == CodeSmith API Entry Point == //
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using CodeSmith.Api.Middleware;
using CodeSmith.Api.Services;
using CodeSmith.Core.Interfaces;
using CodeSmith.Infrastructure.DependencyInjection;
using CodeSmith.Infrastructure.Diagnostics;
using CodeSmith.Api.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Identity.Web;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// == Telemetry (OpenTelemetry → Application Insights) == //
// The Azure Monitor distro auto-instruments ASP.NET Core requests, outbound HTTP (the LLM
// provider calls), and SqlClient (the usage-enforcement round-trips), so traces split provider
// time from enforcement time per request. Custom CodeSmith spans (llm.completion + phases,
// problem.generation.attempt) ride the same pipeline via AddSource. Active only when the
// APPLICATIONINSIGHTS_CONNECTION_STRING env var is set (the Container App carries it);
// local dev without it runs with telemetry off.
if (!string.IsNullOrWhiteSpace(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
{
    builder.Services.AddOpenTelemetry()
        .UseAzureMonitor()
        .WithTracing(tracing => tracing.AddSource(CodeSmithDiagnostics.SourceName));
}

// == Service Registration == //

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();
// Exception → status mapping lives in AppExceptionHandler's declarative table; add a row there for new exception types
builder.Services.AddExceptionHandler<AppExceptionHandler>();

// Forwarded headers so RemoteIpAddress reflects the real client (Azure / proxies)
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// HttpContext + current user (for usage enforcement seam + dev bypass)
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();

// == Authentication (Bearer + Dev Debug side path) == //
// Bearer (Entra External ID) is the default scheme in all environments. In Development only,
// the Debug scheme is also registered and included in the default authorization policy so
// allow-listed X-Debug-User-Id headers satisfy [Authorize] without a bearer token.
var authenticationBuilder = builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme);
authenticationBuilder.AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));

if (builder.Environment.IsDevelopment())
{
    authenticationBuilder.AddScheme<AuthenticationSchemeOptions, DebugAuthenticationHandler>("Debug", _ => { });
}

builder.Services.AddAuthorization(options =>
{
    if (builder.Environment.IsDevelopment())
    {
        options.DefaultPolicy = new AuthorizationPolicyBuilder(
                JwtBearerDefaults.AuthenticationScheme, "Debug")
            .RequireAuthenticatedUser()
            .Build();
    }
    else
    {
        options.DefaultPolicy = new AuthorizationPolicyBuilder(
                JwtBearerDefaults.AuthenticationScheme)
            .RequireAuthenticatedUser()
            .Build();
    }
});

// Metered AI endpoints: custom 401 ProblemDetails (login_required) instead of stock Unauthorized
builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, MeteredAiAuthorizationMiddlewareResultHandler>();

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
              .AllowAnyMethod()
              // The SPA calls this API cross-origin, so every POST otherwise pays a preflight
              // OPTIONS round-trip; letting browsers cache the preflight verdict removes it.
              .SetPreflightMaxAge(TimeSpan.FromHours(1));
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
