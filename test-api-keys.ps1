# PowerShell script to test API key validation pipeline
# Run this script after starting all services

Write-Host "ERP Prototype API Key Testing Script" -ForegroundColor Cyan
Write-Host "=======================================" -ForegroundColor Cyan
Write-Host ""

# Predefined API keys (updated with latest generated keys)
$apiKeys = @{
    "Admin Master" = "WQt1fpWUsNMOq6DuUYfIcAQfvO2MprTiff5-4q0svJE"
    "Dev Team Lead" = "Yppw-SaC0oCVei6hKLRVAhdeWctJDa5fTWc9bIdZ-Do"
    "QA Automation" = "kunurD6-ywinGUszH8Fc9xH57YpiKiHv7kvm9cUVgdU"
    "Monitoring Service" = "kDGcGwSFRdolzTmkFnjt9jlcQybn69VVRc1LrKJgRow"
    "Analytics Dashboard" = "r4ZKpmK9abTSk19T0Fw2O1XrBoGk0Hqx_tdizbUuhms"
}

# Test endpoints
$endpoints = @(
    "http://localhost:5000/api/weather/hello",
    "http://localhost:5000/api/docs/health"
)

Write-Host "Testing WITHOUT API Key (should fail):" -ForegroundColor Red
Write-Host "----------------------------------------" -ForegroundColor Red
try {
    $response = Invoke-RestMethod -Uri "http://localhost:5000/api/orders/hello" -Method Get
    Write-Host "UNEXPECTED: Request succeeded without API key" -ForegroundColor Red
} catch {
    Write-Host "EXPECTED: Request failed without API key - $($_.Exception.Message)" -ForegroundColor Green
}
Write-Host ""

Write-Host "Testing WITH Valid API Keys:" -ForegroundColor Green
Write-Host "-------------------------------" -ForegroundColor Green

foreach ($keyName in $apiKeys.Keys) {
    $apiKey = $apiKeys[$keyName]
    Write-Host "Testing with $keyName API Key:" -ForegroundColor Yellow
    
    foreach ($endpoint in $endpoints) {
        try {
            $headers = @{ "X-API-Key" = $apiKey }
            $response = Invoke-RestMethod -Uri $endpoint -Method Get -Headers $headers
            $serviceName = $response.service
            Write-Host "  SUCCESS: $serviceName - $($response.message)" -ForegroundColor Green
        } catch {
            Write-Host "  FAILED: $endpoint - $($_.Exception.Message)" -ForegroundColor Red
        }
    }
    Write-Host ""
}

Write-Host "Testing WITH Invalid API Key:" -ForegroundColor Red
Write-Host "--------------------------------" -ForegroundColor Red
try {
    $headers = @{ "X-API-Key" = "invalid-key-123" }
    $response = Invoke-RestMethod -Uri "http://localhost:5000/api/orders/hello" -Method Get -Headers $headers
    Write-Host "UNEXPECTED: Request succeeded with invalid API key" -ForegroundColor Red
} catch {
    Write-Host "EXPECTED: Request failed with invalid API key - $($_.Exception.Message)" -ForegroundColor Green
}
Write-Host ""

Write-Host "Testing Public Endpoints (no API key required):" -ForegroundColor Cyan
Write-Host "--------------------------------------------------" -ForegroundColor Cyan
try {
    $response = Invoke-RestMethod -Uri "http://localhost:5000/api/gateway/services" -Method Get
    Write-Host "Gateway Services: Found $($response.services.Count) services" -ForegroundColor Green
} catch {
    Write-Host "Failed to access gateway services: $($_.Exception.Message)" -ForegroundColor Red
}

try {
    $response = Invoke-RestMethod -Uri "http://localhost:5000/health" -Method Get
    Write-Host "Gateway Health: $($response.Status)" -ForegroundColor Green
} catch {
    Write-Host "Failed to access gateway health: $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

Write-Host "Generate More API Keys:" -ForegroundColor Magenta
Write-Host "--------------------------" -ForegroundColor Magenta
try {
    $response = Invoke-RestMethod -Uri "http://localhost:5007/seed/random/3" -Method Post
    Write-Host "$($response.message)" -ForegroundColor Green
} catch {
    Write-Host "Failed to generate API keys: $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

Write-Host "Testing Complete!" -ForegroundColor Cyan
Write-Host "===================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Access Documentation:" -ForegroundColor Yellow
Write-Host "- Scalar (Direct - No Auth): http://localhost:5002/scalar/all" -ForegroundColor White
Write-Host "- Scalar (Via Gateway - Requires API Key): http://localhost:5000/api/docs/scalar/all" -ForegroundColor White
Write-Host "- Helper HTML Page: scalar-with-api-key.html" -ForegroundColor White
Write-Host "- Identity Service: http://localhost:5007/swagger" -ForegroundColor White
Write-Host ""
Write-Host "Your API Keys:" -ForegroundColor Yellow
foreach ($keyName in $apiKeys.Keys) {
    Write-Host "- $keyName : $($apiKeys[$keyName])" -ForegroundColor White
}
