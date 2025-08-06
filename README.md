# ERP Weather Service - Pipeline Ready

A streamlined Weather Service application built with .NET 10, featuring microservices architecture with API Gateway, centralized authentication, and CI/CD pipeline ready for deployment.

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
└─────────────────┘    └─────────────────┘    └─────────────────┘
         │                       │                       │
         │ gRPC ValidateApiKey   │                       │
         └───────────────────────┼───────────────────────┘
                                 │
                        ┌─────────────────┐
                        │      Redis      │
                        │   (Port 6379)   │
                        │                 │
                        │ • API Key Store │
                        │ • User Sessions │
                        │ • Permissions   │
                        └─────────────────┘
```

### 🔐 Hybrid Authentication Pipeline

The BFF Gateway supports **both JWT tokens and API keys** for authentication:

#### JWT Authentication (Recommended)
1. **Client Request** → BFF Gateway (with `Authorization: Bearer <JWT>` or `X-JWT-Token` header)
2. **BFF Gateway** → JWT Validation (using RSA public key from Identity Service)
3. **Header Sanitization** → BFF Gateway removes sensitive headers
4. **User Context** → BFF Gateway adds user/service headers (`X-User-Id`, `X-Service-Name`, etc.)
5. **Response** → BFF Gateway → Downstream Service (with sanitized headers)

#### API Key Authentication (Legacy Support)
1. **Client Request** → BFF Gateway (with `X-API-Key` header)
2. **BFF Gateway** → Identity Service (gRPC `ValidateApiKey` call on port 5008)
3. **Identity Service** → Redis (API key lookup and validation)
4. **Header Sanitization** → BFF Gateway removes sensitive headers (`X-API-Key`, etc.)
5. **User Context** → BFF Gateway adds user headers (`X-User-Id`, `X-User-Name`, `X-User-Permissions`)
6. **Response** → BFF Gateway → Downstream Service (with sanitized headers)

## 🚀 Services

### Core Services
- **🚪 BFF Gateway** (Port 5000) - API Gateway with YARP reverse proxy, gRPC authentication, and header sanitization
- **🌤️ Weather Service** (Port 5001) - Weather forecast and meteorological data with Scalar documentation
- **🔐 Identity Service** (HTTP: 5007, gRPC: 5008) - API key validation, user management, and authentication
- **🗄️ Redis** (Port 6379) - Distributed caching, API key storage, and session management

## 🔑 Authentication Methods

The system supports **two authentication methods** with **gRPC** communication between BFF Gateway and Identity Service:

### JWT Token Authentication (Recommended)
- **RSA-based JWT tokens** with public/private key cryptography
- **Service tokens** for inter-service communication (1-hour expiration)
- **User tokens** for client authentication (8-hour expiration)
- **Stateless validation** using RSA public key verification
- **Claims-based authorization** with permissions and user context

### API Key Authentication (Legacy Support)

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

## 🛠️ Development Setup

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

#### JWT Authentication Testing (Recommended)
```bash
# 1. Generate a test JWT token
curl -X POST http://localhost:5007/jwt/test-token \
  -H "Content-Type: application/json" \
  -d '{"userName": "john.doe", "tokenType": "user", "permissions": ["read", "write"]}'

# 2. Use the JWT token to access services (copy token from step 1)
curl -i http://localhost:5000/api/weather/weatherforecast \
  -H "Authorization: Bearer <JWT_TOKEN_FROM_STEP_1>"

# 3. Test service-to-service JWT token
curl -X POST http://localhost:5007/jwt/test-token \
  -H "Content-Type: application/json" \
  -d '{"userName": "WeatherService", "tokenType": "service", "permissions": ["read", "write"]}'

# 4. Get public key for verification
curl -i http://localhost:5007/jwt/public-key
```

#### API Key Authentication Testing (Legacy Support)
```bash
# Test without authentication (should return 401)
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
- **JWT-based authentication** with RSA public/private key cryptography
- **gRPC communication** between BFF Gateway and Identity Service
- **Hybrid authentication** supporting both JWT tokens and API keys
- **Stateless token validation** using RSA public key verification
- **Automatic key rotation** support for enhanced security
- **Claims-based authorization** with fine-grained permissions
- **Token expiration** with configurable lifetimes
- **Secure key storage** with JSON-based RSA key persistence
- Authentication tokens removed from headers before reaching downstream services
- User/service context headers added for downstream communication

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
