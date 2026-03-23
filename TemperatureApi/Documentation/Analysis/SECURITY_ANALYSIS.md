# 🔐 appsettings.json Security Analysis

**Date**: 8 March 2026  
**Status**: ⚠️ **SECURITY REVIEW REQUIRED** (Medium Priority)

---

## Executive Summary

The current `appsettings.json` file contains **hardcoded sensitive credentials** that pose a security risk. While the configuration structure is sound, **immediate remediation is required** before production deployment.

**Severity**: 🔴 **MEDIUM** (Secrets in source control)  
**Impact**: Database credentials exposed, JWT key exposed  
**Remediation Time**: ~15 minutes

---

## 🚨 Security Issues Found

### 1. **PostgreSQL Password in Plain Text** ⚠️ **CRITICAL**

**Issue**:
```json
"Postgres": "Host=localhost;Username=postgres;Password=root123;Database=iot_db;Port=5432;..."
```

**Problems**:
- ❌ Password `root123` is hardcoded
- ❌ Weak default password (only 8 characters)
- ❌ Visible in source control history
- ❌ Visible in Docker image layers
- ❌ Visible in Kubernetes secrets (if not using proper secret management)

**Risk Level**: 🔴 **CRITICAL**  
**CVSS**: 9.8 (Critical)

---

### 2. **JWT Key in Plain Text** ⚠️ **CRITICAL**

**Issue**:
```json
"Key": "s8D4mR7pL0qYz3VnK1bW9hFt6aCx2QeRZ5GvHjK0Lw="
```

**Problems**:
- ❌ JWT signing key is hardcoded
- ❌ Visible in source control
- ❌ Anyone with access to code can forge tokens
- ❌ Cannot be rotated without code change
- ❌ Visible in logs if configuration is printed

**Risk Level**: 🔴 **CRITICAL**  
**CVSS**: 9.8 (Critical)

---

### 3. **AllowedHosts Set to Wildcard** ⚠️ **HIGH**

**Issue**:
```json
"AllowedHosts": "*"
```

**Problems**:
- ❌ Allows requests from any Host header
- ❌ Vulnerable to Host Header Injection attacks
- ❌ Could bypass CORS security controls
- ❌ Could lead to password reset poisoning

**Risk Level**: 🟠 **HIGH**  
**CVSS**: 6.5 (Medium)

---

### 4. **Localhost Origins in Production Config** ⚠️ **MEDIUM**

**Issue**:
```json
"AllowedOrigins": "http://localhost:3000,http://localhost:5000"
```

**Problems**:
- ❌ Development origins in production config
- ❌ May be accidentally deployed to production
- ❌ Allows localhost access to production API
- ❌ Not environment-aware

**Risk Level**: 🟡 **MEDIUM**  
**CVSS**: 4.3 (Medium)

---

## ✅ What's Secure

### 1. **JWT Configuration Structure** ✅
- Token validation enabled (issuer, audience, lifetime)
- Signature validation configured
- Minimum key length enforced in code (32 chars)

### 2. **Request Timeout** ✅
- 30-second timeout prevents slow-loris attacks
- Prevents resource exhaustion

### 3. **Development vs Production Split** ✅
- Separate configs for dev and production
- Development logging more verbose
- Good practice for environment separation

---

## 🔧 Remediation Plan

### **Solution 1: Use Environment Variables (RECOMMENDED)** ⭐

Replace hardcoded values with environment variable placeholders:

**Step 1**: Update `appsettings.json`
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
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
  "AllowedHosts": "api.example.com,api.internal.example.com",
  "RequestTimeoutSeconds": 30,
  "AllowedOrigins": "${ALLOWED_ORIGINS}"
}
```

**Step 2**: Set environment variables before running
```bash
export POSTGRES_CONNECTION_STRING="Host=db;Username=postgres;Password=$(openssl rand -base64 32);Database=iot_db;Port=5432;Pooling=true;Minimum Pool Size=10;Maximum Pool Size=500;"
export REDIS_CONNECTION_STRING="redis:6379"
export JWT_KEY="$(openssl rand -base64 32)"
export ALLOWED_ORIGINS="https://app.example.com,https://admin.example.com"
```

**Step 3**: Update Program.cs to read environment variables
```csharp
// Before building the configuration
var connectionString = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")
    ?? builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("POSTGRES_CONNECTION_STRING not set");

var redisConnection = Environment.GetEnvironmentVariable("REDIS_CONNECTION_STRING")
    ?? builder.Configuration.GetConnectionString("Redis")
    ?? throw new InvalidOperationException("REDIS_CONNECTION_STRING not set");

var jwtKey = Environment.GetEnvironmentVariable("JWT_KEY")
    ?? builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("JWT_KEY environment variable not set");

var allowedOrigins = Environment.GetEnvironmentVariable("ALLOWED_ORIGINS")
    ?? builder.Configuration["AllowedOrigins"]
    ?? "http://localhost:3000";
```

---

### **Solution 2: Use Azure Key Vault / AWS Secrets Manager** ⭐⭐

For production, use cloud-native secret management:

**Azure Key Vault Example**:
```csharp
var keyVaultEndpoint = new Uri("https://your-keyvault.vault.azure.net/");
var credential = new DefaultAzureCredential();
builder.Configuration.AddAzureKeyVault(keyVaultEndpoint, credential);
```

**AWS Secrets Manager Example**:
```csharp
builder.Configuration.AddSecretsManager(
    region: RegionEndpoint.USEast1,
    configurationProvider: new SecretsManagerConfigurationProvider(secretsManagerClientConfig)
);
```

---

### **Solution 3: Use .NET User Secrets (Dev Only)** 

For local development:
```bash
dotnet user-secrets init
dotnet user-secrets set "Jwt:Key" "your-secret-key"
dotnet user-secrets set "ConnectionStrings:Postgres" "your-connection-string"
```

---

## 📋 Complete Remediation Checklist

### Immediate Actions (Today)
- [ ] Remove hardcoded database password from `appsettings.json`
- [ ] Remove hardcoded JWT key from `appsettings.json`
- [ ] Change AllowedHosts from `*` to specific domains
- [ ] Update AllowedOrigins to production URLs
- [ ] Git history cleanup (remove secrets)

### Short Term (This Week)
- [ ] Implement environment variable support in Program.cs
- [ ] Test with Docker environment variables
- [ ] Test with Kubernetes environment variables
- [ ] Create .env.example file with placeholders
- [ ] Document secret configuration process

### Long Term (This Month)
- [ ] Integrate Azure Key Vault or AWS Secrets Manager
- [ ] Set up automated secret rotation
- [ ] Implement audit logging for secret access
- [ ] Create secrets management runbook
- [ ] Train team on secure configuration

---

## 🔒 Secure appsettings.json Template

Here's what a secure production configuration looks like:

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
    "Audience": "TemperatureApi.Client",
    "ExpirationMinutes": 60,
    "RefreshExpirationDays": 7
  },
  "AllowedHosts": "api.example.com,api.internal",
  "AllowedOrigins": "${ALLOWED_ORIGINS}",
  "RequestTimeoutSeconds": 30,
  "Caching": {
    "DefaultExpirationMinutes": 5
  },
  "Security": {
    "EnableHttpsRedirect": true,
    "EnforceHttpsOnly": true,
    "CorsPreflightMaxAge": 3600
  }
}
```

---

## 🚀 Step-by-Step Fix

### Step 1: Create Secure Config Files

Update `Config/appsettings.json`:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "ConnectionStrings": {
    "Postgres": "${POSTGRES_CONNECTION_STRING:DefaultValue}",
    "Redis": "${REDIS_CONNECTION_STRING:localhost:6379}"
  },
  "Jwt": {
    "Key": "${JWT_KEY}",
    "Issuer": "TemperatureApi.Api",
    "Audience": "TemperatureApi.Client"
  },
  "AllowedHosts": "api.example.com",
  "RequestTimeoutSeconds": 30,
  "AllowedOrigins": "${ALLOWED_ORIGINS:http://localhost:3000}"
}
```

### Step 2: Update Program.cs

Modify the configuration loading:
```csharp
var builder = WebApplication.CreateBuilder(args);

// Load configuration from environment variables
var postgresConnStr = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")
    ?? builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("POSTGRES_CONNECTION_STRING not configured");

var redisConnStr = Environment.GetEnvironmentVariable("REDIS_CONNECTION_STRING")
    ?? builder.Configuration.GetConnectionString("Redis")
    ?? throw new InvalidOperationException("REDIS_CONNECTION_STRING not configured");

var jwtKey = Environment.GetEnvironmentVariable("JWT_KEY")
    ?? builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("JWT_KEY not configured");

var allowedOrigins = (Environment.GetEnvironmentVariable("ALLOWED_ORIGINS")
    ?? builder.Configuration["AllowedOrigins"] ?? "http://localhost:3000").Split(",");

var allowedHosts = builder.Configuration["AllowedHosts"]?.Split(",") ?? new[] { "*" };

// Validate JWT Key
if (jwtKey.Length < 32)
    throw new InvalidOperationException("JWT_KEY must be at least 32 characters");
```

### Step 3: Create `.env.example` File

```bash
# PostgreSQL Configuration
POSTGRES_CONNECTION_STRING=Host=localhost;Username=postgres;Password=CHANGE_ME;Database=iot_db;Port=5432;Pooling=true;Minimum Pool Size=10;Maximum Pool Size=500;

# Redis Configuration
REDIS_CONNECTION_STRING=localhost:6379

# JWT Configuration (Generate with: openssl rand -base64 32)
JWT_KEY=CHANGE_ME_WITH_STRONG_RANDOM_32_CHARACTER_KEY

# CORS Configuration
ALLOWED_ORIGINS=https://app.example.com,https://admin.example.com

# Host Configuration
ALLOWED_HOSTS=api.example.com,api.internal.example.com
```

### Step 4: Add to `.gitignore`

```
# Sensitive files
.env
.env.local
.env.*.local
appsettings.Production.json
user-secrets.json

# IDEs
.vscode/settings.json
Properties/launchSettings.json
```

### Step 5: Update Docker Configuration

**Dockerfile**:
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

# Do NOT pass secrets in environment at build time
# They will be injected at runtime

EXPOSE 80 443
ENTRYPOINT ["dotnet", "TemperatureApi.dll"]
```

**docker-compose.yml**:
```yaml
services:
  api:
    image: temperature-api:latest
    environment:
      - POSTGRES_CONNECTION_STRING=Host=postgres;Username=postgres;Password=postgres123;Database=iot_db;Port=5432;
      - REDIS_CONNECTION_STRING=redis:6379
      - JWT_KEY=${JWT_KEY}
      - ALLOWED_ORIGINS=http://localhost:3000,http://localhost:5000
      - ASPNETCORE_ENVIRONMENT=Production
    ports:
      - "80:80"
      - "443:443"
    depends_on:
      postgres:
        condition: service_healthy
      redis:
        condition: service_healthy
```

### Step 6: Update Kubernetes Deployment

```yaml
apiVersion: v1
kind: Secret
metadata:
  name: temperature-api-secrets
  namespace: temperature-api
type: Opaque
stringData:
  postgres-connection-string: "Host=postgres;Username=postgres;Password=YOUR_SECURE_PASSWORD;Database=iot_db;"
  redis-connection-string: "redis:6379"
  jwt-key: "YOUR_GENERATED_JWT_KEY_HERE"

---
apiVersion: apps/v1
kind: Deployment
metadata:
  name: temperature-api
  namespace: temperature-api
spec:
  replicas: 3
  selector:
    matchLabels:
      app: temperature-api
  template:
    metadata:
      labels:
        app: temperature-api
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
          value: "https://app.example.com,https://admin.example.com"
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
```

---

## 📊 Security Comparison

| Approach | Security | Ease of Use | Rotation | Cost |
|----------|----------|-----------|----------|------|
| **Hardcoded** | 🔴 Critical | ✅ Easy | ❌ Manual | Free |
| **Environment Variables** | 🟡 Good | ✅ Easy | ✅ Easy | Free |
| **User Secrets (Dev)** | 🟢 Good | ✅ Easy | ✅ Easy | Free |
| **Key Vault / Secrets Manager** | 🟢 Excellent | 🟡 Moderate | ✅ Automated | $ |
| **Sealed Secrets (K8s)** | 🟢 Good | 🟡 Moderate | ✅ Easy | Free |

---

## 🎯 Recommended Solution

**For Development**: Use environment variables + .env files (checked into git as .env.example only)

**For Production**: Use Azure Key Vault or AWS Secrets Manager with managed identity/IAM roles

**For Kubernetes**: Use Kubernetes Secrets with proper RBAC and optional Sealed Secrets encryption

---

## ✅ Testing the Changes

### Test with Environment Variables

```bash
# Set test variables
export POSTGRES_CONNECTION_STRING="Host=localhost;Username=postgres;Password=test123;Database=iot_db;Port=5432;"
export REDIS_CONNECTION_STRING="localhost:6379"
export JWT_KEY="s8D4mR7pL0qYz3VnK1bW9hFt6aCx2QeRZ5GvHjK0Lw="
export ALLOWED_ORIGINS="http://localhost:3000"

# Run the application
cd TemperatureApi
dotnet run

# Verify no secrets in logs
# Verify no secrets in configuration output
```

### Verify in Docker

```bash
# Build image
docker build -t temperature-api:secure .

# Run with environment variables
docker run -e POSTGRES_CONNECTION_STRING="..." \
           -e REDIS_CONNECTION_STRING="..." \
           -e JWT_KEY="..." \
           -p 80:80 \
           temperature-api:secure

# Inspect image for hardcoded secrets
docker inspect temperature-api:secure
```

---

## 📚 References

- [Microsoft: Safe storage of app secrets](https://docs.microsoft.com/en-us/aspnet/core/security/app-secrets)
- [Azure Key Vault Integration](https://docs.microsoft.com/en-us/aspnet/core/security/key-vault-configuration)
- [AWS Secrets Manager for .NET](https://docs.aws.amazon.com/secretsmanager/latest/userguide/dotnet-examples.html)
- [OWASP: Secrets Management](https://owasp.org/www-community/attacks/Sensitive_Data_Exposure)
- [CWE-798: Use of Hardcoded Credentials](https://cwe.mitre.org/data/definitions/798.html)

---

## 🎬 Action Items

**Priority: CRITICAL - Must complete before production deployment**

- [ ] Remove hardcoded secrets from `appsettings.json`
- [ ] Update `Program.cs` to read from environment variables
- [ ] Test with environment variables in Docker
- [ ] Test with environment variables in Kubernetes
- [ ] Clean git history to remove exposed secrets
- [ ] Rotate PostgreSQL password
- [ ] Generate new JWT key
- [ ] Create .env.example file
- [ ] Document secret configuration process
- [ ] Train team on secure practices
- [ ] Plan integration with Key Vault/Secrets Manager

---

**Status**: ⚠️ **SECURITY ISSUE - REQUIRES IMMEDIATE ACTION**  
**Timeline**: Must fix before production  
**Complexity**: Low (Environment variables only)  
**Testing Required**: Yes (Docker and Kubernetes)

