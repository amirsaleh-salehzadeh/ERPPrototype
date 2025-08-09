# ERP Monitoring Stack Management Script
# This script manages the Prometheus/Grafana monitoring infrastructure for the ERP system

param(
    [Parameter(Mandatory=$false)]
    [ValidateSet("start", "stop", "restart", "status", "logs", "test")]
    [string]$Action = "status",
    
    [Parameter(Mandatory=$false)]
    [string]$Service = "all"
)

$ErrorActionPreference = "Stop"

# Configuration
$MonitoringComposeFile = "docker-compose.monitoring.yml"
$ProjectName = "erp-monitoring"

Write-Host "🔍 ERP Monitoring Stack Manager" -ForegroundColor Cyan
Write-Host "=================================" -ForegroundColor Cyan

function Test-DockerRunning {
    try {
        docker version | Out-Null
        return $true
    } catch {
        Write-Host "❌ Docker is not running or not installed!" -ForegroundColor Red
        return $false
    }
}

function Show-MonitoringStatus {
    Write-Host "📊 Checking monitoring stack status..." -ForegroundColor Yellow
    
    if (Test-Path $MonitoringComposeFile) {
        Write-Host "✅ Monitoring compose file found" -ForegroundColor Green
        
        try {
            $containers = docker-compose -f $MonitoringComposeFile -p $ProjectName ps --format "table {{.Name}}\t{{.State}}\t{{.Ports}}"
            Write-Host "`n🐳 Monitoring containers status:" -ForegroundColor Cyan
            Write-Host $containers
            
            Write-Host "`n🔗 Monitoring URLs:" -ForegroundColor Cyan
            Write-Host "  📈 Prometheus: http://localhost:9090" -ForegroundColor White
            Write-Host "  📊 Grafana: http://localhost:3000 (admin/admin)" -ForegroundColor White
            Write-Host "  🚨 Alertmanager: http://localhost:9093" -ForegroundColor White
            Write-Host "  📱 SMS Gateway: http://localhost:8080/health" -ForegroundColor White
            
        } catch {
            Write-Host "⚠️ Could not get container status. Stack may not be running." -ForegroundColor Yellow
        }
    } else {
        Write-Host "❌ Monitoring compose file not found!" -ForegroundColor Red
    }
}

function Start-MonitoringStack {
    Write-Host "🚀 Starting monitoring stack..." -ForegroundColor Green
    
    if (-not (Test-DockerRunning)) {
        return
    }
    
    if (-not (Test-Path $MonitoringComposeFile)) {
        Write-Host "❌ Monitoring compose file not found!" -ForegroundColor Red
        return
    }
    
    try {
        Write-Host "📦 Pulling latest images..." -ForegroundColor Yellow
        docker-compose -f $MonitoringComposeFile -p $ProjectName pull
        
        Write-Host "🔧 Building SMS gateway..." -ForegroundColor Yellow
        docker-compose -f $MonitoringComposeFile -p $ProjectName build sms-gateway
        
        Write-Host "▶️ Starting services..." -ForegroundColor Yellow
        docker-compose -f $MonitoringComposeFile -p $ProjectName up -d
        
        Write-Host "⏳ Waiting for services to be ready..." -ForegroundColor Yellow
        Start-Sleep -Seconds 15
        
        Write-Host "✅ Monitoring stack started successfully!" -ForegroundColor Green
        Show-MonitoringStatus
        
        # Test SMS Gateway
        Write-Host "`n🧪 Testing SMS Gateway..." -ForegroundColor Yellow
        try {
            $response = Invoke-RestMethod -Uri "http://localhost:8080/health" -Method Get
            Write-Host "✅ SMS Gateway is healthy: $($response.status)" -ForegroundColor Green
        } catch {
            Write-Host "⚠️ SMS Gateway may not be ready yet" -ForegroundColor Yellow
        }
        
    } catch {
        Write-Host "❌ Failed to start monitoring stack: $($_.Exception.Message)" -ForegroundColor Red
    }
}

function Stop-MonitoringStack {
    Write-Host "🛑 Stopping monitoring stack..." -ForegroundColor Red
    
    if (-not (Test-DockerRunning)) {
        return
    }
    
    try {
        docker-compose -f $MonitoringComposeFile -p $ProjectName down
        Write-Host "✅ Monitoring stack stopped successfully!" -ForegroundColor Green
    } catch {
        Write-Host "❌ Failed to stop monitoring stack: $($_.Exception.Message)" -ForegroundColor Red
    }
}

function Show-Logs {
    param([string]$ServiceName = "")
    
    Write-Host "📋 Showing monitoring stack logs..." -ForegroundColor Yellow
    
    if (-not (Test-DockerRunning)) {
        return
    }
    
    try {
        if ($ServiceName -and $ServiceName -ne "all") {
            docker-compose -f $MonitoringComposeFile -p $ProjectName logs -f $ServiceName
        } else {
            docker-compose -f $MonitoringComposeFile -p $ProjectName logs -f
        }
    } catch {
        Write-Host "❌ Failed to show logs: $($_.Exception.Message)" -ForegroundColor Red
    }
}

function Test-MonitoringStack {
    Write-Host "🧪 Testing monitoring stack..." -ForegroundColor Yellow
    
    $services = @(
        @{ Name = "Prometheus"; Url = "http://localhost:9090/-/healthy"; Expected = "Prometheus is Healthy." }
        @{ Name = "Grafana"; Url = "http://localhost:3000/api/health"; Expected = "ok" }
        @{ Name = "Alertmanager"; Url = "http://localhost:9093/-/healthy"; Expected = "OK" }
        @{ Name = "SMS Gateway"; Url = "http://localhost:8080/health"; Expected = "healthy" }
    )
    
    foreach ($service in $services) {
        try {
            Write-Host "  Testing $($service.Name)..." -ForegroundColor White
            $response = Invoke-RestMethod -Uri $service.Url -Method Get -TimeoutSec 10
            
            if ($response -match $service.Expected -or $response.status -eq $service.Expected) {
                Write-Host "  ✅ $($service.Name) is healthy" -ForegroundColor Green
            } else {
                Write-Host "  ⚠️ $($service.Name) responded but status unclear" -ForegroundColor Yellow
            }
        } catch {
            Write-Host "  ❌ $($service.Name) is not responding" -ForegroundColor Red
        }
    }
    
    # Test service health endpoints
    Write-Host "`n🔍 Testing ERP service health endpoints..." -ForegroundColor Yellow
    $erpServices = @(
        @{ Name = "Identity Service"; Url = "http://localhost:5007/health" }
        @{ Name = "Weather Service"; Url = "http://localhost:5001/health" }
        @{ Name = "BFF Gateway"; Url = "http://localhost:5000/health" }
        @{ Name = "Scalar Docs"; Url = "http://localhost:5002/health" }
    )
    
    foreach ($service in $erpServices) {
        try {
            $response = Invoke-RestMethod -Uri $service.Url -Method Get -TimeoutSec 5
            if ($response.status -eq "Healthy") {
                Write-Host "  ✅ $($service.Name) is healthy" -ForegroundColor Green
            } else {
                Write-Host "  ⚠️ $($service.Name): $($response.status)" -ForegroundColor Yellow
            }
        } catch {
            Write-Host "  ❌ $($service.Name) is not responding" -ForegroundColor Red
        }
    }
    
    Write-Host "`n📊 Access your monitoring dashboards:" -ForegroundColor Cyan
    Write-Host "  📈 Prometheus: http://localhost:9090" -ForegroundColor White
    Write-Host "  📊 Grafana: http://localhost:3000 (admin/admin)" -ForegroundColor White
    Write-Host "  🚨 Alertmanager: http://localhost:9093" -ForegroundColor White
}

# Main execution
switch ($Action.ToLower()) {
    "start" { Start-MonitoringStack }
    "stop" { Stop-MonitoringStack }
    "restart" { 
        Stop-MonitoringStack
        Start-Sleep -Seconds 5
        Start-MonitoringStack
    }
    "status" { Show-MonitoringStatus }
    "logs" { Show-Logs -ServiceName $Service }
    "test" { Test-MonitoringStack }
    default { 
        Write-Host "❌ Invalid action. Use: start, stop, restart, status, logs, test" -ForegroundColor Red
        exit 1
    }
}

Write-Host "`n🎉 Done!" -ForegroundColor Green
