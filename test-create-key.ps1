# Test creating a new API key
$body = @{
    UserName = "test_user"
    Description = "Test API key"
    Permissions = @("read", "write")
    ExpiresInDays = 30
} | ConvertTo-Json

Write-Host "Creating new API key..."
Write-Host "Request Body: $body"

try {
    $response = Invoke-RestMethod -Uri "http://localhost:5007/api-keys" -Method Post -ContentType "application/json" -Body $body
    Write-Host "✅ Success! Response:"
    $response | ConvertTo-Json -Depth 3
    
    # Now test validation with the new key
    $newApiKey = $response.ApiKey
    Write-Host "`nTesting validation with new key: $newApiKey"
    
    $validateBody = @{
        ApiKey = $newApiKey
        ServiceName = "WeatherService"
        Endpoint = "/weatherforecast"
    } | ConvertTo-Json
    
    $validateResponse = Invoke-RestMethod -Uri "http://localhost:5007/validate" -Method Post -ContentType "application/json" -Body $validateBody
    Write-Host "✅ Validation Success! Response:"
    $validateResponse | ConvertTo-Json -Depth 3
    
} catch {
    Write-Host "❌ Error: $($_.Exception.Message)"
    Write-Host "Status Code: $($_.Exception.Response.StatusCode)"
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $responseBody = $reader.ReadToEnd()
        Write-Host "Response Body: $responseBody"
    }
}
