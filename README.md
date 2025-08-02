# ERP Prototype - Microservices Architecture with API Key Authentication

This project demonstrates a complete microservices architecture using .NET 8 with YARP (Yet Another Reverse Proxy) as a Backend for Frontend (BFF) gateway, featuring centralized API key authentication and authorization through a dedicated Identity service.

## Architecture Overview

```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   Client App    │───▶│  BFF Gateway    │───▶│  Microservices  │
│                 │    │   (Port 5000)   │    │  (Various Ports)│
│  X-API-Key      │    │  ✅ Middleware   │    │  + User Headers │
└─────────────────┘    └─────────────────┘    └─────────────────┘
                              │ REST API
                              ▼
                       ┌─────────────────┐    ┌─────────────────┐
                       │ Identity Service│───▶│ Redis / Memory  │
                       │   (Port 5007)   │    │  ✅ API Keys    │
                       │  ✅ Validation   │    │  ✅ 20+ Keys    │
                       └─────────────────┘    └─────────────────┘
```

## Services

### 1. BFF Gateway (Port 5000)
- **Technology**: ASP.NET Core 8 with YARP
- **Purpose**: API Gateway with API key validation pipeline
- **Features**:
  - **API Key Validation Middleware**: Validates all requests before routing
  - Routes requests to appropriate microservices
  - Communicates with Identity service for authentication
  - Injects user context headers for downstream services
  - Service discovery and load balancing
  - Bypasses validation for health/docs endpoints

### 2. Identity Service (Port 5007) 🔐
- **Technology**: ASP.NET Core 8 with Redis/In-Memory storage
- **Purpose**: Centralized API key management and validation
- **Features**:
  - **API Key Generation**: Creates secure, random API keys
  - **Redis Integration**: Production-ready storage with in-memory fallback
  - **User Management**: Associates keys with users and permissions
  - **Usage Tracking**: Monitors API key usage and expiration
  - **Automatic Seeding**: Creates test API keys on startup
- **Endpoints**:
  - `POST /api-keys` - Create new API key
  - `POST /validate` - Validate API key
  - `GET /api-keys/{key}/info` - Get API key information
  - `POST /seed/random/{count}` - Generate random test API keys
  - `POST /seed/predefined` - Create predefined test API keys

### 3. WeatherService (Port 5001)
- **Technology**: ASP.NET Core 8 Minimal APIs
- **Purpose**: Sample microservice providing weather forecast data
- **Endpoints**:
  - `GET /hello` - Hello world endpoint
  - `GET /weatherforecast` - Returns weather forecast data
  - `GET /health` - Health check endpoint

### 4. Order Service (Port 5003)
- **Technology**: ASP.NET Core 8 Minimal APIs
- **Purpose**: Order management microservice
- **Endpoints**:
  - `GET /hello` - Hello world endpoint
  - `GET /orders` - Get all orders
  - `GET /orders/{id}` - Get specific order
  - `GET /orders/stats` - Order statistics

### 5. Inventory Service (Port 5004)
- **Technology**: ASP.NET Core 8 Minimal APIs
- **Purpose**: Inventory and product management
- **Endpoints**:
  - `GET /hello` - Hello world endpoint
  - `GET /products` - Get all products
  - `GET /products/low-stock` - Get low stock products
  - `GET /inventory/stats` - Inventory statistics

### 6. Customer Service (Port 5005)
- **Technology**: ASP.NET Core 8 Minimal APIs
- **Purpose**: Customer relationship management
- **Endpoints**:
  - `GET /hello` - Hello world endpoint
  - `GET /customers` - Get all customers
  - `GET /customers/{id}` - Get specific customer
  - `GET /customers/stats` - Customer statistics

### 7. Finance Service (Port 5006)
- **Technology**: ASP.NET Core 8 Minimal APIs
- **Purpose**: Financial management and reporting
- **Endpoints**:
  - `GET /hello` - Hello world endpoint
  - `GET /invoices` - Get all invoices
  - `GET /transactions` - Get all transactions
  - `GET /finance/reports/summary` - Financial summary

### 8. Documentation Service (Port 5002)
- **Technology**: ASP.NET Core 8 with Scalar
- **Purpose**: API documentation using Scalar instead of Swagger
- **Features**:
  - Modern API documentation interface
  - Aggregates OpenAPI specs from all services
  - Purple theme with sidebar navigation
  - Shows all microservice endpoints in one interface

## 🔑 API Key Authentication

This system implements **enterprise-grade centralized API key authentication** where ALL requests to microservices (including documentation) must include a valid API key in the `X-API-Key` header. The BFF Gateway validates these keys with the Identity service before routing requests.

### How It Works:
1. **Client** sends request with `X-API-Key` header to gateway
2. **BFF Gateway** intercepts ALL requests in authentication middleware
3. **Identity Service** validates the API key via REST API call
4. **Gateway** adds user context headers (user, permissions) and forwards to microservice
5. **Microservice** receives authenticated request with user information
6. **Audit Trail** logs all authentication attempts and usage patterns

### 🛡️ Security Features:
- **Complete Route Protection**: ALL routes except health/services require authentication
- **Centralized Validation**: Single source of truth for API key management
- **User Context Injection**: Services know who is making requests
- **Usage Tracking**: Monitor API key usage patterns and statistics
- **Expiration Support**: API keys have configurable expiration dates
- **Permission System**: Role-based access control ready for implementation

### Getting API Keys for Testing

The Identity service automatically creates test API keys on startup. Here are the **predefined API keys** you can use immediately:

#### 🔑 **Predefined API Keys (Ready to Use)**

**⚡ Quick Start**: Copy any API key below and start testing immediately!

| User Type | API Key | Permissions | Use Case |
|-----------|---------|-------------|----------|
| **🔐 Admin Master** | `0MyvBtNvMQMrfJHZjORFVxjHcUUYEpv5HrOhJBRrhOY` | read, write, delete, admin | Full system access, all operations |
| **👨‍💻 Dev Team Lead** | `38c_y0McElpnr4iLNVLsR0VjGQuzRlGP-zeCmVIhI6M` | read, write, deploy | Development and deployment |
| **🧪 QA Automation** | `91sd4TPkE2fNyxh7xhSBIJt11JciT8bWHQ9aTGQhiAo` | read, write, test | Testing and quality assurance |
| **📊 Monitoring Service** | `8Swc7979DTVqEYebKAdpf3xmiUpE9mcOGsy1emvaoNk` | read, health | System monitoring and health checks |
| **📈 Analytics Dashboard** | `h02zaXOJKTcdmuytRruPhEf8JutxDuhCpmKkVWgheuA` | read, analytics | Analytics and reporting dashboards |

**💡 Pro Tip**: The system automatically creates 15+ additional random API keys on startup for extended testing!

#### 🎲 **Generate More API Keys**

Create additional random API keys for testing:

```bash
# Generate 10 random API keys
curl -X POST http://localhost:5007/seed/random/10

# Generate 5 random API keys
curl -X POST http://localhost:5007/seed/random/5

# Create predefined API keys again
curl -X POST http://localhost:5007/seed/predefined
```

#### 🔍 **Get Current API Keys**

Check the Identity service logs to see all generated API keys, or create a new one:

```bash
# Create a custom API key
curl -X POST http://localhost:5007/api-keys \
  -H "Content-Type: application/json" \
  -d '{
    "userName": "your_username",
    "description": "My Test API Key",
    "permissions": ["read", "write"],
    "expiresInDays": 30
  }'
```

## Getting Started

### Prerequisites
- .NET 8 SDK
- Redis (optional - will use in-memory storage if not available)
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
   # Terminal 1 - Identity Service (REQUIRED - Start this first!)
   dotnet run --project src\Services\ERP.IdentityService\ERP.IdentityService.csproj

   # Terminal 2 - WeatherService
   dotnet run --project src\Services\ERP.WeatherService\ERP.WeatherService.csproj

   # Terminal 3 - Order Service
   dotnet run --project src\Services\ERP.OrderService\ERP.OrderService.csproj

   # Terminal 4 - Inventory Service
   dotnet run --project src\Services\ERP.InventoryService\ERP.InventoryService.csproj

   # Terminal 5 - Customer Service
   dotnet run --project src\Services\ERP.CustomerService\ERP.CustomerService.csproj

   # Terminal 6 - Finance Service
   dotnet run --project src\Services\ERP.FinanceService\ERP.FinanceService.csproj

   # Terminal 7 - Documentation Service
   dotnet run --project src\Documentation\Scalar.Documentation\Scalar.Documentation.csproj

   # Terminal 8 - BFF Gateway (Start this last!)
   dotnet run --project src\Gateway\BFF.Gateway\BFF.Gateway.csproj
   ```

   **⚠️ Important**:
   - Start the **Identity Service FIRST** as it creates the API keys needed for authentication
   - Start the **BFF Gateway LAST** as it needs to connect to all other services
   - All services will show comprehensive logs with emojis for easy monitoring

3. **Test the API Key Authentication Pipeline**:

   #### 🚫 **Test Without API Key (Should Fail)**:
   ```bash
   # This will return "API key is required"
   curl http://localhost:5000/api/weather/hello
   curl http://localhost:5000/api/orders/hello
   ```

   #### ✅ **Test With Valid API Keys (Should Succeed)**:
   ```bash
   # Using Admin Master API Key
   curl -H "X-API-Key: 0MyvBtNvMQMrfJHZjORFVxjHcUUYEpv5HrOhJBRrhOY" \
        http://localhost:5000/api/weather/hello

   # Using Dev Team Lead API Key
   curl -H "X-API-Key: 38c_y0McElpnr4iLNVLsR0VjGQuzRlGP-zeCmVIhI6M" \
        http://localhost:5000/api/orders/hello

   # Using QA Automation API Key
   curl -H "X-API-Key: 91sd4TPkE2fNyxh7xhSBIJt11JciT8bWHQ9aTGQhiAo" \
        http://localhost:5000/api/inventory/hello

   # Using Monitoring Service API Key
   curl -H "X-API-Key: 8Swc7979DTVqEYebKAdpf3xmiUpE9mcOGsy1emvaoNk" \
        http://localhost:5000/api/customers/hello

   # Using Analytics Dashboard API Key
   curl -H "X-API-Key: h02zaXOJKTcdmuytRruPhEf8JutxDuhCpmKkVWgheuA" \
        http://localhost:5000/api/finance/hello
   ```

   #### 🔍 **Test All Services Through Gateway**:
   ```bash
   # Replace YOUR_API_KEY with any of the predefined keys above
   export API_KEY="0MyvBtNvMQMrfJHZjORFVxjHcUUYEpv5HrOhJBRrhOY"

   # Test all hello endpoints
   curl -H "X-API-Key: $API_KEY" http://localhost:5000/api/weather/hello
   curl -H "X-API-Key: $API_KEY" http://localhost:5000/api/orders/hello
   curl -H "X-API-Key: $API_KEY" http://localhost:5000/api/inventory/hello
   curl -H "X-API-Key: $API_KEY" http://localhost:5000/api/customers/hello
   curl -H "X-API-Key: $API_KEY" http://localhost:5000/api/finance/hello
   curl -H "X-API-Key: $API_KEY" http://localhost:5000/api/docs/hello

   # Test business endpoints
   curl -H "X-API-Key: $API_KEY" http://localhost:5000/api/weather/weatherforecast
   curl -H "X-API-Key: $API_KEY" http://localhost:5000/api/orders/orders
   curl -H "X-API-Key: $API_KEY" http://localhost:5000/api/inventory/products
   curl -H "X-API-Key: $API_KEY" http://localhost:5000/api/customers/customers
   curl -H "X-API-Key: $API_KEY" http://localhost:5000/api/finance/invoices
   ```

   #### 🔓 **Test Public Endpoints (No API Key Required)**:
   ```bash
   # Only these endpoints bypass API key validation
   curl http://localhost:5000/api/gateway/services  # Service discovery
   curl http://localhost:5000/health                # Gateway health check

   # Everything else requires authentication:
   curl http://localhost:5000/api/docs/scalar/all   # Returns: "API key is required"
   curl http://localhost:5000/api/orders/hello      # Returns: "API key is required"
   ```

   #### ❌ **Test Invalid API Key (Should Fail)**:
   ```bash
   # This will return "Invalid API key"
   curl -H "X-API-Key: invalid-key-123" http://localhost:5000/api/orders/hello
   ```

   #### 🤖 **Automated Testing Scripts**:

   For convenience, use the provided testing scripts:

   **Windows (PowerShell)**:
   ```powershell
   .\test-api-keys.ps1
   ```

   **Linux/Mac (Bash)**:
   ```bash
   ./test-api-keys.sh
   ```

   These scripts will automatically test all API keys against all services and show you the results.

   #### 📚 **Testing with Scalar Documentation**:

   **⚠️ Important**: Scalar documentation is now protected by API key authentication when accessed through the gateway.

   **Option 1 - Use Helper HTML Page (Easiest)**:
   ```bash
   # Open the helper page in your browser
   start scalar-with-api-key.html  # Windows
   open scalar-with-api-key.html   # Mac
   ```

   **Option 2 - Access Scalar Directly (No Auth Required)**:
   - Go to: http://localhost:5002/scalar/all
   - Click "Auth" button in Scalar
   - Select "ApiKey" authentication
   - Enter API key: `0MyvBtNvMQMrfJHZjORFVxjHcUUYEpv5HrOhJBRrhOY`
   - Test endpoints (they will go through the gateway with authentication)

   **Option 3 - Access Through Gateway (Requires API Key)**:
   - First authenticate: `curl -H "X-API-Key: YOUR_KEY" http://localhost:5000/api/docs/scalar/all`
   - Then use browser with the same API key header (requires browser extension)

4. **Access documentation and test with API keys**:

   #### 🎯 **Recommended: Use the Helper HTML Page**
   ```bash
   # Open the helper page in your browser
   start scalar-with-api-key.html  # Windows
   open scalar-with-api-key.html   # Mac/Linux
   ```

   **What it does**:
   - � Shows all predefined API keys with descriptions
   - 📚 Loads Scalar documentation with one click
   - 🧪 Makes it easy to test authenticated endpoints
   - 🎨 Beautiful interface with color-coded API keys

   #### �🔐 **Protected Documentation (Requires API Key)**:
   - **Scalar via Gateway**: http://localhost:5000/api/docs/scalar/all
     - **⚠️ Authentication Required**: Must include `X-API-Key` header
     - **Use Case**: Production-like access through the gateway

   #### 🔓 **Direct Documentation (No Gateway Auth)**:
   - **Scalar (All Services)**: http://localhost:5002/scalar/all
     - **✅ Best for Testing**: Access Scalar directly, then set API key in the interface
     - **How to Use**: Click "Auth" → Select "ApiKey" → Enter your API key
   - **Gateway Service Mappings**: http://localhost:5000/api/gateway/services
   - **Identity Service**: http://localhost:5007/swagger
   - **Individual Services**:
     - Weather: http://localhost:5001/swagger
     - Orders: http://localhost:5003/swagger
     - Inventory: http://localhost:5004/swagger
     - Customers: http://localhost:5005/swagger
     - Finance: http://localhost:5006/swagger

   #### 🚀 **Quick Start Guide**:
   1. **Start all services** (Identity service first!)
   2. **Open `scalar-with-api-key.html`** in your browser
   3. **Click any API key** to select it (Admin Master recommended)
   4. **Click "Access Direct"** to load Scalar
   5. **In Scalar, click "Auth"** and enter the API key
   6. **Test any endpoint** - requests will be authenticated automatically!

## 🔧 API Key Management

### Creating New API Keys

```bash
# Create a custom API key
curl -X POST http://localhost:5007/api-keys \
  -H "Content-Type: application/json" \
  -d '{
    "userName": "john_doe",
    "description": "John Doe Development Key",
    "permissions": ["read", "write"],
    "expiresInDays": 90
  }'
```

### Validating API Keys

```bash
# Validate an API key directly
curl -X POST http://localhost:5007/validate \
  -H "Content-Type: application/json" \
  -d '{
    "apiKey": "YOUR_API_KEY_HERE",
    "serviceName": "OrderService",
    "endpoint": "/api/orders/hello"
  }'
```

### Getting API Key Information

```bash
# Get information about an API key
curl http://localhost:5007/api-keys/YOUR_API_KEY_HERE/info
```

### Generating Test API Keys

```bash
# Generate 20 random API keys for testing
curl -X POST http://localhost:5007/seed/random/20

# Create predefined API keys
curl -X POST http://localhost:5007/seed/predefined
```

### Using API Keys in Scalar Documentation

#### 🎯 **Method 1: Using the Helper HTML Page (Recommended)**
1. Open `scalar-with-api-key.html` in your browser
2. Click on any predefined API key to select it
3. Click "Access Direct (No Auth)" to load Scalar
4. In Scalar, click the "Auth" button and enter your API key
5. Test any endpoint - the API key will be automatically included

#### 🔧 **Method 2: Manual Setup in Scalar**
1. Go to http://localhost:5002/scalar/all (direct access)
2. Click the "Auth" button in the top-right corner
3. Select "ApiKey" authentication
4. Enter one of the predefined API keys in the "X-API-Key" field
5. Click "Set" to save the authentication
6. Test any endpoint - requests will include the API key header

#### 🧪 **Method 3: Testing Through Gateway with curl**
```bash
# Test Scalar access through gateway (requires API key)
curl -H "X-API-Key: 0MyvBtNvMQMrfJHZjORFVxjHcUUYEpv5HrOhJBRrhOY" \
     http://localhost:5000/api/docs/scalar/all

# Test individual endpoints through gateway
curl -H "X-API-Key: 0MyvBtNvMQMrfJHZjORFVxjHcUUYEpv5HrOhJBRrhOY" \
     http://localhost:5000/api/orders/orders
```

### Monitoring API Key Usage

Check the Identity service logs to see:
- API key validation attempts
- Usage statistics
- Failed authentication attempts
- User activity patterns

### Running with Docker

```bash
# Build and run all services
docker-compose up --build

# Access services
# Gateway: http://localhost:7000
# WeatherService: http://localhost:7001
# Documentation: http://localhost:7002
```

## Service Mapping & Logging

The BFF Gateway uses a **JSON-based service mapping configuration** that enables:
1. **Dynamic service discovery** - No hardcoded service logic
2. **Kubernetes-ready scaling** - Services can be scaled independently
3. **Centralized configuration** - All service mappings in one place
4. **Extensible architecture** - Easy to add new services

### Service Mapping Configuration (`servicemapping.json`)
```json
{
  "ServiceMappings": [
    {
      "PathPrefix": "/api/weather",
      "ServiceName": "WeatherService",
      "DisplayName": "Weather Forecast Service",
      "Description": "Provides weather forecast data and meteorological information"
    }
    // ... more services
  ]
}
```

### Service Logging Features:
1. **Identifies the target service** using path prefix matching
2. **Logs service details** with 🚀 emoji for visibility
3. **Adds service headers** for internal tracking
4. **Removes service headers** after processing with 🧹 emoji

Example logs:
```
✅ Loaded 6 service mappings from configuration
🚀 Request routed to service: WeatherService (Weather Forecast Service) - Path: /api/weather/forecast
🎯 WeatherService received request from gateway - Service: WeatherService
🌤️ Generating weather forecast data
🌤️ Weather forecast generated with 5 entries
🧹 Service headers removed after gateway processing
```

## 🌐 API Routes

### 🚪 Through Gateway (Port 5000) - **Requires API Key**

All routes through the gateway require the `X-API-Key` header except for public endpoints.

#### **Weather Service Routes**:
- `GET /api/weather/hello` → Hello world endpoint
- `GET /api/weather/weatherforecast` → Weather forecast data
- `GET /api/weather/health` → Health check

#### **Order Service Routes**:
- `GET /api/orders/hello` → Hello world endpoint
- `GET /api/orders/orders` → Get all orders
- `GET /api/orders/orders/{id}` → Get specific order
- `GET /api/orders/orders/stats` → Order statistics

#### **Inventory Service Routes**:
- `GET /api/inventory/hello` → Hello world endpoint
- `GET /api/inventory/products` → Get all products
- `GET /api/inventory/products/low-stock` → Low stock products
- `GET /api/inventory/inventory/stats` → Inventory statistics

#### **Customer Service Routes**:
- `GET /api/customers/hello` → Hello world endpoint
- `GET /api/customers/customers` → Get all customers
- `GET /api/customers/customers/{id}` → Get specific customer
- `GET /api/customers/customers/stats` → Customer statistics

#### **Finance Service Routes**:
- `GET /api/finance/hello` → Hello world endpoint
- `GET /api/finance/invoices` → Get all invoices
- `GET /api/finance/transactions` → Get all transactions
- `GET /api/finance/finance/reports/summary` → Financial summary

#### **Documentation Service Routes**:
- `GET /api/docs/hello` → Hello world endpoint
- `GET /api/docs/scalar/all` → Aggregated API documentation

#### **Identity Service Routes**:
- `GET /api/identity/hello` → Hello world endpoint
- `POST /api/identity/api-keys` → Create API key
- `POST /api/identity/validate` → Validate API key

#### **Public Routes (No API Key Required)**:
- `GET /api/gateway/services` → Service mappings
- `GET /health` → Gateway health check

### 🔗 Direct Access (No API Key Required)

#### **Identity Service (Port 5007)**:
- `GET /hello` → Hello world
- `POST /api-keys` → Create API key
- `POST /validate` → Validate API key
- `GET /api-keys/{key}/info` → Get API key info
- `POST /seed/random/{count}` → Generate random API keys
- `POST /seed/predefined` → Create predefined API keys
- `GET /swagger` → Swagger documentation

#### **Individual Services**:
- **WeatherService (Port 5001)**: `/hello`, `/weatherforecast`, `/health`, `/swagger`
- **OrderService (Port 5003)**: `/hello`, `/orders`, `/orders/{id}`, `/orders/stats`, `/swagger`
- **InventoryService (Port 5004)**: `/hello`, `/products`, `/products/low-stock`, `/swagger`
- **CustomerService (Port 5005)**: `/hello`, `/customers`, `/customers/{id}`, `/swagger`
- **FinanceService (Port 5006)**: `/hello`, `/invoices`, `/transactions`, `/swagger`
- **Documentation Service (Port 5002)**: `/scalar/all`, `/health`, `/swagger`

## 🚀 Current Features

✅ **Centralized API Key Authentication**: Complete validation pipeline
✅ **Microservices Architecture**: 7 services with proper separation
✅ **API Gateway**: YARP-based routing with middleware
✅ **Redis Integration**: Production-ready storage with fallback
✅ **Comprehensive Documentation**: Scalar with aggregated specs
✅ **Service Discovery**: JSON-based configuration for Kubernetes
✅ **User Context Injection**: Services receive user information
✅ **Usage Tracking**: Monitor API key usage and patterns
✅ **Automatic Testing**: Predefined and random API keys
✅ **Health Checks**: All services have health endpoints

## 🔮 Future Enhancements

This infrastructure is designed to support a full ERP system with:
- **gRPC Inter-Service Communication**: Type-safe service-to-service calls
- **JWT Token Authentication**: Replace API keys with JWT tokens
- **Role-Based Access Control**: Fine-grained permissions
- **Database Integration**: PostgreSQL/SQL Server with Entity Framework
- **Message Queuing**: RabbitMQ/Azure Service Bus for async communication
- **Service Mesh**: Istio for advanced traffic management
- **Monitoring and Observability**: OpenTelemetry, Prometheus, Grafana
- **CI/CD Pipelines**: GitHub Actions with automated deployment
- **Kubernetes Deployment**: Production-ready container orchestration

## 🛠️ Technology Stack

- **.NET 8**: Latest LTS version with Minimal APIs
- **YARP**: Microsoft's reverse proxy for API Gateway
- **Redis**: In-memory data store for API keys (with fallback)
- **StackExchange.Redis**: Redis client for .NET
- **Scalar**: Modern API documentation (preferred over Swagger)
- **Docker**: Containerization support
- **JSON Configuration**: Service mapping and configuration
- **Structured Logging**: Comprehensive request/response logging
