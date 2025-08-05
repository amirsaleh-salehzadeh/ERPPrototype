#!/usr/bin/env pwsh
#
# Quick Start Script for ERP Prototype (.NET 10)
# Simple script to start all services quickly
#

Write-Host "🚀 Starting ERP Prototype Services (.NET 10)..." -ForegroundColor Green

# Build first
Write-Host "Building solution..." -ForegroundColor Yellow
dotnet build --configuration Release --verbosity quiet

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Build failed!" -ForegroundColor Red
    exit 1
}

Write-Host "✅ Build successful" -ForegroundColor Green

# Start services
Write-Host "Starting services..." -ForegroundColor Yellow

Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd 'src/Services/ERP.IdentityService'; Write-Host 'Identity Service Starting...' -ForegroundColor Magenta; dotnet run --urls 'http://localhost:5007'"

Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd 'src/Services/Playground.WeatherService'; Write-Host 'Weather Service Starting...' -ForegroundColor Yellow; dotnet run --urls 'http://localhost:5001'"

Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd 'src/Gateway/BFF.Gateway'; Write-Host 'Gateway Starting...' -ForegroundColor Green; dotnet run --urls 'http://localhost:5000'"

Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd 'src/Documentation/Scalar.Documentation'; Write-Host 'Documentation Starting...' -ForegroundColor Cyan; dotnet run --urls 'http://localhost:5002'"

Write-Host "⏳ Waiting for services to start..." -ForegroundColor Cyan
Start-Sleep -Seconds 8

Write-Host "`n✅ Services started!" -ForegroundColor Green
Write-Host "🌐 Available at:" -ForegroundColor Cyan
Write-Host "   • Gateway (Main): http://localhost:5000/swagger" -ForegroundColor White
Write-Host "   • Weather API: http://localhost:5001/swagger" -ForegroundColor White  
Write-Host "   • Documentation: http://localhost:5002" -ForegroundColor White
Write-Host "   • Identity API: http://localhost:5007/swagger" -ForegroundColor White

Write-Host "`n🌐 Opening main gateway..." -ForegroundColor Green
Start-Process "http://localhost:5000/swagger"
