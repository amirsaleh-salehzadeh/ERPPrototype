#!/usr/bin/env pwsh
#
# Stop All ERP Prototype Services
# Stops all running services by killing processes using the configured ports
#

Write-Host "🛑 Stopping ERP Prototype Services..." -ForegroundColor Red

$ports = @(5000, 5001, 5002, 5007)
$stoppedCount = 0

foreach ($port in $ports) {
    Write-Host "Checking port $port..." -ForegroundColor Yellow
    
    try {
        $connections = Get-NetTCPConnection -LocalPort $port -ErrorAction SilentlyContinue
        
        if ($connections) {
            foreach ($connection in $connections) {
                $processId = $connection.OwningProcess
                $process = Get-Process -Id $processId -ErrorAction SilentlyContinue
                
                if ($process) {
                    Write-Host "  Stopping $($process.Name) (PID: $processId)" -ForegroundColor White
                    Stop-Process -Id $processId -Force -ErrorAction SilentlyContinue
                    $stoppedCount++
                }
            }
        } else {
            Write-Host "  No process found on port $port" -ForegroundColor Gray
        }
    }
    catch {
        Write-Host "  Error checking port $port`: $($_.Exception.Message)" -ForegroundColor Red
    }
}

if ($stoppedCount -gt 0) {
    Write-Host "`n✅ Stopped $stoppedCount process(es)" -ForegroundColor Green
} else {
    Write-Host "`n💡 No services were running" -ForegroundColor Cyan
}

Write-Host "🏁 Done!" -ForegroundColor Green
