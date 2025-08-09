# ============================================================================
# ERP API TESTING SCRIPT
# ============================================================================
# This PowerShell script tests the API key validation system for the ERP
# prototype, including both direct Identity Service validation and end-to-end
# testing through the BFF Gateway.
#
# Test Coverage:
# 1. Direct Identity Service API key validation
# 2. BFF Gateway API key validation (via gRPC)
# 3. End-to-end weather service access through gateway
#
# Prerequisites:
# - Identity Service running on localhost:5007
# - BFF Gateway running on localhost:5000
# - Valid API key configured in the system
# ============================================================================

# Test API key validation with configured master key
$apiKey = "LGplFG5SbbcuGStQIBSlf2GGTStli3ZFdcGaMOhA4qM"  # admin_master key

# Prepare request body for Identity Service validation
$body = @{
    ApiKey = $apiKey                    # API key to validate
    ServiceName = "WeatherService"      # Target service name
    Endpoint = "/weatherforecast"       # Specific endpoint being accessed
} | ConvertTo-Json

Write-Host "============================================================================"
Write-Host "🔐 Testing Identity Service Direct Validation..."
Write-Host "============================================================================"
Write-Host "Request Body: $body" -ForegroundColor Cyan

try {
    # Test direct API key validation against Identity Service
    $response = Invoke-RestMethod -Uri "http://localhost:5007/validate" -Method POST -ContentType "application/json" -Body $body
    Write-Host "✅ Identity Service Response:" -ForegroundColor Green
    Write-Host $($response | ConvertTo-Json -Depth 3) -ForegroundColor White
} catch {
    Write-Host "❌ Identity Service Error: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        Write-Host "Response Details: $($_.Exception.Response)" -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "============================================================================"
Write-Host "🌐 Testing BFF Gateway with API Key Validation (gRPC)..."
Write-Host "============================================================================"

try {
    # Test API key validation through BFF Gateway (uses gRPC to Identity Service)
    $headers = @{
        "X-API-Key" = $apiKey           # API key in header (BFF Gateway standard)
    }
    
    Write-Host "Making request to BFF Gateway with API key header..." -ForegroundColor Cyan
    $response = Invoke-RestMethod -Uri "http://localhost:5000/api/weather/weatherforecast" -Method GET -Headers $headers
    
    Write-Host "✅ BFF Gateway Response (Weather Data):" -ForegroundColor Green
    Write-Host $($response | ConvertTo-Json -Depth 3) -ForegroundColor White
} catch {
    Write-Host "BFF Error: $($_.Exception.Message)"
}
