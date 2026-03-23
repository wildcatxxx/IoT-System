# 🔄 Secure Configuration Fix - Before & After

---

## Current (INSECURE) vs Recommended (SECURE)

### Config File Comparison

#### ❌ CURRENT - appsettings.json (INSECURE)
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "ConnectionStrings": {
    "Postgres": "Host=localhost;Username=postgres;Password=root123;Database=iot_db;Port=5432;Pooling=true;Minimum Pool Size=10;Maximum Pool Size=500;Connection Idle Lifetime=600;",
    "Redis": "localhost:6379"
  },
  "Jwt": {
    "Key": "s8D4mR7pL0qYz3VnK1bW9hFt6aCx2QeRZ5GvHjK0Lw=",
    "Issuer": "TemperatureApi.Api",
    "Audience": "TemperatureApi.Client"
  },
  "AllowedHosts": "*",
  "RequestTimeoutSeconds": 30,
  "AllowedOrigins": "http://localhost:3000,http://localhost:5000"
}
```

#### ✅ RECOMMENDED - appsettings.json (SECURE)
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "System": "Warning"
    }
  },
  "ConnectionStrings": {
    "Postgres": "${POSTGRES_CONNECTION_STRING}",
    "Redis": "${REDIS_CONNECTION_STRING}"
  },
  "Jwt": {
    "Key": "${JWT_KEY}",
    "Issuer": "TemperatureApi.Api",
    "Audience": "TemperatureApi.Client"
  },
  "AllowedHosts": "api.example.com,api.internal",
  "RequestTimeoutSeconds": 30,
  "AllowedOrigins": "${ALLOWED_ORIGINS}"
}
```

**Changes**:
- ✅ Database password removed → environment variable
- ✅ Redis connection removed → environment variable
- ✅ JWT key removed → environment variable
- ✅ AllowedOrigins removed → environment variable
- ✅ AllowedHosts changed from `*` to specific domains
- ✅ Added System logging level

---

## Program.cs Configuration Reading

### ❌ CURRENT (Reads from config only)
```csharp
var jwtSettings = builder.Configuration.GetSection("Jwt");

// This reads the hardcoded key from appsettings.json
var jwtKey = jwtSettings["Key"] 
    ?? throw new InvalidOperationException("JWT_KEY environment variable not set.");
```

### ✅ RECOMMENDED (Reads from environment first)
```csharp
var jwtSettings = builder.Configuration.GetSection("Jwt");

// Try environment variable first, fall back to config
var jwtKey = Environment.GetEnvironmentVariable("JWT_KEY")
    ?? jwtSettings["Key"]
    ?? throw new InvalidOperationException("JWT_KEY not configured. Set JWT_KEY environment variable.");

if (string.IsNullOrEmpty(jwtKey) || jwtKey.Length < 32)
{
    throw new InvalidOperationException("JWT_KEY must be at least 32 characters. Generate with: openssl rand -base64 32");
}

// Load database connection with environment variables
var postgresConnStr = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")
    ?? builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("POSTGRES_CONNECTION_STRING not set");

var redisConnStr = Environment.GetEnvironmentVariable("REDIS_CONNECTION_STRING")
    ?? builder.Configuration.GetConnectionString("Redis")
    ?? throw new InvalidOperationException("REDIS_CONNECTION_STRING not set");

// Load CORS origins from environment
var allowedOrigins = (Environment.GetEnvironmentVariable("ALLOWED_ORIGINS") 
    ?? builder.Configuration["AllowedOrigins"] 
    ?? "http://localhost:3000").Split(",");
```

---

## Running the Application

### ❌ CURRENT (Hardcoded - Insecure)
```bash
# Secrets are already in the config file - INSECURE!
cd TemperatureApi
dotnet run

# Anyone with access to code sees the secrets
cat Config/appsettings.json  # 😱 Password visible!
```

### ✅ RECOMMENDED (Environment Variables - Secure)
```bash
# 1. Generate secrets
JWT_KEY=$(openssl rand -base64 32)
DB_PASSWORD=$(openssl rand -base64 16)

# 2. Set environment variables
export POSTGRES_CONNECTION_STRING="Host=localhost;Username=postgres;Password=$DB_PASSWORD;Database=iot_db;Port=5432;Pooling=true;Minimum Pool Size=10;Maximum Pool Size=500;"
export REDIS_CONNECTION_STRING="localhost:6379"
export JWT_KEY="$JWT_KEY"
export ALLOWED_ORIGINS="https://app.example.com,https://admin.example.com"

# 3. Run application
cd TemperatureApi
dotnet run

# 4. Verify config file has no secrets
cat Config/appsettings.json  # ✓ Safe to share!
```

---

## Docker Deployment

### ❌ CURRENT (Secrets in image)
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY . .
RUN dotnet publish -c Release -o /app/publish
COPY --from=build /app/publish .

# Problem: appsettings.json with secrets is baked into image!
# Anyone with access to image can extract secrets
```

**Security Issue**: Secrets are baked into Docker layers and visible via `docker history`

### ✅ RECOMMENDED (Secrets from environment)
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["TemperatureApi.csproj", "./"]
RUN dotnet restore
COPY . .
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish

FROM base
WORKDIR /app
COPY --from=publish /app/publish .

# Do NOT include secrets here - they come from environment at runtime
EXPOSE 80 443
ENTRYPOINT ["dotnet", "TemperatureApi.dll"]
```

**How to run**:
```bash
# Build image (no secrets in build)
docker build -t temperature-api:latest .

# Run with environment variables
docker run -e POSTGRES_CONNECTION_STRING="..." \
           -e REDIS_CONNECTION_STRING="..." \
           -e JWT_KEY="..." \
           -e ALLOWED_ORIGINS="https://app.example.com" \
           -p 80:80 \
           temperature-api:latest
```

---

## Docker Compose

### ❌ CURRENT (Secrets in compose file)
```yaml
services:
  api:
    environment:
      - POSTGRES_CONNECTION_STRING=Host=postgres;Username=postgres;Password=root123;...
      - JWT_KEY=s8D4mR7pL0qYz3VnK1bW9hFt6aCx2QeRZ5GvHjK0Lw=
```

**Security Issue**: Secrets in docker-compose.yml (checked into source control)

### ✅ RECOMMENDED (Secrets from .env file)
```yaml
# docker-compose.yml - SAFE to commit to git
services:
  api:
    image: temperature-api:latest
    environment:
      - POSTGRES_CONNECTION_STRING=${POSTGRES_CONNECTION_STRING}
      - REDIS_CONNECTION_STRING=${REDIS_CONNECTION_STRING}
      - JWT_KEY=${JWT_KEY}
      - ALLOWED_ORIGINS=${ALLOWED_ORIGINS}
      - ASPNETCORE_ENVIRONMENT=Production
    ports:
      - "80:80"
      - "443:443"
    depends_on:
      postgres:
        condition: service_healthy
```

**Create .env file** (do NOT commit to git):
```bash
# .env - LOCAL ONLY, add to .gitignore
POSTGRES_CONNECTION_STRING=Host=postgres;Username=postgres;Password=YOUR_SECURE_PASSWORD;Database=iot_db;Port=5432;Pooling=true;Minimum Pool Size=10;Maximum Pool Size=500;
REDIS_CONNECTION_STRING=redis:6379
JWT_KEY=YOUR_GENERATED_KEY_FROM_openssl_rand
ALLOWED_ORIGINS=http://localhost:3000,http://localhost:5000
```

**How to run**:
```bash
# Create .env from .env.example
cp .env.example .env

# Edit .env with your secrets (LOCAL ONLY)
nano .env

# Run docker-compose (reads from .env)
docker-compose up -d

# Verify: .env is in .gitignore, so secrets don't leak
cat .gitignore  # Should include: .env
```

---

## Kubernetes Deployment

### ❌ CURRENT (Hardcoded secrets)
```yaml
# NOT RECOMMENDED
apiVersion: apps/v1
kind: Deployment
metadata:
  name: temperature-api
spec:
  template:
    spec:
      containers:
      - name: temperature-api
        env:
        - name: JWT_KEY
          value: "s8D4mR7pL0qYz3VnK1bW9hFt6aCx2QeRZ5GvHjK0Lw="  # 😱 Hardcoded!
```

**Security Issues**:
- Secrets visible in YAML files
- Stored in etcd in plain text
- Visible to anyone with kubectl access

### ✅ RECOMMENDED (Kubernetes Secrets)
```yaml
# 1. Create Secret resource
apiVersion: v1
kind: Secret
metadata:
  name: temperature-api-secrets
  namespace: temperature-api
type: Opaque
stringData:
  postgres-connection-string: |
    Host=postgres;Username=postgres;Password=YOUR_SECURE_PASSWORD;Database=iot_db;Port=5432;
  redis-connection-string: "redis:6379"
  jwt-key: "YOUR_GENERATED_JWT_KEY"

---
# 2. Reference Secret in Deployment
apiVersion: apps/v1
kind: Deployment
metadata:
  name: temperature-api
  namespace: temperature-api
spec:
  replicas: 3
  template:
    spec:
      containers:
      - name: temperature-api
        image: temperature-api:latest
        env:
        - name: POSTGRES_CONNECTION_STRING
          valueFrom:
            secretKeyRef:
              name: temperature-api-secrets
              key: postgres-connection-string
        - name: REDIS_CONNECTION_STRING
          valueFrom:
            secretKeyRef:
              name: temperature-api-secrets
              key: redis-connection-string
        - name: JWT_KEY
          valueFrom:
            secretKeyRef:
              name: temperature-api-secrets
              key: jwt-key
        - name: ALLOWED_ORIGINS
          value: "https://app.example.com"
```

**How to deploy**:
```bash
# 1. Create namespace
kubectl create namespace temperature-api

# 2. Create Secret (from a secure source, not from git)
kubectl create secret generic temperature-api-secrets \
  --from-literal=postgres-connection-string="..." \
  --from-literal=redis-connection-string="..." \
  --from-literal=jwt-key="$(openssl rand -base64 32)" \
  -n temperature-api

# 3. Apply deployment
kubectl apply -f k8s-deployment.yaml

# 4. Verify Secret is encrypted in etcd
# (Secrets are still vulnerable in etcd unless encryption is enabled)
```

---

## Comparison Summary

| Aspect | Current | Recommended |
|--------|---------|-------------|
| **Secrets Location** | Config file | Environment variables |
| **Source Control Risk** | ⚠️ High | ✅ None |
| **Docker Image Risk** | ⚠️ High | ✅ None |
| **Secret Rotation** | ❌ Requires code change | ✅ Just restart container |
| **Multiple Environments** | ❌ Same secrets everywhere | ✅ Different secrets per env |
| **Easy to Audit** | ❌ Secrets in files | ✅ No secrets in files |
| **Safe to Share Code** | ❌ No | ✅ Yes |
| **Production Ready** | ❌ No | ✅ Yes |

---

## Implementation Checklist

### Step 1: Update Config Files ✅
- [ ] Remove hardcoded passwords from `Config/appsettings.json`
- [ ] Remove hardcoded JWT key from `Config/appsettings.json`
- [ ] Change `AllowedHosts` from `*` to specific domains
- [ ] Add environment variable placeholders

### Step 2: Update Code ✅
- [ ] Update `Program.cs` to read from environment variables
- [ ] Add fallback to config file (environment takes precedence)
- [ ] Add validation for required secrets

### Step 3: Test Locally ✅
- [ ] Set environment variables
- [ ] Run `dotnet run` and verify it works
- [ ] Verify config file has no secrets

### Step 4: Test with Docker ✅
- [ ] Build Docker image
- [ ] Run with environment variables
- [ ] Verify docker image has no secrets

### Step 5: Test with Kubernetes ✅
- [ ] Create Kubernetes Secrets
- [ ] Deploy with secret references
- [ ] Verify pods have correct environment

### Step 6: Clean Up ✅
- [ ] Add `.env*` to `.gitignore`
- [ ] Create `.env.example` with placeholders
- [ ] Clean git history to remove exposed secrets
- [ ] Rotate all exposed secrets (new DB password, new JWT key)

### Step 7: Document ✅
- [ ] Document how to set environment variables
- [ ] Document how to generate new secrets
- [ ] Create operations runbook

---

## Estimated Timeline

- **Config changes**: 5 minutes
- **Code updates**: 5 minutes
- **Local testing**: 5 minutes
- **Docker testing**: 5 minutes
- **Kubernetes testing**: 5 minutes
- **Git history cleanup**: 5 minutes
- **Documentation**: 10 minutes

**Total**: ~40 minutes

---

## After Implementation

✅ **Secure**:
- No secrets in source control
- No secrets in Docker images
- Different secrets per environment
- Easy to rotate secrets
- Audit trail of access
- Production-ready

✅ **Operational Benefits**:
- Secrets managed separately
- No code changes for secret rotation
- Easy to onboard new team members
- Clear separation of concerns
- Better security posture

---

**Next Steps**: Implement these changes following the step-by-step guide above! 🚀

