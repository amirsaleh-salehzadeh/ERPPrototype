# Test API Key Validation
$apiKey = "qbbHXCeMb6egGQ0n-Kwt94hGq9zOyQFv0kpjpuXsa0g"
$headers = @{
    "X-API-Key" = $apiKey
    "Content-Type" = "application/json"
}

Write-Host "Testing BFF Gateway with API key validation..."
Write-Host "API Key: $apiKey"

try {
    $response = Invoke-RestMethod -Uri "http://localhost:5000/api/weather/weatherforecast" -Method Get -Headers $headers
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
