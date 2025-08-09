# ERP Weather Service - Pipeline Ready

A streamlined Weather Service application built with .NET 10, featuring microservices architecture with API Gateway, centralized authentication, ELK Stack logging, and CI/CD pipeline ready for deployment.

## 🏗️ Architecture Overview

```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   BFF Gateway   │────│  Weather Service │    │ Identity Service │
│   (Port 5000)   │    │   (Port 5001)   │    │ HTTP: 5007      │
│                 │    │                 │    │ gRPC: 5008      │
│ • YARP Proxy    │    │ • Weather API   │    │                 │
│ • gRPC API Auth │    │ • Health Checks │    │ • API Key Mgmt  │
│ • Header Sanit. │    │ • Scalar Docs   │    │ • Redis Cache   │
│ • Rate Limiting │    │ • OpenAPI       │    │ • gRPC Service  │
│ • ELK Logging   │    │                 │    │                 │
└─────────────────┘    └─────────────────┘    └─────────────────┘
         │                       │                       │
         │ gRPC ValidateApiKey   │                       │
         └───────────────────────┼───────────────────────┘
                                 │
        ┌────────────────────────┼─────────────┐
        │                       │             │
┌─────────────────┐    ┌─────────────────┐    │
│  Elasticsearch  │    │      Redis      │    │
│   (Port 9200)   │    │   (Port 6379)   │    │
│                 │    │                 │    │
│ • Log Storage   │    │ • API Key Store │    │
│ • Search Index  │    │ • User Sessions │    │
│ • Analytics     │    │ • Permissions   │    │
└─────────────────┘    └─────────────────┘    │
         │                                    │
         │              ┌─────────────────────┘
         │              │
┌─────────────────┐    ┌─────────────────┐
│     Kibana      │    │    Logstash     │
│   (Port 5601)   │    │   (Port 5044)   │
│                 │    │                 │
│ • Dashboards    │    │ • Log Processing│
│ • Visualizations│    │ • Data Pipeline │
│ • Log Analysis  │    │ • Filtering     │
└─────────────────┘    └─────────────────┘
```

### 🔐 API Key Validation Pipeline

The BFF Gateway uses **gRPC** to validate API keys through the Identity Service:

1. **Client Request** → BFF Gateway (with `X-API-Key` header)
2. **BFF Gateway** → Identity Service (gRPC `ValidateApiKey` call on port 5008)
3. **Identity Service** → Redis (API key lookup and validation)
4. **Header Sanitization** → BFF Gateway removes sensitive headers (`X-API-Key`, etc.)
5. **User Context** → BFF Gateway adds user headers (`X-User-Id`, `X-User-Name`, `X-User-Permissions`)
6. **Response** → BFF Gateway → Downstream Service (with sanitized headers)

### 📊 Logging Pipeline

The BFF Gateway implements comprehensive **ELK Stack logging**:

1. **Request Capture** → RequestLoggingMiddleware captures all HTTP requests/responses
2. **Structured Logging** → Serilog formats logs with correlation IDs and metadata
3. **Elasticsearch Storage** → Logs are indexed in Elasticsearch with time-based indices
4. **Kibana Visualization** → Real-time dashboards and analytics for monitoring

## 🚀 Services

### Core Services
- **🚪 BFF Gateway** (Port 5000) - API Gateway with YARP reverse proxy, gRPC authentication, header sanitization, and comprehensive logging
- **🌤️ Weather Service** (Port 5001) - Weather forecast and meteorological data with Scalar documentation
- **🔐 Identity Service** (HTTP: 5007, gRPC: 5008) - API key validation, user management, and authentication
- **🗄️ Redis** (Port 6379) - Distributed caching, API key storage, and session management

### Logging & Monitoring Services
- **🔍 Elasticsearch** (Port 9200) - Log storage, search, and analytics engine
- **📊 Kibana** (Port 5601) - Interactive dashboards and log visualization
- **🔄 Logstash** (Port 5044) - Log processing and data pipeline

## 🔑 API Key Authentication

The system uses centralized API key authentication with **gRPC** communication between BFF Gateway and Identity Service:

### Available API Keys
- **Admin Master**: `LGplFG5SbbcuGStQIBSlf2GGTStli3ZFdcGaMOhA4qM` (admin permissions)
- **Dev Team Lead**: `oxXGrzB51BiGor3pqAU0u5n5N20bI3cSBn3JM7zZWxM` (read/write permissions)
- **QA Automation**: `HGnDTMAoIgWe7HtQGSUuag1zXyXggTNoN2R5BGFcfvE` (read permissions)

### Usage
```bash
# Test Weather Service
curl -X GET "http://localhost:5000/api/weather/weatherforecast" \
  -H "X-API-Key: LGplFG5SbbcuGStQIBSlf2GGTStli3ZFdcGaMOhA4qM"

# Test without API key (should return 401)
curl -X GET "http://localhost:5000/api/weather/weatherforecast"

# Test Identity Service directly (HTTP endpoint)
curl -X POST "http://localhost:5007/validate" \
  -H "Content-Type: application/json" \
  -d '{"ApiKey":"LGplFG5SbbcuGStQIBSlf2GGTStli3ZFdcGaMOhA4qM","ServiceName":"WeatherService","Endpoint":"/weatherforecast"}'
```

## � ELK Stack Logging

### Overview
The BFF Gateway implements comprehensive logging using the **ELK Stack** (Elasticsearch, Logstash, Kibana) for real-time monitoring and analytics.

### Features
- **🔍 Request/Response Logging** - Detailed capture of all HTTP traffic
- **🏷️ Correlation Tracking** - Unique request IDs for tracing
- **⚡ Performance Monitoring** - Response time analysis and alerting
- **🎯 Structured Logging** - JSON-formatted logs with metadata
- **📈 Real-time Dashboards** - Kibana visualizations and analytics

### Quick Setup
```powershell
# Complete ELK setup with BFF Gateway
.\setup-elk-logging.ps1

# Or step-by-step
docker-compose -f docker-compose.elk.yml up -d  # Start ELK Stack
.\elk-management.ps1 start                       # Check services
dotnet run --project src/Gateway/BFF.Gateway     # Start BFF Gateway
.\setup-kibana-dashboards.ps1                    # Configure Kibana
```

### Service URLs
- **📊 Kibana Dashboard**: http://localhost:5601
- **🔍 Elasticsearch API**: http://localhost:9200
- **🔄 Logstash**: http://localhost:5044
- **🚪 BFF Gateway**: http://localhost:5000

### Kibana Usage
1. Open **Kibana** at http://localhost:5601
2. Go to **"Discover"** tab
3. Select **"bff-gateway-logs-*"** index pattern
4. Filter by time range and explore logs

### Useful Queries
```
# All requests
service_name:"bff-gateway"

# Slow requests (>1 second)
response.ElapsedMs:>1000

# Error responses
response.StatusCode:>=400

# Specific endpoint
request.Path:"/api/weather"

# By correlation ID
correlation_id:"YOUR-CORRELATION-ID"
```

### Management Scripts
```powershell
# ELK Stack management
.\elk-management.ps1 [start|stop|status|kibana|test]

# Setup Kibana dashboards
.\setup-kibana-dashboards.ps1

# Complete setup
.\setup-elk-logging.ps1
```

## �🛠️ Development Setup

### Prerequisites
- .NET 10 SDK
- Docker & Docker Compose
- Redis (for API key storage and caching)
- PowerShell (for build scripts)

### Quick Start
```bash
# Clone repository
git clone <repository-url>
cd ERPPrototype

# Option 1: Complete setup with ELK logging
./setup-elk-logging.ps1

# Option 2: Basic setup
# Start Redis (required for API key storage)
docker run -d --name erp-redis -p 6379:6379 redis:7-alpine

# Build and run locally (in separate terminals)
./scripts/build.ps1
dotnet run --project src/Services/ERP.IdentityService      # HTTP: 5007, gRPC: 5008
dotnet run --project src/Services/Playground.WeatherService # Port 5001
dotnet run --project src/Gateway/BFF.Gateway               # Port 5000

# Or use Docker Compose
docker-compose up -d
```

## 🚀 CI/CD Pipeline

### GitHub Actions Workflow
The project includes a complete CI/CD pipeline (`.github/workflows/ci-cd.yml`):

1. **Build & Test** - Compile, test, and generate coverage
2. **Security Scan** - Trivy vulnerability scanning
3. **Docker Build** - Multi-service container builds
4. **Deploy Dev** - Automatic deployment to development
5. **Deploy Prod** - Manual deployment to production

### Build Scripts
```powershell
# Build everything
./scripts/build.ps1 -BuildDocker

# Deploy locally
./scripts/deploy.ps1 -Environment local -WaitForHealthy

# Deploy to development
./scripts/deploy.ps1 -Environment dev
```

## 🐳 Docker Support

### Local Development
```bash
# Start all services
docker-compose up -d

# View logs
docker-compose logs -f

# Stop services
docker-compose down
```

### Production Images
- `erp-weather-service:latest`
- `erp-identity-service:latest`
- `erp-bff-gateway:latest`

## ☸️ Kubernetes Deployment

### Manifests
- `k8s/namespace.yaml` - ERP system namespace
- `k8s/redis.yaml` - Redis cache deployment
- `k8s/weather-service.yaml` - Weather service with HPA

### Deploy to Kubernetes
```bash
kubectl apply -f k8s/namespace.yaml
kubectl apply -f k8s/redis.yaml
kubectl apply -f k8s/weather-service.yaml
```

## 📊 Monitoring & Health Checks

### Health Endpoints
- BFF Gateway: `http://localhost:5000/health`
- Weather Service: `http://localhost:5001/health`
- Identity Service: `http://localhost:5007/health`

### API Documentation
- Weather Service (Scalar): `http://localhost:5001/scalar/v1`
- Weather Service (OpenAPI): `http://localhost:5001/openapi/v1.json`

### Service Discovery
- Service mappings: `http://localhost:5000/api/gateway/services`

## 🧪 Testing

### API Testing
```bash
# Test without API key (should return 401)
curl -i http://localhost:5000/api/weather/weatherforecast

# Test with valid API key (should succeed)
curl -i http://localhost:5000/api/weather/weatherforecast \
  -H "X-API-Key: LGplFG5SbbcuGStQIBSlf2GGTStli3ZFdcGaMOhA4qM"

# Test gRPC validation (BFF Gateway uses gRPC internally)
# The above requests will trigger gRPC calls to Identity Service on port 5008
```

### Load Testing
```bash
# Install k6 for load testing
# Test weather endpoint
k6 run --vus 10 --duration 30s scripts/load-test.js
```

## 🔧 Configuration

### Environment Variables
- `ASPNETCORE_ENVIRONMENT` - Development/Production
- `ConnectionStrings__Redis` - Redis connection string
- `IdentityService__GrpcUrl` - Identity service gRPC URL (default: http://localhost:5008)
- `IdentityService__RestUrl` - Identity service HTTP URL (default: http://localhost:5007)

### Service Configuration
- BFF Gateway: `src/Gateway/BFF.Gateway/appsettings.json`
- Weather Service: `src/Services/Playground.WeatherService/appsettings.json`
- Identity Service: `src/Services/ERP.IdentityService/appsettings.json`

## 📈 Scaling

### Horizontal Pod Autoscaler (HPA)
The Weather service includes HPA configuration:
- Min replicas: 2
- Max replicas: 10
- CPU target: 70%
- Memory target: 80%

### Load Balancing
- YARP handles load balancing in the BFF Gateway
- Kubernetes services provide load balancing for pods

## 🔒 Security

### API Key Management
- Centralized through Identity Service with gRPC communication
- Redis-backed storage for high performance
- Configurable expiration and role-based permissions
- Header sanitization removes sensitive data before downstream services

### Communication Security
- gRPC communication between BFF Gateway and Identity Service
- API keys removed from headers before reaching downstream services
- User context headers added for service-to-service communication

### Container Security
- Trivy vulnerability scanning in CI/CD
- Non-root container users
- Minimal base images (Alpine Linux)

## ✅ Current Implementation Status

### Completed Features
- ✅ **gRPC API Key Validation** - BFF Gateway uses gRPC to validate API keys with Identity Service
- ✅ **Header Sanitization** - Sensitive headers (API keys) are removed before reaching downstream services
- ✅ **User Context Headers** - User information is added to requests for downstream services
- ✅ **Redis Integration** - API keys and user data stored in Redis for high performance
- ✅ **Scalar Documentation** - Modern API documentation for Weather Service
- ✅ **Service Discovery** - Dynamic service mapping configuration
- ✅ **Health Checks** - Comprehensive health monitoring for all services

### Architecture Highlights
- **Microservices Communication**: gRPC for internal service communication (BFF ↔ Identity)
- **Security**: API key validation with automatic header sanitization
- **Performance**: Redis caching for API key lookups and user sessions
- **Documentation**: Scalar-based API documentation with interactive testing
- **Scalability**: YARP reverse proxy with load balancing capabilities

## 📝 Next Steps

1. **Add Unit Tests** - Expand test coverage for all services
2. **Add Integration Tests** - End-to-end API testing with gRPC validation
3. **Add Monitoring** - Prometheus/Grafana integration with gRPC metrics
4. **Add Logging** - Structured logging with Serilog and correlation IDs
5. **Add Tracing** - OpenTelemetry distributed tracing across gRPC calls

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch
3. Make changes and add tests
4. Run `./scripts/build.ps1` to verify
5. Submit a pull request

## 📄 License

This project is licensed under the MIT License.
