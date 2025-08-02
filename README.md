# ERP Prototype - Microservices Architecture

This project demonstrates a microservices architecture using .NET 8 with YARP (Yet Another Reverse Proxy) as a Backend for Frontend (BFF) gateway.

## Architecture Overview

```
┌─────────────────┐    ┌──────────────────┐    ┌─────────────────────┐
│   Client        │───▶│   BFF Gateway    │───▶│  WeatherService     │
│                 │    │   (YARP Proxy)   │    │  (Port 5001)        │
└─────────────────┘    │   (Port 5000)    │    └─────────────────────┘
                       │                  │    
                       │                  │    ┌─────────────────────┐
                       │                  │───▶│  Documentation      │
                       │                  │    │  (Scalar - Port     │
                       └──────────────────┘    │   5002)             │
                                               └─────────────────────┘
```

## Services

### 1. BFF Gateway (Port 5000)
- **Technology**: ASP.NET Core 8 with YARP
- **Purpose**: API Gateway with service name logging
- **Features**:
  - Routes requests to appropriate microservices
  - Logs service names for each request
  - Removes service identification headers after processing
  - Service discovery and load balancing

### 2. WeatherService (Port 5001)
- **Technology**: ASP.NET Core 8 Minimal APIs
- **Purpose**: Sample microservice providing weather forecast data
- **Endpoints**:
  - `GET /weatherforecast` - Returns weather forecast data
  - `GET /health` - Health check endpoint

### 3. Documentation Service (Port 5002)
- **Technology**: ASP.NET Core 8 with Scalar
- **Purpose**: API documentation using Scalar instead of Swagger
- **Features**:
  - Modern API documentation interface
  - Aggregates OpenAPI specs from other services
  - Purple theme with sidebar navigation

## Getting Started

### Prerequisites
- .NET 8 SDK
- Docker (optional)

### Running Locally

1. **Clone and build the solution**:
   ```bash
   git clone <repository-url>
   cd ERPPrototype
   dotnet build
   ```

2. **Start the services** (in separate terminals):
   ```bash
   # Terminal 1 - WeatherService
   dotnet run --project src\Services\Playground.WeatherService\Playground.WeatherService.csproj
   
   # Terminal 2 - Documentation Service
   dotnet run --project src\Documentation\Scalar.Documentation\Scalar.Documentation.csproj
   
   # Terminal 3 - BFF Gateway
   dotnet run --project src\Gateway\BFF.Gateway\BFF.Gateway.csproj
   ```

3. **Test the integration**:
   ```bash
   # Test weather service through gateway
   curl http://localhost:5000/api/weather/forecast
   
   # Test direct access to services
   curl http://localhost:5001/weatherforecast
   curl http://localhost:5002/health
   ```

4. **Access documentation**:
   - Scalar API Documentation: http://localhost:5002/scalar/v1

### Running with Docker

```bash
# Build and run all services
docker-compose up --build

# Access services
# Gateway: http://localhost:7000
# WeatherService: http://localhost:7001  
# Documentation: http://localhost:7002
```

## Service Logging

The BFF Gateway implements service name logging that:
1. **Identifies the target service** based on the request path
2. **Logs the service name** with a 🚀 emoji for visibility
3. **Adds service headers** for internal tracking
4. **Removes service headers** after processing with a 🧹 emoji

Example logs:
```
🚀 Request routed to service: WeatherService - Path: /api/weather/forecast
🎯 WeatherService received request from gateway - Service: WeatherService
🌤️ Generating weather forecast data
🌤️ Weather forecast generated with 5 entries
🧹 Service name header removed after gateway processing
```

## API Routes

### Through Gateway (Port 5000)
- `GET /api/weather/forecast` → Routes to WeatherService `/weatherforecast`
- `GET /api/docs/*` → Routes to Documentation Service

### Direct Access
- **WeatherService (Port 5001)**:
  - `GET /weatherforecast`
  - `GET /health`
  
- **Documentation Service (Port 5002)**:
  - `GET /scalar/v1` (API Documentation)
  - `GET /health`
  - `GET /api/specs` (Aggregated OpenAPI specs)

## Future Enhancements

This infrastructure is designed to support a full ERP system with:
- Authentication and authorization
- Database integration
- Message queuing
- Service mesh
- Monitoring and observability
- CI/CD pipelines

## Technology Stack

- **.NET 8**: Latest LTS version
- **YARP**: Microsoft's reverse proxy
- **Scalar**: Modern API documentation
- **Docker**: Containerization
- **Minimal APIs**: Lightweight API development
