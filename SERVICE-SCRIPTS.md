# ERP Prototype Service Management Scripts

This directory contains PowerShell scripts to manage all ERP Prototype services running on .NET 10.

## 🚀 Quick Start

### Start All Services (Simple)
```powershell
./start-services.ps1
```

### Start All Services (Advanced)
```powershell
./run-all-services.ps1
```

### Stop All Services
```powershell
./stop-services.ps1
```

## 📋 Available Scripts

### `start-services.ps1` (Recommended for quick development)
- **Purpose**: Quick and simple service startup
- **Features**:
  - Builds the solution
  - Starts all 4 services in separate windows
  - Opens the main gateway in browser
  - Minimal output for fast startup

### `run-all-services.ps1` (Full-featured)
- **Purpose**: Production-ready service management
- **Features**:
  - Port conflict detection and resolution
  - Health checks for all services
  - Comprehensive logging and status reporting
  - Multiple execution modes
  - Service dependency management

**Parameters:**
- `-Quiet`: Minimal output
- `-NoBrowser`: Don't open browser automatically
- `-Stop`: Stop all running services

**Examples:**
```powershell
# Start with minimal output
./run-all-services.ps1 -Quiet

# Start without opening browser
./run-all-services.ps1 -NoBrowser

# Stop all services
./run-all-services.ps1 -Stop
```

### `stop-services.ps1`
- **Purpose**: Stop all running services
- **Features**:
  - Finds processes using ERP service ports (5000, 5001, 5002, 5007)
  - Gracefully terminates all service processes
  - Reports stopped processes

## 🌐 Service Endpoints

When all services are running:

| Service | Port | Swagger/API | Description |
|---------|------|-------------|-------------|
| **BFF Gateway** | 5000 | http://localhost:5000/swagger | Main API Gateway |
| **Weather Service** | 5001 | http://localhost:5001/swagger | Weather data API |
| **Documentation** | 5002 | http://localhost:5002 | API Documentation |
| **Identity Service** | 5007 | http://localhost:5007/swagger | Authentication & Authorization |

## 🔧 Troubleshooting

### Port Conflicts
If you get port conflict errors:
1. Run `./stop-services.ps1` to stop existing services
2. Or use `./run-all-services.ps1 -Stop` then restart

### Build Errors
If services fail to start due to build errors:
1. Check the individual service windows for error details
2. Run `dotnet build` manually to see build issues
3. Ensure you have .NET 10 SDK installed

### Service Not Responding
If a service shows as unhealthy:
1. Check the individual PowerShell window for that service
2. Look for startup errors or exceptions
3. Verify the service's appsettings.json configuration

## 💡 Tips

- Each service runs in its own PowerShell window for easy monitoring
- Close individual service windows to stop specific services
- Use `Ctrl+C` in service windows for graceful shutdown
- The main gateway (port 5000) is the primary entry point
- All services support hot reload during development

## 🏗️ Architecture

The ERP Prototype uses a microservices architecture with:
- **BFF Gateway**: Routes requests and provides unified API
- **Identity Service**: Handles authentication with Redis storage
- **Weather Service**: Sample business service
- **Documentation**: Scalar-based API documentation hub

All services are built on **.NET 10 Preview** and communicate via HTTP/REST and gRPC.
