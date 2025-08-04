#!/usr/bin/env pwsh
Write-Host "Starting Scalar Documentation service on .NET 10..." -ForegroundColor Green
Set-Location "src/Documentation/Scalar.Documentation"
dotnet run --urls "http://localhost:5000"
