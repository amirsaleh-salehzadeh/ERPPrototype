# 🚀 ERP Prototype - Complete Developer Instructions

## 📋 **Complete ERP Microservices Architecture**

You now have a **full ERP prototype** with 6 microservices:

| Service | Port | Purpose | Endpoints |
|---------|------|---------|-----------|
| **🌤️ WeatherService** | 5001 | Sample service for testing | `/weatherforecast`, `/health` |
| **📚 Documentation** | 5002 | Scalar API documentation | `/scalar/v1`, `/health`, `/api/specs` |
| **📦 OrderService** | 5003 | Order management & fulfillment | `/orders`, `/orders/{id}`, `/orders/stats` |
| **📊 InventoryService** | 5004 | Product inventory & stock management | `/products`, `/products/low-stock`, `/inventory/stats` |
| **👤 CustomerService** | 5005 | Customer data & relationships | `/customers`, `/customers/search`, `/customers/stats` |
| **💰 FinanceService** | 5006 | Accounting, invoicing & financial reports | `/invoices`, `/transactions`, `/finance/reports/summary` |
| **🚪 BFF Gateway** | 5000 | API Gateway with YARP | Routes all `/api/*` requests |

---

## 🚀 **Quick Start - Run All Services**

### **Prerequisites:**
- **.NET 8 SDK** installed
- **Git** for cloning
- **curl** or **Postman** for testing

### **1. Setup & Build:**
```bash
# Clone and build
git clone <your-repository-url>
cd ERPPrototype
dotnet build
```

### **2. Start All Services (7 terminals):**

**Terminal 1 - WeatherService:**
```bash
dotnet run --project src\Services\Playground.WeatherService\Playground.WeatherService.csproj
```

**Terminal 2 - Documentation Service:**
```bash
dotnet run --project src\Documentation\Scalar.Documentation\Scalar.Documentation.csproj
```

**Terminal 3 - Order Service:**
```bash
dotnet run --project src\Services\ERP.OrderService\ERP.OrderService.csproj
```

**Terminal 4 - Inventory Service:**
```bash
dotnet run --project src\Services\ERP.InventoryService\ERP.InventoryService.csproj
```

**Terminal 5 - Customer Service:**
```bash
dotnet run --project src\Services\ERP.CustomerService\ERP.CustomerService.csproj
```

**Terminal 6 - Finance Service:**
```bash
dotnet run --project src\Services\ERP.FinanceService\ERP.FinanceService.csproj
```

**Terminal 7 - BFF Gateway:**
```bash
dotnet run --project src\Gateway\BFF.Gateway\BFF.Gateway.csproj
```

### **3. Test All Services Through Gateway:**

**Orders:**
```bash
curl http://localhost:5000/api/orders/orders
curl http://localhost:5000/api/orders/orders/1
curl http://localhost:5000/api/orders/orders/stats
```

**Inventory:**
```bash
curl http://localhost:5000/api/inventory/products
curl http://localhost:5000/api/inventory/products/low-stock
curl http://localhost:5000/api/inventory/inventory/stats
```

**Customers:**
```bash
curl http://localhost:5000/api/customers/customers
curl http://localhost:5000/api/customers/customers/101
curl http://localhost:5000/api/customers/customers/stats
```

**Finance:**
```bash
curl http://localhost:5000/api/finance/invoices
curl http://localhost:5000/api/finance/transactions
curl http://localhost:5000/api/finance/finance/reports/summary
```

**Weather (original test service):**
```bash
curl http://localhost:5000/api/weather/forecast
```

**Gateway Service Discovery:**
```bash
curl http://localhost:5000/api/gateway/services
```

### **4. Access Documentation:**
- **Scalar API Documentation**: http://localhost:5002/scalar/v1
- **Service Mappings**: http://localhost:5000/api/gateway/services

---

## 🎯 **What You Should See:**

### **Service Mapping Response:**
```json
{
  "services": [
    {
      "pathPrefix": "/api/weather",
      "serviceName": "WeatherService", 
      "displayName": "Weather Forecast Service"
    },
    {
      "pathPrefix": "/api/orders",
      "serviceName": "OrderService",
      "displayName": "Order Management Service"
    },
    {
      "pathPrefix": "/api/inventory", 
      "serviceName": "InventoryService",
      "displayName": "Inventory Management Service"
    },
    {
      "pathPrefix": "/api/customers",
      "serviceName": "CustomerService", 
      "displayName": "Customer Management Service"
    },
    {
      "pathPrefix": "/api/finance",
      "serviceName": "FinanceService",
      "displayName": "Financial Management Service"
    },
    {
      "pathPrefix": "/api/docs",
      "serviceName": "DocumentationService",
      "displayName": "API Documentation Service"
    }
  ]
}
```

### **Enhanced Gateway Logs:**
```
✅ Loaded 6 service mappings from configuration
🚀 Request routed to service: OrderService (Order Management Service) - Path: /api/orders/orders
📦 OrderService received request from gateway - Service: OrderService
📋 Fetching all orders
🧹 Service headers removed after gateway processing
```

---

## 🏗️ **Architecture Benefits:**

✅ **Kubernetes-Ready**: JSON-based service mapping supports independent scaling  
✅ **No Hardcoded Logic**: All service routing configured in `servicemapping.json`  
✅ **Extensible**: Easy to add new ERP modules  
✅ **Service Discovery**: `/api/gateway/services` endpoint for orchestration  
✅ **Modern Documentation**: Scalar provides superior API documentation  
✅ **Production-Ready**: Proper logging, health checks, and monitoring  

---

## 🐳 **Docker Alternative:**
```bash
docker-compose up --build
# Access: Gateway (7000), WeatherService (7001), Documentation (7002), 
#         Orders (7003), Inventory (7004), Customers (7005), Finance (7006)
```

This complete ERP prototype demonstrates enterprise-grade microservices architecture with proper separation of concerns, scalable infrastructure, and modern development practices! 🎉
