#!/bin/bash

# Nethermind Arbitrum Monitoring Setup Script
# This script starts the monitoring stack (Prometheus, Grafana, Pushgateway)

set -e

echo "🚀 Starting Nethermind Arbitrum Monitoring Stack..."

# Check if Docker is running
if ! docker info > /dev/null 2>&1; then
    echo "❌ Docker is not running. Please start Docker and try again."
    exit 1
fi

# Check if docker-compose is available
if ! command -v docker-compose &> /dev/null; then
    echo "❌ docker-compose is not installed. Please install docker-compose and try again."
    exit 1
fi

# Create monitoring directories if they don't exist
mkdir -p monitoring/{prometheus,grafana/{provisioning/{datasources,dashboards},dashboards}}

echo "📊 Starting monitoring services..."
docker-compose -f docker-compose.monitoring.yml up -d

# Wait for services to start
echo "⏳ Waiting for services to start..."
sleep 10

# Check if services are running
if docker-compose -f docker-compose.monitoring.yml ps | grep -q "Up"; then
    echo "✅ Monitoring stack started successfully!"
    echo ""
    echo "📈 Access your monitoring tools:"
    echo "   • Grafana:     http://localhost:3000 (admin/nethermind123)"
    echo "   • Prometheus:  http://localhost:9090"
    echo "   • Pushgateway: http://localhost:9091"
    echo ""
    echo "🔧 To start Nethermind with metrics enabled:"
    echo "   make run-sepolia  # (uses command-line metrics configuration)"
    echo ""
    echo "🔧 Alternative - use your existing config with metrics overlay:"
    echo "   dotnet run --project src/Nethermind.Arbitrum -- --config src/Nethermind.Arbitrum/Properties/configs/arbitrum-sepolia-archive.json --Metrics.Enabled true --Metrics.ExposePort 8008 --Metrics.ExposeHost 0.0.0.0"
    echo ""
    echo "📋 To view logs:"
    echo "   docker-compose -f docker-compose.monitoring.yml logs -f"
    echo ""
    echo "🛑 To stop monitoring:"
    echo "   docker-compose -f docker-compose.monitoring.yml down"
else
    echo "❌ Failed to start monitoring services. Check logs with:"
    echo "   docker-compose -f docker-compose.monitoring.yml logs"
    exit 1
fi
