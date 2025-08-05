# Test Identity Service directly
$apiKey = "lLdcTPiY972evliSWCBCTNhS0O_WSg7NTaXYalpL4zA"
$body = @{
    ApiKey = $apiKey
    ServiceName = "WeatherService"
    Endpoint = "/weatherforecast"
} | ConvertTo-Json

Write-Host "Testing Identity Service directly..."
Write-Host "API Key: $apiKey"
Write-Host "Request Body: $body"

try {
    $response = Invoke-RestMethod -Uri "http://localhost:5007/validate" -Method Post -ContentType "application/json" -Body $body
    Write-Host "✅ Success! Response:"
    $response | ConvertTo-Json -Depth 3
} catch {
    Write-Host "❌ Error: $($_.Exception.Message)"
    Write-Host "Status Code: $($_.Exception.Response.StatusCode)"
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $responseBody = $reader.ReadToEnd()
        Write-Host "Response Body: $responseBody"
    }
}
