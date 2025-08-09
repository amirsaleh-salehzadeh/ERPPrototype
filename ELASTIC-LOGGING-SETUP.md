# BFF Gateway - Elastic Stack Logging Implementation

## 🎉 Implementation Summary

Successfully implemented comprehensive **ELK Stack logging** for the BFF Gateway with detailed request/response tracking and Kibana dashboard integration.

## ✅ What's Been Implemented

### 🔧 Core Features
- **✅ Serilog Integration** - Advanced structured logging with multiple sinks
- **✅ Elasticsearch Sink** - Direct log shipping to Elasticsearch with resilient configuration
- **✅ Request Logging Middleware** - Detailed HTTP request/response capture
- **✅ Correlation Tracking** - Unique request IDs for distributed tracing
- **✅ Performance Monitoring** - Response time tracking and categorization
- **✅ ELK Stack** - Complete Elasticsearch, Kibana, and Logstash setup

### 📊 Logging Capabilities
- **Request Details**: Method, Path, Headers, Query Parameters, Body (truncated for large payloads)
- **Response Details**: Status Code, Headers, Body, Content Type
- **Performance Metrics**: Response time in milliseconds with categorization (normal/slow/very_slow)
- **User Context**: IP Address, User Agent, Referer
- **Correlation Tracking**: Unique request IDs in `X-Correlation-ID` header
- **Structured Data**: JSON-formatted logs with rich metadata

### 🛠️ Files Created/Modified

#### New Files
```
├── docker-compose.elk.yml                    # ELK Stack services
├── elk-management.ps1                        # ELK management script
├── setup-elk-logging.ps1                     # Complete setup automation
├── setup-kibana-dashboards.ps1               # Kibana dashboard configuration
├── test-elk-setup.ps1                        # Testing and validation script
├── logstash/
│   ├── config/logstash.yml                   # Logstash configuration
│   └── pipeline/logstash.conf                # Log processing pipeline
└── src/Gateway/BFF.Gateway/Middleware/
    └── RequestLoggingMiddleware.cs            # HTTP request logging middleware
```

#### Modified Files
```
├── src/Gateway/BFF.Gateway/
│   ├── BFF.Gateway.csproj                    # Added Serilog + Elasticsearch packages
│   ├── Program.cs                            # Integrated Serilog and middleware
│   ├── appsettings.json                      # Elasticsearch configuration
│   └── appsettings.Development.json          # Development logging settings
└── README.md                                 # Updated with ELK documentation
```

## 🚀 Quick Start

### 1. Start ELK Stack
```powershell
# Option 1: Complete automated setup
.\setup-elk-logging.ps1

# Option 2: Manual setup
docker-compose -f docker-compose.elk.yml up -d
```

### 2. Start BFF Gateway
```powershell
dotnet run --project src/Gateway/BFF.Gateway
```

### 3. Generate Test Logs
```powershell
curl http://localhost:5000/health
curl http://localhost:5000/api/gateway/services
```

### 4. View Logs in Kibana
1. Open http://localhost:5601
2. Go to **"Discover"** tab
3. Select **"bff-gateway-logs-*"** index pattern
4. Explore logs with filters and time ranges

## 📊 Service URLs

| Service | URL | Purpose |
|---------|-----|---------|
| **BFF Gateway** | http://localhost:5000 | API Gateway with logging |
| **Kibana** | http://localhost:5601 | Log visualization dashboard |
| **Elasticsearch** | http://localhost:9200 | Log storage and search |
| **Logstash** | http://localhost:5044 | Log processing pipeline |

## 🔍 Kibana Usage Examples

### Index Pattern
- **Pattern**: `bff-gateway-logs-*`
- **Time Field**: `@timestamp`

### Useful Queries
```
# All BFF Gateway requests
service_name:"bff-gateway"

# Slow requests (over 1 second)
response.ElapsedMs:>1000

# Error responses (4xx/5xx)
response.StatusCode:>=400

# Specific endpoint requests
request.Path:"/api/weather"

# Requests by correlation ID
correlation_id:"YOUR-CORRELATION-ID"

# Performance analysis
performance_category:"slow" OR performance_category:"very_slow"
```

## 🛠️ Management Commands

### ELK Stack Management
```powershell
# Start/stop services
.\elk-management.ps1 start
.\elk-management.ps1 stop

# Check status
.\elk-management.ps1 status

# Open Kibana
.\elk-management.ps1 kibana

# Test connectivity
.\elk-management.ps1 test
```

### Testing & Validation
```powershell
# Test ELK setup
.\test-elk-setup.ps1

# Setup Kibana dashboards
.\setup-kibana-dashboards.ps1
```

## 📈 Log Structure Example

### Request Log
```json
{
  "RequestId": "f47ac10b-58cc-4372-a567-0e02b2c3d479",
  "Method": "GET",
  "Path": "/health",
  "QueryString": "",
  "Headers": {
    "User-Agent": "curl/7.68.0",
    "Accept": "*/*"
  },
  "RemoteIpAddress": "127.0.0.1",
  "Timestamp": "2025-08-09T13:50:00.000Z",
  "Protocol": "HTTP/1.1",
  "IsHttps": false
}
```

### Response Log
```json
{
  "RequestId": "f47ac10b-58cc-4372-a567-0e02b2c3d479",
  "StatusCode": 200,
  "StatusText": "OK",
  "Headers": {
    "Content-Type": "application/json",
    "X-Correlation-ID": "f47ac10b-58cc-4372-a567-0e02b2c3d479"
  },
  "ElapsedMs": 45,
  "Timestamp": "2025-08-09T13:50:00.045Z"
}
```

## 🔧 Configuration Details

### Elasticsearch Index Template
- **Index Pattern**: `bff-gateway-logs-{0:yyyy.MM.dd}`
- **Shards**: 1 (development), configurable for production
- **Replicas**: 0 (development), should be 1+ for production
- **Retention**: Daily rolling indices

### Performance Categorization
- **normal**: < 1000ms
- **slow**: 1000ms - 5000ms
- **very_slow**: > 5000ms

## 🚦 Current Status

✅ **Successfully Tested & Verified:**
- Elasticsearch is healthy and indexing logs
- Kibana is accessible and ready for dashboards
- BFF Gateway is logging structured data
- **72 log entries** successfully indexed during testing
- Request/Response correlation working
- Performance metrics being captured

## 🎯 Next Steps

1. **Customize Kibana Dashboards** - Create specific visualizations for your monitoring needs
2. **Set up Alerts** - Configure Watcher or external alerting for error rates/performance issues
3. **Production Tuning** - Adjust Elasticsearch settings for production workload
4. **Log Retention** - Configure index lifecycle management (ILM) policies
5. **Security** - Add authentication and TLS for production deployment

## 🐛 Troubleshooting

### Common Issues
1. **Elasticsearch not connecting**: Check if container is running with `docker ps`
2. **No logs appearing**: Verify BFF Gateway is making requests and check Elasticsearch indices
3. **Kibana not loading**: Wait 1-2 minutes for full initialization
4. **Performance issues**: Adjust Elasticsearch memory settings in docker-compose

### Debug Commands
```powershell
# Check Elasticsearch health
curl http://localhost:9200/_cluster/health

# List indices
curl http://localhost:9200/_cat/indices

# Check specific index
curl http://localhost:9200/bff-gateway-logs-2025.08.09/_search?size=1
```

## 📞 Support

For issues or questions:
1. Check the `logs/` directory for application logs
2. Review Docker container logs: `docker logs erp-elasticsearch`
3. Verify network connectivity between services
4. Consult Elasticsearch and Kibana documentation
