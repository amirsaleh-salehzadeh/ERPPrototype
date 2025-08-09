# ERP Monitoring Setup Guide

## 📊 Complete Monitoring Solution

This setup provides comprehensive monitoring for your ERP system with:
- **Prometheus** for metrics collection
- **Grafana** for visualization dashboards  
- **Alertmanager** for alert routing
- **SMS Gateway** for critical notifications
- **Health checks** for all services

## 🚀 Quick Start

### 1. Start Monitoring Stack
```powershell
# Start all monitoring services
.\manage-monitoring.ps1 -Action start

# Check status
.\manage-monitoring.ps1 -Action status

# Test all services
.\manage-monitoring.ps1 -Action test
```

### 2. Configure SMS Alerts

#### Option A: Twilio (Recommended)
1. Sign up at https://www.twilio.com/
2. Get your Account SID and Auth Token
3. Create `.env` file in `monitoring/sms-gateway/`:
```bash
TWILIO_ACCOUNT_SID=ACxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
TWILIO_AUTH_TOKEN=your_auth_token_here
SMS_FROM_NUMBER=+1234567890
SMS_TO_NUMBERS=+1987654321,+1555123456
```

#### Option B: Email Notifications
```bash
SMTP_HOST=smtp.gmail.com
SMTP_PORT=587
SMTP_USER=your-email@gmail.com
SMTP_PASS=your-app-password
EMAIL_TO=admin@company.com,ops@company.com
```

### 3. Test Alerts
```powershell
# Test SMS functionality
curl -X POST "http://localhost:8080/test-sms" -H "Authorization: Basic $(btoa('admin:sms-secret-key'))"

# Test email functionality  
curl -X POST "http://localhost:8080/test-email"
```

## 🎯 Access Points

| Service | URL | Credentials | Purpose |
|---------|-----|-------------|---------|
| **Grafana** | http://localhost:3000 | admin/admin | 📊 Dashboards & Visualization |
| **Prometheus** | http://localhost:9090 | None | 📈 Metrics & Targets |
| **Alertmanager** | http://localhost:9093 | None | 🚨 Alert Management |
| **SMS Gateway** | http://localhost:8080/health | None | 📱 Notification Health |

## 📋 Service Health Endpoints

| Service | Health Check | Metrics | Ready Check |
|---------|-------------|---------|-------------|
| **BFF Gateway** | http://localhost:5000/health | http://localhost:5000/metrics | http://localhost:5000/health/ready |
| **Identity Service** | http://localhost:5007/health | http://localhost:5007/metrics | http://localhost:5007/health/ready |
| **Weather Service** | http://localhost:5001/health | http://localhost:5001/metrics | http://localhost:5001/health/ready |
| **Scalar Docs** | http://localhost:5002/health | http://localhost:5002/metrics | http://localhost:5002/health/ready |

## 🚨 Alert Rules

### Critical Alerts (SMS + Email)
- ❌ Service is down (UP == 0)
- 🔥 High CPU usage (>80% for 5 minutes)
- 💾 High memory usage (>90% for 5 minutes)
- ⏱️ High response time (>2 seconds for 5 minutes)

### Warning Alerts (Email only)
- ⚠️ Service degraded performance
- 📊 Unusual traffic patterns
- 🔄 Service restart detected

## 🛠️ Management Commands

```powershell
# Start monitoring
.\manage-monitoring.ps1 -Action start

# Stop monitoring
.\manage-monitoring.ps1 -Action stop

# Restart monitoring
.\manage-monitoring.ps1 -Action restart

# View logs (all services)
.\manage-monitoring.ps1 -Action logs

# View specific service logs
.\manage-monitoring.ps1 -Action logs -Service prometheus

# Check status
.\manage-monitoring.ps1 -Action status

# Test all endpoints
.\manage-monitoring.ps1 -Action test
```

## 📊 Grafana Dashboard Features

### ERP Services Dashboard includes:
- 🟢 Real-time service health status
- 📈 Response time trends
- 💻 CPU and memory usage
- 📊 Request rate metrics
- 🔄 Service uptime tracking

### Default Panels:
1. **Service Health Status** - Quick overview of all services
2. **Response Times** - Performance trending
3. **System Metrics** - CPU and memory usage
4. **Service Details Table** - Detailed health information

## 🔧 Customization

### Adding New Services
1. Update `prometheus.yml` with new scrape targets
2. Add health checks to the new service
3. Update alert rules in `alert_rules.yml`
4. Modify Grafana dashboard queries

### Custom Alert Channels
1. Edit `alertmanager/alertmanager.yml`
2. Add new receivers and routing rules
3. Configure webhook endpoints in SMS gateway
4. Test with custom notification endpoints

## 📱 SMS Gateway API

### Endpoints:
- `GET /health` - Health check
- `POST /sms` - Send SMS alert (requires auth)
- `POST /email` - Send email alert
- `POST /test-sms` - Test SMS functionality
- `POST /test-email` - Test email functionality

### Authentication:
- Username: `admin`
- Password: `sms-secret-key`
- Use Basic Authentication header

## 🐛 Troubleshooting

### SMS Gateway Not Working
1. Check `.env` file configuration
2. Verify Twilio credentials
3. Check container logs: `docker logs erp-monitoring-sms-gateway-1`

### Prometheus Not Scraping
1. Verify service health endpoints are accessible
2. Check firewall rules
3. Review Prometheus targets: http://localhost:9090/targets

### Grafana Dashboard Empty
1. Confirm Prometheus datasource is configured
2. Verify metrics are being collected
3. Check Grafana logs for errors

### Alerts Not Firing
1. Check alert rules in Prometheus: http://localhost:9090/alerts
2. Verify Alertmanager configuration
3. Test webhook endpoints manually

## 📞 Emergency Contacts

Configure these in your `.env` file:
```bash
# Primary on-call
SMS_TO_NUMBERS=+1555123456

# Secondary contacts  
EMAIL_TO=ops@company.com,devops@company.com,manager@company.com
```

## 🔒 Security Notes

- Change default Grafana password after first login
- Secure SMS gateway endpoints in production
- Use environment variables for sensitive configuration
- Regular backup of Grafana dashboards and Prometheus data
- Monitor SMS usage to avoid unexpected charges

---

## 🎉 You're Ready!

Your ERP monitoring system is now configured to:
✅ Monitor all services 24/7
✅ Send SMS alerts for critical issues  
✅ Provide detailed dashboards
✅ Track performance metrics
✅ Enable quick troubleshooting

Need help? Check the logs or test individual components using the management script!
