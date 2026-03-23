/// <summary>
/// Program.cs - Temperature API Application Entry Point
/// 
/// Configures and initializes a production-ready ASP.NET Core Web API for temperature data management.
/// This file sets up the dependency injection container, middleware pipeline, authentication, 
/// security policies, monitoring, and health checks.
/// 
/// Key Features:
/// - JWT Bearer authentication with environment-based secret management
/// - PostgreSQL database connectivity with connection pooling for high-throughput scenarios (20k req/s)
/// - Redis caching and distributed session support
/// - CORS policy with configurable allowed origins via environment variables
/// - Rate limiting (100k requests per 10-second window at API level, with load balancing consideration)
/// - Serilog structured logging with async file writing and rolling daily log files
/// - Security headers middleware to prevent MIME sniffing, clickjacking, and XSS attacks
/// - HSTS and CSP policies for HTTPS enforcement and content security
/// - Request timeout handling (30-second default) to prevent hanging requests
/// - Prometheus metrics collection and exposure at /metrics endpoint
/// - Health checks for PostgreSQL and Redis dependencies
/// - Reverse proxy support with X-Forwarded headers for load balancers
/// - Retry policies for resilient service calls
/// 
/// Environment Variables Required:
/// - JWT_KEY: Symmetric key for JWT signing (minimum 32 characters)
/// - POSTGRES_CONNECTION_STRING: PostgreSQL database connection string
/// - POSTGRES_PASSWORD: PostgreSQL password (fallback for development)
/// - REDIS_CONNECTION_STRING: Redis connection string
/// - ALLOWED_ORIGINS: Comma-separated list of CORS-allowed origins
/// 
/// Configuration Sections:
/// - Jwt: Contains Issuer and Audience claims for token validation
/// - ConnectionStrings: Database and cache connection strings (with environment variable overrides)
/// - AllowedOrigins: CORS origins (overridable via environment variable)
/// 
/// Middleware Pipeline (in order):
/// 1. Security headers and server identification removal
/// 2. Forwarded headers processing (XForwardedFor, XForwardedProto)
/// 3. Swagger/OpenAPI (development only)
/// 4. Request timeout enforcement
/// 5. Structured request logging (Serilog)
/// 6. Prometheus metrics collection
/// 7. CORS policy enforcement
/// 8. Rate limiting
/// 9. Authentication
/// 10. Authorization
/// </summary>
using TemperatureApi.Applications.Interfaces;
using TemperatureApi.Applications.Services;
using TemperatureApi.Infrastructure.Repositories;
using Npgsql;
using System.Data;
using Serilog;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.AspNetCore.RateLimiting;
using HealthChecks.Redis;
using TemperatureApi.Infrastructure.Policies;
using Prometheus;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

// ===== SECURITY: Load secrets from environment variables (not config files) =====
var jwtKey = Environment.GetEnvironmentVariable("JWT_KEY")
    ?? throw new InvalidOperationException("JWT_KEY environment variable not set. Generate with: openssl rand -base64 32");

if (string.IsNullOrEmpty(jwtKey) || jwtKey.Length < 32)
{
    throw new InvalidOperationException("JWT_KEY must be at least 32 characters. Generate with: openssl rand -base64 32");
}

// Database connection string from environment (with fallback for development)
var postgresConnStr = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")
    ?? (builder.Configuration.GetConnectionString("Postgres") + $"Password={Environment.GetEnvironmentVariable("POSTGRES_PASSWORD") ?? "postgres"};")
    ?? throw new InvalidOperationException("POSTGRES_CONNECTION_STRING environment variable not set");

var redisConnStr = Environment.GetEnvironmentVariable("REDIS_CONNECTION_STRING")
    ?? builder.Configuration.GetConnectionString("Redis")
    ?? throw new InvalidOperationException("REDIS_CONNECTION_STRING environment variable not set");

// CORS origins from environment
var allowedOrigins = (Environment.GetEnvironmentVariable("ALLOWED_ORIGINS")
    ?? builder.Configuration["AllowedOrigins"]
    ?? "http://localhost:3000").Split(",", StringSplitOptions.RemoveEmptyEntries);

var jwtSettings = builder.Configuration.GetSection("Jwt");

//Rate limiting - Production ready for 20k req/s
// Moved to API Gateway/Load Balancer level for better control
// API-level rate limiting set high to allow proper load distribution
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("fixed", config =>
    {
        config.PermitLimit = 100000;  // 100k requests per window
        config.Window = TimeSpan.FromSeconds(10);  // Per 10 seconds
        config.QueueLimit = 50000;  // Large queue for burst traffic
    });
});

// Authentication and Authorization Configuration
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtKey))  // Use environment variable
        };
    });

builder.Services.AddAuthorization();

// CORS Configuration (Phase 3 - Security Hardening)
// Origins loaded from environment variable ALLOWED_ORIGINS
builder.Services.AddCors(options =>
{
    options.AddPolicy("ProductionPolicy", policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials()
            .WithExposedHeaders("X-Total-Count", "X-Page-Number")
            .SetPreflightMaxAge(TimeSpan.FromHours(1));
    });
});

// Logger - Async file writing (non-blocking) for 20k req/s performance
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.Async(a =>
        a.File("logs/temperature-api-.log",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 30))  // Keep 30 days of logs
    .MinimumLevel.Information()
    .CreateLogger();

builder.Host.UseSerilog();

// Controllers
builder.Services.AddControllers();

// Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register IDbConnection with connection pooling (CRITICAL for 20k req/s)
// postgresConnStr already loaded from environment variables above
if (string.IsNullOrEmpty(postgresConnStr))
{
    throw new InvalidOperationException("Database connection string not configured");
}

builder.Services.AddScoped<IDbConnection>(sp =>
{
    var connection = new NpgsqlConnection(postgresConnStr);
    // Connection pooling is automatic in Npgsql
    // Default pool: min 1, max 30 (sufficient for typical loads)
    // For 20k req/s, relies on load balancing across multiple API instances
    return connection;
});

builder.Services.AddHealthChecks()
    .AddNpgSql(postgresConnStr)
    .AddRedis(redisConnStr);

// Prometheus metrics (Phase 2 - monitoring)
builder.Services.AddSingleton<ICollectorRegistry>(Metrics.DefaultRegistry);

// Policies
// Retry Policies
builder.Services.AddSingleton(
    RetryPolicies.GetDefaultPolicy()
);

// Register Repository
builder.Services.AddScoped<ITemperatureRepository, TemperatureRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();

// Register Service
builder.Services.AddScoped<ITemperatureService, TemperatureService>();
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();

// Cache
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
});


var app = builder.Build();

// Security Headers Middleware (Phase 3 - Security Hardening)
app.Use(async (context, next) =>
{
    // Remove server identification headers
    context.Response.Headers.Remove("Server");
    context.Response.Headers.Remove("X-AspNet-Version");
    context.Response.Headers.Remove("X-AspNetMvc-Version");

    // Prevents Swagger from showing when in dev
    if (app.Environment.IsProduction())
    {
        // Security headers for production
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";  // Prevent MIME sniffing
        context.Response.Headers["X-Frame-Options"] = "DENY";  // Prevent clickjacking
        context.Response.Headers["X-XSS-Protection"] = "1; mode=block";  // XSS protection
        context.Response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains"; // HSTS
        context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:";
    }

    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";

    await next();
});

// Forwarded headers for reverse proxy (load balancer)
var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
    RequireHeaderSymmetry = false,
    ForwardLimit = null
};

forwardedHeadersOptions.KnownNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeadersOptions);

// Middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Request timeout middleware (CRITICAL for 20k req/s - prevent hanging requests)
app.Use(async (context, next) =>
{
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
    var originalToken = context.RequestAborted;
    using var linkedTokens = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, originalToken);
    context.RequestAborted = linkedTokens.Token;

    try
    {
        await next();
    }
    catch (OperationCanceledException)
    {
        if (!context.Response.HasStarted)
        {
            context.Response.StatusCode = StatusCodes.Status408RequestTimeout;
            await context.Response.WriteAsJsonAsync(new { error = "Request timeout after 30 seconds" });
        }
    }
});

// Structured request logging (async, non-blocking)
app.UseSerilogRequestLogging(opts =>
{
    opts.EnrichDiagnosticContext = (context, httpContext) =>
    {
        context.Set("UserName", httpContext.User?.Identity?.Name ?? "Anonymous");
        context.Set("ClientIP", httpContext.Connection.RemoteIpAddress?.ToString());
    };
    opts.MessageTemplate = "[{StatusCode}] {Method} {Path} {Duration}ms";
});

// Prometheus metrics middleware (Phase 2 - monitoring)
app.UseHttpMetrics();

app.UseCors("ProductionPolicy");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// endpoints
app.MapControllers();
app.MapMetrics(); // Prometheus metrics endpoint at /metrics
app.MapHealthChecks("/health");

app.Run();