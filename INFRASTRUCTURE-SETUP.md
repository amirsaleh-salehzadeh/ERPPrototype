# ERP Prototype - Complete Infrastructure Setup

This document provides instructions for setting up and managing the complete ERP infrastructure stack including caching, logging, monitoring, and alerting capabilities.

## 📋 Prerequisites

- Docker Desktop installed and running
- PowerShell (for Windows management scripts)
- At least 8GB RAM available for containers
- Ports 3000, 5601, 6379, 8080, 8081, 9090, 9093, 9100, 9200, 9300 available

## 🏗️ Infrastructure Components

### Redis Stack (Caching)
- **Redis**: Primary cache and session storage
- **Redis Commander**: Web UI for Redis management

### ELK Stack (Logging)
- **Elasticsearch**: Log storage and search engine
- **Logstash**: Log processing pipeline
- **Kibana**: Log visualization and dashboards

### Monitoring Stack (Observability)
- **Prometheus**: Metrics collection and storage
- **Grafana**: Metrics visualization and dashboards
- **Alertmanager**: Alert routing and management
- **Node Exporter**: System metrics collection
- **SMS Gateway**: Custom notification service for alerts

## 🚀 Quick Start

### 1. Clone and Navigate
```bash
cd c:\AIProj\ERPPrototype
```

### 2. Configure Notifications (Optional but Recommended)
```bash
# Copy the environment template
copy .env.example .env

# Edit .env with your actual credentials
notepad .env
```

**Important**: Update the following in your `.env` file:
- `TWILIO_ACCOUNT_SID` and `TWILIO_AUTH_TOKEN` (for SMS alerts)
- `SMS_FROM_NUMBER` and `SMS_TO_NUMBERS` (your phone numbers)
- `SMTP_USER` and `SMTP_PASS` (for email alerts)

### 3. Start the Infrastructure
```powershell
# Start all services
.\manage-infrastructure.ps1 start all

# Or start specific services
.\manage-infrastructure.ps1 start elk          # Only ELK stack
.\manage-infrastructure.ps1 start monitoring   # Only monitoring
.\manage-infrastructure.ps1 start redis        # Only Redis
```

### 4. Verify Services
```powershell
# Check status of all services
.\manage-infrastructure.ps1 status

# Test service health
.\manage-infrastructure.ps1 test
```

## 🌐 Service URLs

Once started, access the following services:

| Service | URL | Credentials |
|---------|-----|-------------|
| Redis Commander | http://localhost:8081 | None |
| Elasticsearch | http://localhost:9200 | None |
| Kibana | http://localhost:5601 | None |
| Prometheus | http://localhost:9090 | None |
| Grafana | http://localhost:3000 | admin/admin123 |
| Alertmanager | http://localhost:9093 | None |
| SMS Gateway | http://localhost:8080/health | None |

### ERP Application Services (when running)
| Service | URL | Health Check |
|---------|-----|-------------|
| Identity Service | http://localhost:5007 | /health |
| Weather Service | http://localhost:5006 | /health |
| BFF Gateway | http://localhost:5001 | /health |
| Documentation | http://localhost:5002 | /health |

## 📊 Monitoring Setup

### Grafana Dashboards
1. Open Grafana at http://localhost:3000
2. Login with `admin/admin123`
3. Import dashboards from `/monitoring/grafana/dashboards/`
4. Configure data sources (Prometheus is auto-configured)

### Prometheus Targets
Prometheus is configured to scrape metrics from:
- Node Exporter (system metrics)
- ERP services (application metrics)
- Infrastructure services

### Alert Configuration
Alerts are configured for:
- Service downtime (critical)
- High resource usage (warning)
- Failed health checks (critical)

## 📱 SMS Alert Setup

### Twilio Configuration
1. Sign up at [Twilio Console](https://console.twilio.com/)
2. Get your Account SID and Auth Token
3. Purchase a phone number
4. Update `.env` file with credentials

### Testing Alerts
```powershell
# Test SMS functionality
curl -X POST http://localhost:8080/test-sms -H "Authorization: Basic YWRtaW46c21zLXNlY3JldC1rZXk="

# Test email functionality
curl -X POST http://localhost:8080/test-email
```

## 🛠️ Management Commands

### Starting Services
```powershell
.\manage-infrastructure.ps1 start all          # All services
.\manage-infrastructure.ps1 start elk          # ELK stack only
.\manage-infrastructure.ps1 start monitoring   # Monitoring only
.\manage-infrastructure.ps1 start redis        # Redis only
```

### Stopping Services
```powershell
.\manage-infrastructure.ps1 stop all           # All services
.\manage-infrastructure.ps1 stop monitoring    # Monitoring only
```

### Viewing Logs
```powershell
.\manage-infrastructure.ps1 logs all           # All service logs
.\manage-infrastructure.ps1 logs sms -Follow   # Follow SMS gateway logs
.\manage-infrastructure.ps1 logs elk           # ELK stack logs
```

### Maintenance
```powershell
.\manage-infrastructure.ps1 restart all        # Restart all services
.\manage-infrastructure.ps1 status            # Show service status
.\manage-infrastructure.ps1 clean             # Remove all containers and data
```

## 🔧 Troubleshooting

### Common Issues

1. **Port Conflicts**
   - Ensure ports 3000, 5601, 6379, 8080, 8081, 9090, 9093, 9100, 9200, 9300 are available
   - Stop any conflicting services or modify ports in `docker-compose.yml`

2. **Memory Issues**
   - Elasticsearch requires at least 2GB RAM
   - Ensure Docker has sufficient memory allocated (8GB+ recommended)

3. **SMS Gateway Not Starting**
   - Check if Node.js dependencies are properly installed in container
   - Verify `.env` file configuration
   - Check logs: `.\manage-infrastructure.ps1 logs sms`

4. **Services Not Connecting**
   - All services use the `erp-network` Docker network
   - Check Docker network: `docker network ls`
   - Restart services if network issues persist

### Health Checks
```powershell
# Quick health check of all services
.\manage-infrastructure.ps1 test

# Individual service checks
curl http://localhost:9200/_cluster/health     # Elasticsearch
curl http://localhost:5601/api/status          # Kibana
curl http://localhost:9090/-/healthy           # Prometheus
curl http://localhost:3000/api/health          # Grafana
curl http://localhost:8080/health              # SMS Gateway
```

### Log Analysis
```powershell
# Check container logs
docker-compose logs elasticsearch
docker-compose logs kibana
docker-compose logs prometheus
docker-compose logs sms-gateway

# Follow live logs
docker-compose logs -f --tail=100 sms-gateway
```

## 📝 Data Persistence

The following data is persisted in Docker volumes:
- `redis_data`: Redis data and configuration
- `elasticsearch_data`: Elasticsearch indices and data
- `prometheus_data`: Prometheus metrics and configuration
- `grafana_data`: Grafana dashboards and settings
- `alertmanager_data`: Alertmanager configuration and state

### Backup Data
```powershell
# Backup volumes
docker run --rm -v erp-prototype_elasticsearch_data:/data -v ${PWD}:/backup alpine tar czf /backup/elasticsearch-backup.tar.gz -C /data .
docker run --rm -v erp-prototype_grafana_data:/data -v ${PWD}:/backup alpine tar czf /backup/grafana-backup.tar.gz -C /data .
```

## 🔄 Updates and Maintenance

### Updating Services
1. Update image versions in `docker-compose.yml`
2. Rebuild and restart: `.\manage-infrastructure.ps1 build && .\manage-infrastructure.ps1 restart all`

### Adding New Services
1. Add service definition to `docker-compose.yml`
2. Update Prometheus configuration to scrape new service
3. Create Grafana dashboard for new service
4. Add health checks and alerts

## 📞 Support

For issues or questions:
1. Check the troubleshooting section above
2. Review container logs for error messages
3. Ensure all prerequisites are met
4. Verify network connectivity between services

## 🎯 Next Steps

1. **Configure Grafana Dashboards**: Create custom dashboards for your specific metrics
2. **Set Up Alert Rules**: Define custom alert rules in Prometheus
3. **Configure SMS Provider**: Set up your preferred SMS provider (Twilio, AWS SNS, etc.)
4. **Customize Logging**: Modify Logstash pipelines for your log format
5. **Security**: Implement authentication and SSL certificates for production use
