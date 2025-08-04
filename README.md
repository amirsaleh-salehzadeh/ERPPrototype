# ERP Weather Service - Pipeline Ready

A streamlined Weather Service application built with .NET 10, featuring microservices architecture with API Gateway, centralized authentication, and CI/CD pipeline ready for deployment.

## 🏗️ Architecture Overview

```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   BFF Gateway   │────│  Weather Service │    │ Identity Service │
│   (Port 5000)   │    │   (Port 5001)   │    │   (Port 5007)   │
│                 │    │                 │    │                 │
│ • YARP Proxy    │    │ • Weather API   │    │ • API Key Mgmt  │
│ • gRPC API Auth │    │ • Health Checks │    │ • Redis Cache   │
│ • Rate Limiting │    │ • Swagger/OAS   │    │ • gRPC Service  │
└─────────────────┘    └─────────────────┘    └─────────────────┘
         │                       │                       │
         └───────────────────────┼───────────────────────┘
                                 │
                        ┌─────────────────┐
                        │      Redis      │
                        │   (Port 6379)   │
                        │                 │
                        │ • API Key Store │
                        │ • Session Cache │
                        │ • Rate Limiting │
                        └─────────────────┘
```

### 🔐 API Key Validation Pipeline

The BFF Gateway uses **gRPC** to validate API keys through the Identity Service:

1. **Client Request** → BFF Gateway (with `X-API-Key` header)
2. **BFF Gateway** → Identity Service (gRPC `ValidateApiKey` call)
3. **Identity Service** → Redis (API key lookup and validation)
4. **Response** → BFF Gateway → Downstream Service (with user context headers)

## 🚀 Services

### Core Services
- **🚪 BFF Gateway** (Port 5000) - API Gateway with YARP reverse proxy and authentication
- **🌤️ Weather Service** (Port 5001) - Weather forecast and meteorological data  
- **🔐 Identity Service** (Port 5007) - API key validation and authentication
- **🗄️ Redis** (Port 6379) - Caching and API key storage

## 🔑 API Key Authentication

The system uses centralized API key authentication:

**Current Admin API Key**: `QNT31UDQeXrLnojw3GpVptmgLqfypfh5nWjCghyFo3U`

### Usage
```bash
# Test Weather Service
curl -X GET "http://localhost:5000/api/weather/hello" \
  -H "X-API-Key: QNT31UDQeXrLnojw3GpVptmgLqfypfh5nWjCghyFo3U"

# Get Weather Forecast
curl -X GET "http://localhost:5000/api/weather/weatherforecast" \
  -H "X-API-Key: QNT31UDQeXrLnojw3GpVptmgLqfypfh5nWjCghyFo3U"
```

## 🛠️ Development Setup

### Prerequisites
- .NET 10 SDK
- Docker & Docker Compose
- Redis (for API key storage - optional, falls back to in-memory)
- PowerShell (for build scripts)

### Quick Start
```bash
# Clone repository
git clone <repository-url>
cd ERPPrototype

# Build and run locally
./scripts/build.ps1
dotnet run --project src/Services/Playground.WeatherService
dotnet run --project src/Services/ERP.IdentityService  
dotnet run --project src/Gateway/BFF.Gateway

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

### Service Discovery
- Service mappings: `http://localhost:5000/api/gateway/services`

## 🧪 Testing

### API Testing
```bash
# Test without API key (should fail)
curl http://localhost:5000/api/weather/hello

# Test with valid API key (should succeed)
curl http://localhost:5000/api/weather/hello \
  -H "X-API-Key: QNT31UDQeXrLnojw3GpVptmgLqfypfh5nWjCghyFo3U"
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
- `IdentityService__RestUrl` - Identity service URL

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
- Centralized through Identity Service
- Redis-backed storage with in-memory fallback
- Configurable expiration and permissions

### Container Security
- Trivy vulnerability scanning in CI/CD
- Non-root container users
- Minimal base images (Alpine Linux)

## 📝 Next Steps

1. **Add Unit Tests** - Expand test coverage
2. **Add Integration Tests** - End-to-end API testing
3. **Add Monitoring** - Prometheus/Grafana integration
4. **Add Logging** - Structured logging with Serilog
5. **Add Tracing** - OpenTelemetry distributed tracing

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch
3. Make changes and add tests
4. Run `./scripts/build.ps1` to verify
5. Submit a pull request

## 📄 License

This project is licensed under the MIT License.
