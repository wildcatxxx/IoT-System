# Temperature API

A robust, production-ready ASP.NET Core 8.0 Web API for managing and retrieving temperature sensor data. Built with security, scalability, and best practices in mind.

## 📋 Features

- **JWT Authentication**: Secure API endpoints with JWT bearer tokens
- **Rate Limiting**: Protection against abuse with configurable rate limits (20 requests per 10 seconds)
- **Caching**: Redis-based distributed caching for improved performance
- **Database**: PostgreSQL with Dapper ORM for efficient data access
- **Health Checks**: Built-in health checks for dependencies (PostgreSQL, Redis)
- **Structured Logging**: Serilog integration with file and console outputs
- **Resilience**: Polly-based retry policies for fault tolerance
- **API Documentation**: Swagger/OpenAPI with interactive UI
- **Request Logging**: Comprehensive request/response logging middleware
- **Error Handling**: Centralized error handling with proper HTTP status codes

## 🏗️ Architecture

```
TemperatureApi/
├── Applications/
│   ├── Interfaces/
│   │   ├── ITemperatureRepository.cs
│   │   └── ITemperatureService.cs
│   └── Services/
│       └── TemperatureService.cs
├── Infrastructure/
│   ├── Policies/
│   │   └── RetryPolicy.cs
│   └── Repositories/
│       └── TemperatureRepository.cs
├── Models/
│   └── TemperatureModel.cs
├── Controller/
│   └── TemperatureController.cs
├── Properties/
│   └── launchSettings.json
├── Program.cs
├── appsettings.json
├── appsettings.Development.json
├── Dockerfile
└── docker-compose.yml
```

## 🚀 Getting Started

### Prerequisites

- .NET 8.0 SDK or later
- Docker & Docker Compose (for containerized deployment)
- PostgreSQL 15+ (if not using Docker)
- Redis 7+ (if not using Docker)

### Local Development

1. **Clone and navigate to the project**
   ```bash
   cd /Users/phil/Desktop/Dockerized/dotnet/IoT/TemperatureApi
   ```

2. **Restore dependencies**
   ```bash
   dotnet restore
   ```

3. **Build the project**
   ```bash
   dotnet build
   ```

4. **Run the application**
   ```bash
   dotnet run
   ```

The API will be available at `http://localhost:5000`

### Using Docker Compose

1. **Start all services**
   ```bash
   docker-compose up -d
   ```

2. **View logs**
   ```bash
   docker-compose logs -f api
   ```

3. **Stop services**
   ```bash
   docker-compose down
   ```

## 📖 API Endpoints

### Get Latest Temperatures

**Endpoint**: `GET /api/temps`

**Authentication**: Required (JWT Bearer Token)

**Query Parameters**:
- `page` (int, optional): Page number, default = 1, minimum = 1
- `pageSize` (int, optional): Records per page, default = 10, range = 1-100

**Rate Limit**: 20 requests per 10 seconds

**Example Request**:
```bash
curl -X GET "http://localhost:5000/api/temps?page=1&pageSize=10" \
  -H "Authorization: Bearer <YOUR_JWT_TOKEN>" \
  -H "Content-Type: application/json"
```

**Success Response (200 OK)**:
```json
[
  {
    "id": 1,
    "temperature": 23.5,
    "recordedAt": "2026-03-08T10:30:00Z"
  },
  {
    "id": 2,
    "temperature": 24.1,
    "recordedAt": "2026-03-08T10:35:00Z"
  }
]
```

**Bad Request Response (400)**:
```json
{
  "error": "Page must be >= 1, pageSize must be between 1-100"
}
```

**Unauthorized Response (401)**:
```json
{
  "type": "https://tools.ietf.org/html/rfc7235#section-3.1",
  "title": "Unauthorized",
  "status": 401,
  "detail": "Authorization header missing or invalid"
}
```

**Server Error Response (500)**:
```json
{
  "error": "An error occurred while fetching temperatures"
}
```

### Health Check

**Endpoint**: `GET /health`

**No Authentication Required**

**Example**:
```bash
curl http://localhost:5000/health
```

**Response (200 OK)**:
```json
{
  "status": "Healthy",
  "checks": {
    "PostgreSQL": "Healthy",
    "Redis": "Healthy"
  }
}
```

## 🔐 Authentication

The API uses JWT (JSON Web Tokens) for authentication. All endpoints except `/health` require a valid JWT token.

### JWT Configuration

See `appsettings.json`:
```json
"Jwt": {
  "Key": "s8D4mR7pL0qYz3VnK1bW9hFt6aCx2QeRZ5GvHjK0Lw=",
  "Issuer": "TemperatureApi.Api",
  "Audience": "TemperatureApi.Client"
}
```

### How to Get a JWT Token

You'll need to generate a JWT token from a token provider (typically another service). Once you have a token, include it in the Authorization header:

```
Authorization: Bearer <JWT_TOKEN>
```

## 🔄 Rate Limiting

The API implements a fixed window rate limiter:
- **Limit**: 20 requests
- **Window**: 10 seconds
- **Queue Limit**: 5 requests (queued after limit reached)

When rate limit is exceeded, the API returns a `429 Too Many Requests` response.

## 💾 Database

### Connection String

**appsettings.json**:
```
Host=localhost;Username=postgres;Password=root123;Database=iot_db;port=5432
```

### Required Tables

The API expects a `temperatures` table:

```sql
CREATE TABLE temperatures (
  id SERIAL PRIMARY KEY,
  value NUMERIC(5,2) NOT NULL,
  recorded_at TIMESTAMP NOT NULL,
  created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_recorded_at ON temperatures(recorded_at DESC);
```

### Schema

| Column | Type | Description |
|--------|------|-------------|
| id | SERIAL | Primary key |
| value | NUMERIC(5,2) | Temperature value |
| recorded_at | TIMESTAMP | When the temperature was recorded |
| created_at | TIMESTAMP | When the record was created |

## 🗂️ Caching

The API uses Redis for distributed caching with the following configuration:

**Cache Key Format**: `temps_{page}_{pageSize}`

**Cache Duration**: 30 seconds

**Cache Operations**:
- Cache HIT: Returns cached data (logged as "Cache HIT")
- Cache MISS: Fetches from database, stores in cache (logged as "Cache MISS")

To clear Redis cache:
```bash
redis-cli FLUSHDB
```

## 📊 Logging

### Log Configuration

Logs are written to:
1. **Console**: Real-time output
2. **File**: Daily rolling logs in `logs/` directory

### Log Levels

- **Information**: Standard operations (default)
- **Warning**: AspNetCore framework warnings
- **Error**: Application errors and exceptions

### Example Log Output

```
[INF] Request: GET /api/temps?page=1&pageSize=10 from 127.0.0.1
[INF] GET /api/temps called with page=1, pageSize=10
[INF] Cache MISS for temperatures page 1, fetching from repository
[INF] Cache HIT for temperatures page 1
```

View logs:
```bash
tail -f logs/temperature-api-*.log
```

## 🛡️ Resilience & Retry Policy

Database calls are protected with Polly retry policies:
- **Max Retries**: 3
- **Backoff**: Exponential
- **Timeout**: Configured per policy

This ensures temporary database connection issues don't immediately fail requests.

## 🐳 Docker Deployment

### docker-compose.yml Services

1. **api**: The ASP.NET Core application (port 5000)
2. **postgres**: PostgreSQL database (port 5432)
3. **redis**: Redis cache (port 6379)

### Environment Variables

The `docker-compose.yml` supports custom configuration via environment variables:

```bash
# Custom JWT secret
JWT_SECRET_KEY="your-secure-key" docker-compose up

# All environment variables
export JWT_SECRET_KEY="custom-key"
docker-compose up -d
```

### Docker Build

Build a custom image:
```bash
docker build -t temperature-api:latest .
```

Run the container:
```bash
docker run -p 5000:5000 \
  -e ConnectionStrings__Postgres="Host=postgres;Port=5432;Database=iot_db;Username=postgres;Password=password" \
  -e ConnectionStrings__Redis="redis:6379" \
  temperature-api:latest
```

## 📝 Development

### Swagger/OpenAPI

Access the interactive API documentation in development mode:

```
http://localhost:5000/swagger
```

This provides:
- All available endpoints
- Request/response schemas
- Try-it-out functionality
- Authentication configuration

### Configuration Files

- **appsettings.json**: Production settings
- **appsettings.Development.json**: Development overrides
- **launchSettings.json**: Launch profiles

### Adding New Endpoints

1. Create a new method in `TemperatureController.cs`
2. Decorate with appropriate HTTP method attribute (`[HttpGet]`, `[HttpPost]`, etc.)
3. Add authorization attributes if needed
4. Implement proper error handling with try-catch
5. Add logging for debugging

Example:
```csharp
[HttpPost]
[Authorize]
[EnableRateLimiting("fixed")]
public async Task<IActionResult> CreateTemperature([FromBody] TemperatureDto dto)
{
    try
    {
        _logger.LogInformation("Creating temperature record");
        // Implementation
        return Created($"/api/temps/{id}", result);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error creating temperature");
        return StatusCode(500, new { error = "Failed to create temperature" });
    }
}
```

## 🧪 Testing the API

### Using curl

```bash
# Health check (no auth needed)
curl http://localhost:5000/health

# Get temperatures (requires JWT)
curl -X GET "http://localhost:5000/api/temps?page=1&pageSize=5" \
  -H "Authorization: Bearer <YOUR_JWT_TOKEN>"

# Test rate limiting (should fail on 21st request)
for i in {1..25}; do
  curl -X GET "http://localhost:5000/api/temps?page=1" \
    -H "Authorization: Bearer <YOUR_JWT_TOKEN>"
  echo "Request $i"
done
```

### Using Swagger UI

1. Open `http://localhost:5000/swagger`
2. Click "Authorize" button
3. Enter JWT token in the format: `Bearer <your-token>`
4. Use "Try it out" on any endpoint

## 📚 Project Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| Dapper | 2.1.66 | Lightweight ORM |
| Npgsql | 10.0.1 | PostgreSQL driver |
| Polly | 8.6.6 | Resilience patterns |
| Serilog | 10.0.0 | Structured logging |
| AspNetCore.HealthChecks.NpgSql | 9.0.0 | Database health checks |
| AspNetCore.HealthChecks.Redis | 9.0.0 | Redis health checks |
| Microsoft.AspNetCore.Authentication.JwtBearer | 8.0.5 | JWT auth |
| Swashbuckle.AspNetCore | 6.6.2 | Swagger/OpenAPI |
| StackExchangeRedis | 10.0.3 | Redis caching |

## 🔧 Troubleshooting

### API Won't Start
- Check if ports 5000, 5432, 6379 are available
- Verify PostgreSQL and Redis are running
- Check logs: `logs/temperature-api-*.log`

### Database Connection Errors
- Verify PostgreSQL is accessible
- Check connection string in `appsettings.json`
- Ensure database `iot_db` exists
- Run schema setup script

### Redis Connection Errors
- Verify Redis is running on `localhost:6379`
- Check for Redis connectivity: `redis-cli ping`
- Review Docker network configuration if using containers

### JWT Validation Errors
- Ensure token is in format: `Authorization: Bearer <token>`
- Verify token hasn't expired
- Check token issuer matches `TemperatureApi.Api`
- Verify token audience matches `TemperatureApi.Client`

### Rate Limit Exceeded
- Wait 10 seconds for the window to reset
- Check if multiple clients are hitting the API
- Consider adjusting rate limit in `Program.cs` if needed

## 📈 Monitoring & Performance

### Key Metrics to Monitor

1. **Response Time**: Should be < 100ms (with cache)
2. **Cache Hit Rate**: Target > 80% for steady queries
3. **Database Connections**: Monitor PostgreSQL connection pool
4. **Redis Memory**: Monitor for memory leaks

### Performance Tips

1. **Increase Cache Duration**: Adjust `CacheExpirationSeconds` in `TemperatureService.cs`
2. **Database Optimization**: Add indexes on frequently queried columns
3. **Connection Pooling**: Configure PostgreSQL connection pool in connection string
4. **Load Testing**: Use tools like Apache JMeter or k6 to identify bottlenecks

## 🚢 Production Deployment

### Pre-Deployment Checklist

- [ ] Update JWT key to a secure value
- [ ] Set strong PostgreSQL password
- [ ] Configure production-level Redis replication
- [ ] Enable HTTPS/SSL
- [ ] Set `ASPNETCORE_ENVIRONMENT=Production`
- [ ] Configure logging rotation
- [ ] Set up monitoring and alerting
- [ ] Perform load testing
- [ ] Backup database regularly

### Deployment Steps

1. **Build the image**
   ```bash
   docker build -t temperature-api:v1.0 .
   ```

2. **Push to registry**
   ```bash
   docker tag temperature-api:v1.0 myregistry/temperature-api:v1.0
   docker push myregistry/temperature-api:v1.0
   ```

3. **Deploy with environment variables**
   ```bash
   docker run -d \
     -p 5000:5000 \
     -e ASPNETCORE_ENVIRONMENT=Production \
     -e Jwt__Key="<production-key>" \
     -e ConnectionStrings__Postgres="<prod-connection>" \
     -e ConnectionStrings__Redis="<prod-redis>" \
     myregistry/temperature-api:v1.0
   ```

## 📄 License

This project is part of the IoT Temperature Monitoring System.

## 👥 Contributing

For improvements and bug fixes, please refer to `IMPROVEMENTS.md` for recent changes and architectural decisions.

## 📞 Support

For issues or questions:
1. Check the logs: `logs/temperature-api-*.log`
2. Review the Swagger documentation at `/swagger`
3. Check the health endpoint: `/health`
4. Verify database and Redis connectivity

---

**Last Updated**: 8 March 2026  
**Version**: 1.0.0  
**Status**: Production Ready ✅
