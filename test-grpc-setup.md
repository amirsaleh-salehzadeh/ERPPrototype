# 🚀 **gRPC Identity Service & API Key Validation Setup**

## 🎯 **What We've Built:**

### **1. Identity Service (Port 5007)**
- **gRPC Server** for API key validation
- **REST API** for API key management
- **In-memory storage** with sample API keys
- **Comprehensive logging** and error handling

### **2. API Key Validation Pipeline in BFF Gateway**
- **Middleware** that intercepts all requests
- **gRPC client** to communicate with Identity service
- **Header injection** for user context
- **Path-based service routing**

### **3. Shared gRPC Contracts**
- **Proto definitions** for Identity, Orders, and Inventory services
- **Generated C# classes** for client/server communication
- **Type-safe** inter-service communication

## 🔧 **Architecture Overview:**

```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   Client App    │───▶│  BFF Gateway    │───▶│  Microservices  │
│                 │    │   (Port 5000)   │    │  (Various Ports)│
└─────────────────┘    └─────────────────┘    └─────────────────┘
                              │
                              ▼ gRPC
                       ┌─────────────────┐
                       │ Identity Service│
                       │   (Port 5007)   │
                       └─────────────────┘
```

## 🔑 **API Key Validation Flow:**

1. **Client** sends request with `X-API-Key` header
2. **BFF Gateway** intercepts request in middleware
3. **Middleware** calls Identity service via gRPC
4. **Identity Service** validates API key and returns user info
5. **Gateway** adds user context headers and forwards to service
6. **Microservice** receives request with user context

## 🧪 **Testing the Setup:**

### **Step 1: Start Identity Service**
```bash
cd src/Services/ERP.IdentityService
dotnet run
```

### **Step 2: Get Sample API Keys**
The Identity service creates sample API keys on startup:
- **Admin User**: Full permissions
- **Developer User**: Read/Write permissions  
- **ReadOnly User**: Read-only permissions

### **Step 3: Test API Key Creation**
```bash
curl -X POST http://localhost:5007/api-keys \
  -H "Content-Type: application/json" \
  -d '{
    "userName": "testuser",
    "description": "Test API Key",
    "permissions": ["read", "write"],
    "expiresInDays": 30
  }'
```

### **Step 4: Test API Key Validation**
```bash
curl -X POST http://localhost:5007/validate \
  -H "Content-Type: application/json" \
  -d '{
    "apiKey": "YOUR_API_KEY_HERE",
    "serviceName": "OrderService",
    "endpoint": "/api/orders"
  }'
```

### **Step 5: Test Through Gateway (with API Key)**
```bash
curl -H "X-API-Key: YOUR_API_KEY_HERE" \
  http://localhost:5000/api/orders/hello
```

## 🔍 **Key Features Implemented:**

### **✅ Identity Service Features:**
- gRPC service for validation
- REST API for management
- Sample API keys pre-created
- Usage tracking and expiration
- Comprehensive error handling

### **✅ BFF Gateway Features:**
- API key validation middleware
- gRPC client integration
- User context injection
- Path-based service detection
- Skip validation for health/docs endpoints

### **✅ Inter-Service Communication:**
- Type-safe gRPC contracts
- Shared proto definitions
- Client/Server code generation
- Error handling and logging

## 🚀 **Next Steps:**

1. **Add gRPC to other services** for inter-service communication
2. **Implement service-to-service calls** (e.g., Orders calling Inventory)
3. **Add authentication tokens** instead of just API keys
4. **Implement role-based permissions** for fine-grained access control
5. **Add distributed tracing** for request correlation

## 🎉 **Benefits Achieved:**

- **Centralized Authentication**: Single point for API key management
- **Secure Communication**: gRPC with type safety
- **Scalable Architecture**: Microservices can authenticate independently
- **Developer Experience**: Clear APIs and comprehensive logging
- **Production Ready**: Error handling, validation, and monitoring

The foundation is now in place for a complete microservices authentication and authorization system! 🔐
