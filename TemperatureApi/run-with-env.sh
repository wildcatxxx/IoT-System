#!/bin/bash
# Setup script to run TemperatureApi with secure environment variables
# Usage: ./run-with-env.sh

set -e

echo "🔐 TemperatureApi - Secure Environment Setup"
echo "=============================================="

# Generate secure random keys if not already set
if [ -z "$JWT_KEY" ]; then
    echo "📝 Generating JWT_KEY..."
    JWT_KEY=$(openssl rand -base64 32)
fi

if [ -z "$POSTGRES_PASSWORD" ]; then
    echo "📝 Generating POSTGRES_PASSWORD..."
    POSTGRES_PASSWORD=$(openssl rand -base64 16)
fi

# Export environment variables
export JWT_KEY
export POSTGRES_PASSWORD
export POSTGRES_CONNECTION_STRING="Host=localhost;Username=postgres;Password=$POSTGRES_PASSWORD;Database=iot_db;Port=5432;Pooling=true;Minimum Pool Size=10;Maximum Pool Size=500;Connection Idle Lifetime=600;"
export REDIS_CONNECTION_STRING="${REDIS_CONNECTION_STRING:-localhost:6379}"
export ALLOWED_ORIGINS="${ALLOWED_ORIGINS:-http://localhost:3000,http://localhost:5000}"
export ASPNETCORE_ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-Development}"

echo ""
echo "✅ Environment variables configured:"
echo "   JWT_KEY: (hidden - $((${#JWT_KEY})) characters)"
echo "   POSTGRES_PASSWORD: (hidden - $((${#POSTGRES_PASSWORD})) characters)"
echo "   POSTGRES_CONNECTION_STRING: Host=localhost;..."
echo "   REDIS_CONNECTION_STRING: $REDIS_CONNECTION_STRING"
echo "   ALLOWED_ORIGINS: $ALLOWED_ORIGINS"
echo "   ASPNETCORE_ENVIRONMENT: $ASPNETCORE_ENVIRONMENT"
echo ""

# Build and run
echo "🔨 Building application..."
dotnet build

echo ""
echo "🚀 Running TemperatureApi..."
dotnet run
