# PowerShell script to add CORS to all microservices
Write-Host "Adding CORS configuration to all microservices..." -ForegroundColor Green

$services = @(
    "src\Services\ERP.InventoryService\Program.cs",
    "src\Services\ERP.CustomerService\Program.cs", 
    "src\Services\ERP.FinanceService\Program.cs"
)

foreach ($service in $services) {
    Write-Host "Processing $service..." -ForegroundColor Yellow
    
    if (Test-Path $service) {
        $content = Get-Content $service -Raw
        
        # Add CORS service registration
        if ($content -notmatch "AddCors") {
            $content = $content -replace "(builder\.Services\.AddLogging\(\);)", "`$1`r`n`r`n// Add CORS for Documentation service`r`nbuilder.Services.AddCors(options =>`r`n{`r`n    options.AddPolicy(`"AllowDocumentation`", policy =>`r`n    {`r`n        policy.WithOrigins(`"http://localhost:5002`", `"http://localhost:5000`")`r`n              .AllowAnyHeader()`r`n              .AllowAnyMethod();`r`n    });`r`n});"
        }
        
        # Add CORS middleware
        if ($content -notmatch "UseCors") {
            $content = $content -replace "(app\.UseHttpsRedirection\(\);)", "`$1`r`n`r`n// Enable CORS`r`napp.UseCors(`"AllowDocumentation`");"
        }
        
        Set-Content $service -Value $content -NoNewline
        Write-Host "✅ Updated $service" -ForegroundColor Green
    } else {
        Write-Host "❌ File not found: $service" -ForegroundColor Red
    }
}

Write-Host "CORS configuration completed!" -ForegroundColor Green
