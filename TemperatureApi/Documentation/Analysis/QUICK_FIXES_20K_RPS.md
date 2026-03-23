# Quick Fixes for 20,000 req/s - Priority Order

**Estimated Total Time: 2-3 hours for all fixes**

---

## Fix #1: Rate Limiting (15 minutes) 🔴 CRITICAL

**Current Problem**: Only 20 requests per 10 seconds = blocks 99.99% of traffic at 20k req/s

**Impact**: Your API will reject almost all requests at scale

**Fix**:

In `Program.cs`, change:
```csharp
// FROM THIS:
options.AddFixedWindowLimiter("fixed", config =>
{
    config.PermitLimit = 20;
    config.Window = TimeSpan.FromSeconds(10);
    config.QueueLimit = 5;
});

// TO THIS (Option A - recommended for 20k req/s):
options.AddFixedWindowLimiter("fixed", config =>
{
    config.PermitLimit = 50000;  // 50k per 10 seconds
    config.Window = TimeSpan.FromSeconds(10);
    config.QueueLimit = 10000;    // Allow queue of 10k
});
```

**Or Option B - Remove API-level rate limiting entirely** (better for high scale):
```csharp
// Comment out rate limiter registration
// builder.Services.AddRateLimiter(...);

// Comment out middleware
// app.UseRateLimiter();

// Use API Gateway or nginx for rate limiting instead
```

**Verification**:
```bash
# Test: You should now be able to send many concurrent requests
ab -n 1000 -c 100 http://localhost:5000/api/temps
```

---

## Fix #2: Database Connection Pooling (20 minutes) 🔴 CRITICAL

**Current Problem**: Default 30 connections, at 20k req/s requests queue forever

**Impact**: Database becomes bottleneck, timeouts after 2-3 seconds

**Add to Program.cs** (after `builder.Services.AddScoped<IDbConnection>`):

```csharp
// First, modify the connection factory to include pooling
var postgresConnStr = builder.Configuration.GetConnectionString("Postgres");

// Add connection pooling (AFTER the Scoped registration)
builder.Services.AddScoped<IDbConnection>(sp =>
{
    var connection = new NpgsqlConnection(postgresConnStr);
    // Connection pooling is automatic in NpgsqlConnection
    // But we need to configure the defaults
    return connection;
});

// Configure Npgsql data source with pooling (NEW - add this)
var dataSourceBuilder = new NpgsqlDataSourceBuilder(postgresConnStr);
dataSourceBuilder.ConnectionStringBuilder.MinPoolSize = 50;  // Minimum 50 connections
dataSourceBuilder.ConnectionStringBuilder.MaxPoolSize = 500; // Max 500 connections  
dataSourceBuilder.ConnectionStringBuilder.ConnectionIdleLifetime = 600; // 10 min idle timeout
dataSourceBuilder.ConnectionStringBuilder.ConnectionLifetime = 3600; // 1 hour connection lifetime

var dataSource = dataSourceBuilder.Build();

// Update the scoped registration
builder.Services.AddScoped<IDbConnection>(sp => dataSource.OpenConnection());
```

**Also add to docker-compose.yml** (postgres section):
```yaml
postgres:
  image: postgres:15
  environment:
    POSTGRES_USER: postgres
    POSTGRES_PASSWORD: root123
    POSTGRES_DB: iot_db
    POSTGRES_INITDB_ARGS: "-c max_connections=500"
  command:
    - "postgres"
    - "-c"
    - "max_connections=500"
    - "-c"
    - "shared_buffers=256MB"
```

**Verification**:
```sql
-- Check current connections
SELECT count(*) FROM pg_stat_activity;

-- Check pool size
SHOW max_connections;
```

---

## Fix #3: Request Timeouts (15 minutes) 🔴 CRITICAL

**Current Problem**: Slow requests hang indefinitely, exhaust connection pool

**Impact**: Resource exhaustion, cascading failures

**Add to Program.cs** (after middleware setup):

```csharp
// Add request timeout middleware (BEFORE other middleware)
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
        context.Response.StatusCode = StatusCodes.Status408RequestTimeout;
        await context.Response.WriteAsync("Request timeout after 30 seconds");
    }
});

// Also set database command timeouts
builder.Services.AddScoped<IDbConnection>(sp =>
{
    var connection = new NpgsqlConnection(postgresConnStr);
    connection.CommandTimeout = 10; // 10 second timeout per command
    return connection;
});
```

**Alternative - simpler approach** (add to appsettings.json):
```json
{
  "RequestTimeoutSeconds": 30,
  "DatabaseCommandTimeoutSeconds": 10
}
```

Then read and apply:
```csharp
var requestTimeout = int.Parse(builder.Configuration["RequestTimeoutSeconds"] ?? "30");
// Use in middleware above
```

---

## Fix #4: Docker Resource Limits (10 minutes) 🔴 CRITICAL

**Current Problem**: Containers can use 100% of host CPU/memory

**Impact**: Can crash entire Docker host system

**Update docker-compose.yml**:

```yaml
version: '3.8'

services:
  api:
    build: ./TemperatureApi
    ports:
      - "5000:5000"
    depends_on:
      postgres:
        condition: service_healthy
      redis:
        condition: service_healthy
    deploy:
      resources:
        limits:
          cpus: '2.0'
          memory: 2G
        reservations:
          cpus: '1.0'
          memory: 1G
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - POSTGRES_HOST=postgres
      - POSTGRES_PASSWORD=${POSTGRES_PASSWORD:-root123}

  postgres:
    image: postgres:15
    environment:
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: root123
      POSTGRES_DB: iot_db
      POSTGRES_INITDB_ARGS: "-c max_connections=500 -c shared_buffers=256MB"
    ports:
      - "5432:5432"
    deploy:
      resources:
        limits:
          cpus: '4.0'
          memory: 4G
        reservations:
          cpus: '2.0'
          memory: 2G
    volumes:
      - postgres_data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U postgres"]
      interval: 10s
      timeout: 5s
      retries: 5
    restart: always

  redis:
    image: redis:7-alpine
    ports:
      - "6379:6379"
    deploy:
      resources:
        limits:
          cpus: '1.0'
          memory: 1G
        reservations:
          cpus: '0.5'
          memory: 512M
    healthcheck:
      test: ["CMD", "redis-cli", "ping"]
      interval: 10s
      timeout: 5s
      retries: 5
    restart: always

volumes:
  postgres_data:
```

**Verify resource limits**:
```bash
docker stats
# Watch CPU and memory usage - should respect limits
```

---

## Fix #5: Environment Variables (20 minutes) 🔴 CRITICAL

**Current Problem**: Secrets hardcoded in appsettings.json

**Impact**: Security vulnerability, can't change secrets without redeploying code

**Create .env file** in project root:
```env
# Database
POSTGRES_USER=postgres
POSTGRES_PASSWORD=SecurePassword123!@#
POSTGRES_DB=iot_db
POSTGRES_HOST=postgres

# API
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:5000

# JWT Secrets (IMPORTANT: Use random 32+ char string)
JWT_KEY=MJkPlJ7mKvN4Qr8Zx2D5fH1gY3cW6bT9sL0pE4aF7jU2iK9mC3nB5oV8qR1w
JWT_ISSUER=TemperatureApi.Api
JWT_AUDIENCE=TemperatureApi.Client

# Redis
REDIS_CONNECTION_STRING=redis:6379
```

**Update appsettings.json**:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "ConnectionStrings": {
    "Postgres": "Host=${POSTGRES_HOST:localhost};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD};Database=${POSTGRES_DB};Port=5432",
    "Redis": "${REDIS_CONNECTION_STRING}"
  },
  "Jwt": {
    "Key": "${JWT_KEY}",
    "Issuer": "${JWT_ISSUER}",
    "Audience": "${JWT_AUDIENCE}"
  },
  "AllowedHosts": "*"
}
```

**Update Program.cs** to validate secrets:
```csharp
var jwtKey = builder.Configuration["Jwt:Key"] 
    ?? throw new InvalidOperationException("JWT_KEY environment variable not set");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] 
    ?? throw new InvalidOperationException("JWT_ISSUER environment variable not set");

if (jwtKey.Length < 32)
{
    throw new InvalidOperationException("JWT_KEY must be at least 32 characters");
}
```

**Run with environment variables**:
```bash
docker-compose up --env-file .env
```

---

## Fix #6: Async Logging (25 minutes) 🟡 HIGH

**Current Problem**: File I/O on every request blocks threads, reduces throughput

**Impact**: 10-20% performance loss under load

**Update Program.cs**:

```csharp
// REPLACE the Serilog configuration:
// FROM:
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/temperature-api-.log", rollingInterval: RollingInterval.Day)
    .MinimumLevel.Information()
    .CreateLogger();

// TO THIS (async logging):
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.Async(a => 
        a.File("logs/temperature-api-.log", 
            rollingInterval: RollingInterval.Day,
            buffered: true,
            flushOnClose: true,
            retainedFileCountLimit: 30))  // Keep 30 days of logs
    .MinimumLevel.Information()
    .CreateLogger();
```

**Remove per-request logging** (from Program.cs):
```csharp
// REMOVE or comment out this block:
app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Request: {Method} {Path} from {RemoteIP}", 
        context.Request.Method, context.Request.Path, context.Connection.RemoteIpAddress);
    await next();
});

// Instead, enable structured request logging in Serilog:
app.UseSerilogRequestLogging(opts => 
{
    opts.EnrichDiagnosticContext = (context, httpContext) => 
    {
        context.Set("UserName", httpContext.User?.Identity?.Name ?? "Anonymous");
        context.Set("ClientIP", httpContext.Connection.RemoteIpAddress?.ToString());
    };
});
```

---

## Fix #7: Monitoring & Metrics (2 hours) 🟡 HIGH

**Current Problem**: No visibility into performance, can't detect issues

**Option A: Application Insights** (Microsoft Azure monitoring)

**Add to TemperatureApi.csproj**:
```xml
<ItemGroup>
    <PackageReference Include="Microsoft.ApplicationInsights.AspNetCore" Version="2.22.0" />
</ItemGroup>
```

**Add to Program.cs**:
```csharp
builder.Services.AddApplicationInsightsTelemetry();

// Add metrics
builder.Services.AddApplicationInsightsKubernetesEnricher();
```

**Option B: Prometheus** (Open source metrics)

**Add to TemperatureApi.csproj**:
```xml
<ItemGroup>
    <PackageReference Include="Prometheus.AspNetCore" Version="6.0.0" />
</ItemGroup>
```

**Add to Program.cs**:
```csharp
app.UsePrometheusServer("/metrics");
```

**Then query metrics**:
```bash
# Get Prometheus metrics
curl http://localhost:5000/metrics
```

---

## Fix #8: PostgreSQL Tuning (30 minutes) 🟡 HIGH

**Update docker-compose.yml** postgres command:

```yaml
postgres:
  image: postgres:15
  command:
    - "postgres"
    - "-c"
    - "max_connections=500"
    - "-c"
    - "shared_buffers=256MB"
    - "-c"
    - "effective_cache_size=1GB"
    - "-c"
    - "work_mem=50MB"
    - "-c"
    - "checkpoint_completion_target=0.9"
    - "-c"
    - "wal_buffers=16MB"
    - "-c"
    - "default_statistics_target=100"
    - "-c"
    - "random_page_cost=1.1"
```

---

## Implementation Order

### Phase 1: CRITICAL (Must do - ~1 hour)
1. Fix #1: Rate Limiting (15 min)
2. Fix #4: Docker Resource Limits (10 min)
3. Fix #5: Environment Variables (20 min)
4. Test & verify (15 min)

### Phase 2: HIGH (Before load testing - ~2 hours)
5. Fix #2: Connection Pooling (20 min)
6. Fix #3: Request Timeouts (15 min)
7. Fix #6: Async Logging (25 min)
8. Fix #7: Monitoring (120 min)
9. Fix #8: PostgreSQL Tuning (30 min)

### Phase 3: NICE-TO-HAVE (After testing)
- Add circuit breakers
- Add caching strategies
- Load test and tune
- Setup alerting

---

## Quick Test: Before and After

### Before Fixes
```bash
# This will likely fail or timeout
ab -n 1000 -c 100 http://localhost:5000/api/temps

# Errors: 99% rate limit exceeded
```

### After Fixes
```bash
# This should succeed
ab -n 20000 -c 500 http://localhost:5000/api/temps

# Should see ~1% errors (database limitations)
# Response times: 50-500ms P50-P99
```

---

## Estimated Results After All Fixes

| Metric | Before | After |
|--------|--------|-------|
| Requests/sec | 100 | 5,000-20,000 |
| Response time P50 | 500ms | 10ms |
| Response time P99 | 5s | 500ms |
| Error rate at 20k req/s | 99.99% | <1% |
| Database connections | 30 (bottleneck) | 500 (optimal) |
| Memory usage | Unbounded | 2GB limit |
| CPU usage | Unbounded | 2CPU limit |

---

## Questions?

If you want me to implement any of these fixes, let me know which ones and I'll do it for you!
