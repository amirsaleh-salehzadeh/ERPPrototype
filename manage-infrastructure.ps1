# ============================================================================
# ERP PROTOTYPE INFRASTRUCTURE MANAGEMENT SCRIPT
# ============================================================================
# PowerShell script for managing the complete ERP infrastructure stack.
# Provides easy commands to start, stop, monitor, and test all services.
#
# INFRASTRUCTURE COMPONENTS:
# 🔧 Redis Stack:      Caching and session storage
# 📊 ELK Stack:        Centralized logging (Elasticsearch, Logstash, Kibana)
# 📈 Monitoring Stack: Metrics and alerting (Prometheus, Grafana, Alertmanager)
# 📱 SMS Gateway:      Custom notification service for alerts
# 🔍 Node Exporter:    System metrics collection
#
# USAGE EXAMPLES:
#   .\manage-infrastructure.ps1 start all          # Start all services
#   .\manage-infrastructure.ps1 start elk          # Start only ELK stack
#   .\manage-infrastructure.ps1 stop monitoring    # Stop monitoring services
#   .\manage-infrastructure.ps1 logs sms -Follow   # Follow SMS gateway logs
#   .\manage-infrastructure.ps1 status             # Show status of all services
#   .\manage-infrastructure.ps1 test               # Test all services
#
# PREREQUISITES:
# - Docker Desktop installed and running
# - docker-compose command available
# - PowerShell execution policy allows script execution
# ============================================================================

# ============================================================================
# SCRIPT PARAMETERS
# ============================================================================
param(
    # Action to perform on the infrastructure
    [Parameter(Mandatory=$true)]
    [ValidateSet("start", "stop", "restart", "status", "logs", "clean", "build", "test")]
    [string]$Action,
    
    # Service group or specific service to target
    [Parameter(Mandatory=$false)]
    [ValidateSet("all", "redis", "elk", "monitoring", "sms")]
    [string]$Service = "all",
    
    # Follow logs in real-time (for logs action)
    [Parameter(Mandatory=$false)]
    [switch]$Follow
)

# ============================================================================
# UTILITY FUNCTIONS - Colored console output for better UX
# ============================================================================
function Write-Success { param($Message) Write-Host "✅ $Message" -ForegroundColor Green }  # Success messages
function Write-Warning { param($Message) Write-Host "⚠️ $Message" -ForegroundColor Yellow } # Warning messages
function Write-Error { param($Message) Write-Host "❌ $Message" -ForegroundColor Red }      # Error messages
function Write-Info { param($Message) Write-Host "ℹ️ $Message" -ForegroundColor Cyan }     # Info messages

# ============================================================================
# SERVICE GROUP DEFINITIONS
# ============================================================================
# Define logical groups of services for easier management
$ServiceGroups = @{
    "redis" = @("redis", "redis-commander")                                                    # Redis caching services
    "elk" = @("elasticsearch", "kibana", "logstash")                                          # ELK logging stack
    "monitoring" = @("prometheus", "grafana", "alertmanager", "node-exporter")                # Monitoring stack
    "sms" = @("sms-gateway")                                                                   # SMS notification service
    "all" = @("redis", "redis-commander", "elasticsearch", "kibana", "logstash", 
              "prometheus", "grafana", "alertmanager", "sms-gateway", "node-exporter")        # All services
}

# ============================================================================
# HELPER FUNCTIONS
# ============================================================================

# Convert service group name to list of actual service names
function Get-ServiceList {
    param($ServiceGroup)
    
    if ($ServiceGroups.ContainsKey($ServiceGroup)) {
        return $ServiceGroups[$ServiceGroup]
    } else {
        return @($ServiceGroup)  # Treat as individual service name
    }
}

# ============================================================================
# INFRASTRUCTURE MANAGEMENT FUNCTIONS
# ============================================================================

# Start infrastructure services with dependency management
function Start-Infrastructure {
    param($Services)
    
    Write-Info "🚀 Starting ERP Infrastructure services..."
    
    # Check if .env file exists, if not create from template
    if (-not (Test-Path ".env")) {
        Write-Warning "No .env file found. Creating from template..."
        if (Test-Path ".env.example") {
            Copy-Item ".env.example" ".env"
            Write-Warning "Please edit .env file with your actual SMS and email credentials before proceeding"
            return
        }
    }
    
    if ($Services -contains "all") {
        # Start services in dependency order
        Write-Info "Starting Redis..."
        docker-compose up -d redis redis-commander
        Start-Sleep 5
        
        Write-Info "Starting Elasticsearch..."
        docker-compose up -d elasticsearch
        Start-Sleep 10
        
        Write-Info "Starting ELK stack..."
        docker-compose up -d kibana logstash
        Start-Sleep 5
        
        Write-Info "Starting monitoring stack..."
        docker-compose up -d prometheus grafana alertmanager node-exporter
        Start-Sleep 5
        
        Write-Info "Starting SMS gateway..."
        docker-compose up -d sms-gateway
    } else {
        $serviceList = $Services -join " "
        docker-compose up -d $Services
    }
    
    Write-Success "Infrastructure services started successfully!"
    Show-ServiceStatus
}

function Stop-Infrastructure {
    param($Services)
    
    Write-Info "⏹️ Stopping ERP Infrastructure services..."
    
    if ($Services -contains "all") {
        docker-compose down
    } else {
        docker-compose stop $Services
    }
    
    Write-Success "Infrastructure services stopped successfully!"
}

function Show-ServiceStatus {
    Write-Info "📊 ERP Infrastructure Status:"
    Write-Host ""
    
    $status = docker-compose ps --format "table"
    Write-Host $status
    
    Write-Host ""
    Write-Info "🌐 Service URLs:"
    Write-Host "Redis Commander:     http://localhost:8081" -ForegroundColor White
    Write-Host "Elasticsearch:       http://localhost:9200" -ForegroundColor White
    Write-Host "Kibana:             http://localhost:5601" -ForegroundColor White
    Write-Host "Prometheus:         http://localhost:9090" -ForegroundColor White
    Write-Host "Grafana:            http://localhost:3000 (admin/admin123)" -ForegroundColor White
    Write-Host "Alertmanager:       http://localhost:9093" -ForegroundColor White
    Write-Host "SMS Gateway:        http://localhost:8080/health" -ForegroundColor White
    Write-Host "Node Exporter:      http://localhost:9100" -ForegroundColor White
    Write-Host ""
    
    Write-Info "📱 ERP Services (when running):"
    Write-Host "Identity Service:    http://localhost:5007/health" -ForegroundColor White
    Write-Host "Weather Service:     http://localhost:5006/health" -ForegroundColor White
    Write-Host "BFF Gateway:         http://localhost:5001/health" -ForegroundColor White
    Write-Host "Scalar Documentation: http://localhost:5002/health" -ForegroundColor White
}

function Show-Logs {
    param($Services, $Follow)
    
    Write-Info "📋 Showing logs for: $($Services -join ', ')"
    
    if ($Follow) {
        docker-compose logs -f $Services
    } else {
        docker-compose logs --tail=50 $Services
    }
}

function Clean-Infrastructure {
    Write-Warning "🧹 This will remove all containers, networks, and volumes. Are you sure? (y/N)"
    $confirm = Read-Host
    
    if ($confirm -eq "y" -or $confirm -eq "Y") {
        Write-Info "Cleaning up infrastructure..."
        docker-compose down -v --remove-orphans
        docker system prune -f
        Write-Success "Infrastructure cleaned successfully!"
    } else {
        Write-Info "Cleanup cancelled."
    }
}

function Build-Infrastructure {
    Write-Info "🔨 Building custom images..."
    docker-compose build --no-cache sms-gateway
    Write-Success "Build completed!"
}

function Test-Infrastructure {
    Write-Info "🧪 Testing infrastructure components..."
    
    # Test Redis
    Write-Info "Testing Redis connection..."
    $redisTest = docker-compose exec redis redis-cli ping 2>$null
    if ($redisTest -eq "PONG") {
        Write-Success "Redis is responding"
    } else {
        Write-Error "Redis is not responding"
    }
    
    # Test Elasticsearch
    Write-Info "Testing Elasticsearch..."
    try {
        $esResponse = Invoke-RestMethod -Uri "http://localhost:9200/_cluster/health" -TimeoutSec 5
        if ($esResponse.status -eq "green" -or $esResponse.status -eq "yellow") {
            Write-Success "Elasticsearch is healthy ($($esResponse.status))"
        } else {
            Write-Warning "Elasticsearch status: $($esResponse.status)"
        }
    } catch {
        Write-Error "Elasticsearch is not responding"
    }
    
    # Test Kibana
    Write-Info "Testing Kibana..."
    try {
        $kibanaResponse = Invoke-RestMethod -Uri "http://localhost:5601/api/status" -TimeoutSec 5
        if ($kibanaResponse.status.overall.state -eq "green") {
            Write-Success "Kibana is healthy"
        } else {
            Write-Warning "Kibana status: $($kibanaResponse.status.overall.state)"
        }
    } catch {
        Write-Error "Kibana is not responding"
    }
    
    # Test Prometheus
    Write-Info "Testing Prometheus..."
    try {
        $promResponse = Invoke-RestMethod -Uri "http://localhost:9090/-/healthy" -TimeoutSec 5
        Write-Success "Prometheus is healthy"
    } catch {
        Write-Error "Prometheus is not responding"
    }
    
    # Test Grafana
    Write-Info "Testing Grafana..."
    try {
        $grafanaResponse = Invoke-RestMethod -Uri "http://localhost:3000/api/health" -TimeoutSec 5
        if ($grafanaResponse.database -eq "ok") {
            Write-Success "Grafana is healthy"
        } else {
            Write-Warning "Grafana database issue"
        }
    } catch {
        Write-Error "Grafana is not responding"
    }
    
    # Test SMS Gateway
    Write-Info "Testing SMS Gateway..."
    try {
        $smsResponse = Invoke-RestMethod -Uri "http://localhost:8080/health" -TimeoutSec 5
        if ($smsResponse.status -eq "healthy") {
            Write-Success "SMS Gateway is healthy"
        } else {
            Write-Warning "SMS Gateway status: $($smsResponse.status)"
        }
    } catch {
        Write-Error "SMS Gateway is not responding"
    }
}

# Main script logic
try {
    $services = Get-ServiceList $Service
    
    switch ($Action) {
        "start" { Start-Infrastructure $services }
        "stop" { Stop-Infrastructure $services }
        "restart" { 
            Stop-Infrastructure $services
            Start-Sleep 3
            Start-Infrastructure $services
        }
        "status" { Show-ServiceStatus }
        "logs" { Show-Logs $services $Follow }
        "clean" { Clean-Infrastructure }
        "build" { Build-Infrastructure }
        "test" { Test-Infrastructure }
    }
} catch {
    Write-Error "An error occurred: $($_.Exception.Message)"
    exit 1
}

# Examples:
# .\manage-infrastructure.ps1 start all          # Start all services
# .\manage-infrastructure.ps1 start elk          # Start only ELK stack
# .\manage-infrastructure.ps1 stop monitoring    # Stop monitoring services
# .\manage-infrastructure.ps1 logs sms -Follow   # Follow SMS gateway logs
# .\manage-infrastructure.ps1 status             # Show status of all services
# .\manage-infrastructure.ps1 test               # Test all services
