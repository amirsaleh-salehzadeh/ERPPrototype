# 📊 Elasticsearch Logging Setup

This document explains how to set up and use Elasticsearch for comprehensive request logging in the BFF Gateway.

## 🚀 Quick Start

### 1. Start Elasticsearch and Kibana

```bash
# Create the network if it doesn't exist
docker network create erp-network

# Start Elasticsearch and Kibana
docker-compose -f docker-compose.elasticsearch.yml up -d

# Check status
docker-compose -f docker-compose.elasticsearch.yml ps
```

### 2. Verify Elasticsearch is Running

```bash
# Check Elasticsearch health
curl http://localhost:9200/_cluster/health

# Check indices (after some requests are logged)
curl http://localhost:9200/_cat/indices?v
```

### 3. Access Kibana Dashboard

Open your browser and navigate to: http://localhost:5601

## 📋 What Gets Logged

The BFF Gateway logs comprehensive request/response data to Elasticsearch:

### Request Data
- **Request ID**: Unique identifier for correlation
- **Timestamp**: UTC timestamp
- **HTTP Method**: GET, POST, PUT, DELETE, etc.
- **Path**: Request path
- **Query String**: URL parameters
- **Headers**: All request headers (sanitized)
- **Content Type & Length**: Request body metadata
- **Body**: Request body (up to 10KB, configurable)
- **User Agent**: Client information
- **Remote IP**: Client IP address
- **Service Mapping**: Target service information
- **User Information**: User ID, name from API key validation
- **API Key**: Masked API key (first 8 chars + ***)

### Response Data
- **Status Code**: HTTP response status
- **Headers**: All response headers
- **Content Type & Length**: Response body metadata
- **Body**: Response body (up to 10KB, configurable)
- **Duration**: Request processing time in milliseconds

### Additional Context
- **Trace ID**: Distributed tracing correlation
- **Span ID**: Request span identifier
- **Machine Name**: Server hostname
- **Environment**: Development/Production
- **Thread ID**: Processing thread

## 🔍 Kibana Setup

### 1. Create Index Pattern

1. Open Kibana at http://localhost:5601
2. Go to **Stack Management** → **Index Patterns**
3. Click **Create index pattern**
4. Enter pattern: `erp-bff-gateway-*`
5. Select **@timestamp** as time field
6. Click **Create index pattern**

### 2. Useful Kibana Queries

#### All Requests
```
*
```

#### Failed Requests (4xx/5xx)
```
ResponseDetails.StatusCode:>=400
```

#### Slow Requests (>1000ms)
```
Duration:>1000
```

#### Requests by Service
```
RequestDetails.ServiceName:"WeatherService"
```

#### Requests by User
```
RequestDetails.UserName:"john.doe"
```

#### API Errors
```
ResponseDetails.StatusCode:>=500
```

### 3. Create Dashboards

#### Request Volume Dashboard
- **Visualization**: Line chart
- **X-axis**: @timestamp (Date Histogram)
- **Y-axis**: Count of requests
- **Split Series**: RequestDetails.Method.keyword

#### Response Time Dashboard
- **Visualization**: Line chart
- **X-axis**: @timestamp
- **Y-axis**: Average Duration
- **Split Series**: RequestDetails.ServiceName.keyword

#### Error Rate Dashboard
- **Visualization**: Pie chart
- **Buckets**: Terms aggregation on Success field
- **Metrics**: Count

## 🛠️ Configuration

### Index Settings

The Elasticsearch sink is configured in `appsettings.json`:

```json
{
  "Name": "Elasticsearch",
  "Args": {
    "nodeUris": "http://localhost:9200",
    "indexFormat": "erp-bff-gateway-logs-{0:yyyy.MM.dd}",
    "autoRegisterTemplate": true,
    "numberOfShards": 2,
    "numberOfReplicas": 1,
    "batchPostingLimit": 50,
    "period": "00:00:02"
  }
}
```

### Development vs Production

**Development** (`appsettings.Development.json`):
- More verbose logging (Debug level)
- Smaller batches for faster feedback
- Single shard, no replicas
- Index: `erp-bff-gateway-dev-logs-*`

**Production** (`appsettings.json`):
- Information level logging
- Larger batches for efficiency
- Multiple shards and replicas
- Index: `erp-bff-gateway-logs-*`

## 🔧 Troubleshooting

### Elasticsearch Not Starting
```bash
# Check logs
docker logs erp-elasticsearch

# Common issues:
# 1. Insufficient memory - increase Docker memory limit
# 2. Port conflicts - check if port 9200 is in use
# 3. Disk space - ensure sufficient disk space
```

### No Logs Appearing
```bash
# Check BFF Gateway logs
docker logs erp-gateway

# Verify Elasticsearch connection
curl http://localhost:9200/_cluster/health

# Check if indices are created
curl http://localhost:9200/_cat/indices?v
```

### Kibana Connection Issues
```bash
# Check Kibana logs
docker logs erp-kibana

# Verify Kibana can reach Elasticsearch
docker exec erp-kibana curl http://elasticsearch:9200
```

## 📈 Performance Considerations

### Index Management
- Indices are created daily (`yyyy.MM.dd` format)
- Consider setting up Index Lifecycle Management (ILM)
- Archive old indices to reduce storage

### Resource Usage
- Elasticsearch: 512MB heap (configurable)
- Kibana: ~200MB memory
- Network: Minimal impact with batching

### Scaling
- Increase `numberOfShards` for high-volume environments
- Add replicas for high availability
- Consider dedicated Elasticsearch cluster for production

## 🔒 Security Notes

- Current setup disables Elasticsearch security for development
- For production, enable authentication and TLS
- Sensitive data (API keys) are automatically masked
- Request/response bodies are limited to 10KB

## 📚 Useful Commands

```bash
# Start only Elasticsearch
docker-compose -f docker-compose.elasticsearch.yml up elasticsearch -d

# View real-time logs
docker-compose -f docker-compose.elasticsearch.yml logs -f

# Stop and remove
docker-compose -f docker-compose.elasticsearch.yml down

# Remove data (destructive!)
docker-compose -f docker-compose.elasticsearch.yml down -v
```
