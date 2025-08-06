# Test API key validation with new keys
$apiKey = "LGplFG5SbbcuGStQIBSlf2GGTStli3ZFdcGaMOhA4qM"  # admin_master

$body = @{
    ApiKey = $apiKey
    ServiceName = "WeatherService"
    Endpoint = "/weatherforecast"
} | ConvertTo-Json

Write-Host "Testing Identity Service validation endpoint..."
Write-Host "Body: $body"

try {
    $response = Invoke-RestMethod -Uri "http://localhost:5007/validate" -Method POST -ContentType "application/json" -Body $body
    Write-Host "Response: $($response | ConvertTo-Json -Depth 3)"
} catch {
    Write-Host "Error: $($_.Exception.Message)"
    Write-Host "Response: $($_.Exception.Response)"
}

Write-Host ""
Write-Host "Testing BFF Gateway with API key (should use gRPC now)..."

try {
    $headers = @{
        "X-API-Key" = $apiKey
    }
    $response = Invoke-RestMethod -Uri "http://localhost:5000/api/weather/weatherforecast" -Method GET -Headers $headers
    Write-Host "BFF Response: $($response | ConvertTo-Json -Depth 3)"
} catch {
    Write-Host "BFF Error: $($_.Exception.Message)"
}
