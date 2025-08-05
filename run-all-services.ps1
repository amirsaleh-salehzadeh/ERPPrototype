#!/usr/bin/env pwsh
#
# ERP Prototype - Start All Services
# This script starts all microservices for the ERP Prototype system running on .NET 10
#

param(
    [switch]$NoBrowser,
    [switch]$Quiet,
    [switch]$Stop
)

# Configuration
$services = @(
    @{
        Name = "ERP Identity Service"
        Path = "src/Services/ERP.IdentityService"
        Port = 5007
        Color = "Magenta"
        SwaggerUrl = "http://localhost:5007/swagger"
    },
    @{
        Name = "Weather Service"
        Path = "src/Services/Playground.WeatherService"
        Port = 5001
        Color = "Yellow"
        SwaggerUrl = "http://localhost:5001/swagger"
    },
    @{
        Name = "BFF Gateway"
        Path = "src/Gateway/BFF.Gateway"
        Port = 5000
        Color = "Green"
        SwaggerUrl = "http://localhost:5000/swagger"
    },
    @{
        Name = "Documentation Service"
        Path = "src/Documentation/Scalar.Documentation"
        Port = 5002
        Color = "Cyan"
        SwaggerUrl = "http://localhost:5002"
    }
)

# Function to display colored output
function Write-ColoredOutput {
    param([string]$Message, [string]$Color = "White")
    Write-Host $Message -ForegroundColor $Color
}

# Function to check if a port is in use
function Test-Port {
    param([int]$Port)
    try {
        $null = Test-NetConnection -ComputerName "localhost" -Port $Port -InformationLevel Quiet -WarningAction SilentlyContinue
        return $true
    }
    catch {
        return $false
    }
}

# Function to stop services
function Stop-Services {
    Write-ColoredOutput "`n🛑 Stopping ERP Prototype Services..." "Red"
    
    foreach ($service in $services) {
        $port = $service.Port
        Write-ColoredOutput "Stopping $($service.Name) on port $port..." "Yellow"
        
        # Find and kill processes using the port
        $processes = Get-NetTCPConnection -LocalPort $port -ErrorAction SilentlyContinue | 
                    Select-Object -ExpandProperty OwningProcess | 
                    ForEach-Object { Get-Process -Id $_ -ErrorAction SilentlyContinue }
        
        foreach ($process in $processes) {
            try {
                Stop-Process -Id $process.Id -Force
                Write-ColoredOutput "  ✅ Stopped process $($process.Name) (PID: $($process.Id))" "Green"
            }
            catch {
                Write-ColoredOutput "  ❌ Failed to stop process $($process.Name) (PID: $($process.Id))" "Red"
            }
        }
    }
    
    Write-ColoredOutput "`n✅ All services stopped." "Green"
    exit 0
}

# Handle stop parameter
if ($Stop) {
    Stop-Services
}

# Check if we're in the correct directory
if (-not (Test-Path "ERPPrototype.sln")) {
    Write-ColoredOutput "❌ Error: Please run this script from the ERPPrototype root directory" "Red"
    exit 1
}

# Display header
if (-not $Quiet) {
    Write-ColoredOutput "`n🚀 ERP Prototype - .NET 10 Service Launcher" "Cyan"
    Write-ColoredOutput "═══════════════════════════════════════════════" "Cyan"
    Write-ColoredOutput "Starting all microservices..." "White"
}

# Check for port conflicts
$portConflicts = @()
foreach ($service in $services) {
    if (Test-Port -Port $service.Port) {
        $portConflicts += "Port $($service.Port) is already in use ($($service.Name))"
    }
}

if ($portConflicts.Count -gt 0) {
    Write-ColoredOutput "`n⚠️  Port Conflicts Detected:" "Yellow"
    foreach ($conflict in $portConflicts) {
        Write-ColoredOutput "   $conflict" "Yellow"
    }
    Write-ColoredOutput "`nWould you like to stop existing services and continue? (y/N)" "Yellow"
    $response = Read-Host
    if ($response -eq "y" -or $response -eq "Y") {
        Stop-Services
        Start-Sleep -Seconds 2
    } else {
        Write-ColoredOutput "❌ Aborted due to port conflicts" "Red"
        exit 1
    }
}

# Build the solution first
if (-not $Quiet) {
    Write-ColoredOutput "`n🔨 Building solution..." "Cyan"
}

$buildResult = dotnet build --configuration Release --verbosity quiet
if ($LASTEXITCODE -ne 0) {
    Write-ColoredOutput "❌ Build failed. Please fix build errors and try again." "Red"
    exit 1
}

if (-not $Quiet) {
    Write-ColoredOutput "✅ Build successful" "Green"
}

# Start each service in its own PowerShell window
$startedServices = @()

foreach ($service in $services) {
    if (-not $Quiet) {
        Write-ColoredOutput "`n🚀 Starting $($service.Name)..." $service.Color
        Write-ColoredOutput "   Path: $($service.Path)" "Gray"
        Write-ColoredOutput "   Port: $($service.Port)" "Gray"
    }
    
    try {
        $windowTitle = "ERP Prototype - $($service.Name)"
        $command = "cd '$($service.Path)'; dotnet run --configuration Release --urls 'http://localhost:$($service.Port)'"
        
        $processInfo = Start-Process powershell -ArgumentList @(
            "-NoExit",
            "-Command",
            "& { `$Host.UI.RawUI.WindowTitle = '$windowTitle'; Write-Host 'Starting $($service.Name)...' -ForegroundColor $($service.Color); $command }"
        ) -PassThru
        
        $startedServices += @{
            Service = $service
            Process = $processInfo
        }
        
        if (-not $Quiet) {
            Write-ColoredOutput "   ✅ Started with PID $($processInfo.Id)" "Green"
        }
    }
    catch {
        Write-ColoredOutput "   ❌ Failed to start: $($_.Exception.Message)" "Red"
    }
    
    # Small delay between service starts
    Start-Sleep -Seconds 1
}

# Wait for services to start up
if (-not $Quiet) {
    Write-ColoredOutput "`n⏳ Waiting for services to start up..." "Cyan"
}

Start-Sleep -Seconds 10

# Check service health
$healthyServices = @()
$unhealthyServices = @()

foreach ($service in $services) {
    if (Test-Port -Port $service.Port) {
        $healthyServices += $service
        if (-not $Quiet) {
            Write-ColoredOutput "   ✅ $($service.Name) - Running on port $($service.Port)" $service.Color
        }
    } else {
        $unhealthyServices += $service
        if (-not $Quiet) {
            Write-ColoredOutput "   ❌ $($service.Name) - Not responding on port $($service.Port)" "Red"
        }
    }
}

# Display results
if (-not $Quiet) {
    Write-ColoredOutput "`n📊 Service Status Summary:" "Cyan"
    Write-ColoredOutput "═══════════════════════════" "Cyan"
    Write-ColoredOutput "✅ Healthy Services: $($healthyServices.Count)/$($services.Count)" "Green"
    
    if ($unhealthyServices.Count -gt 0) {
        Write-ColoredOutput "❌ Unhealthy Services: $($unhealthyServices.Count)" "Red"
    }
    
    Write-ColoredOutput "`n🌐 Available Endpoints:" "Cyan"
    foreach ($service in $healthyServices) {
        Write-ColoredOutput "   • $($service.Name): $($service.SwaggerUrl)" $service.Color
    }
    
    Write-ColoredOutput "`n🎯 Quick Links:" "Yellow"
    Write-ColoredOutput "   • Main Gateway: http://localhost:5000/swagger" "White"
    Write-ColoredOutput "   • Documentation: http://localhost:5002" "White"
    Write-ColoredOutput "   • Weather API: http://localhost:5001/swagger" "White"
    Write-ColoredOutput "   • Identity API: http://localhost:5007/swagger" "White"
    
    Write-ColoredOutput "`n💡 Tips:" "Yellow"
    Write-ColoredOutput "   • Each service runs in its own window" "Gray"
    Write-ColoredOutput "   • Close service windows or use Ctrl+C to stop individual services" "Gray"
    Write-ColoredOutput "   • Run './run-all-services.ps1 -Stop' to stop all services" "Gray"
    Write-ColoredOutput "   • Use './run-all-services.ps1 -Quiet' for minimal output" "Gray"
}

# Open browser to main endpoint (unless disabled)
if (-not $NoBrowser -and $healthyServices.Count -gt 0) {
    if (-not $Quiet) {
        Write-ColoredOutput "`n🌐 Opening browser to main gateway..." "Cyan"
    }
    Start-Sleep -Seconds 2
    Start-Process "http://localhost:5000/swagger"
}

if (-not $Quiet) {
    Write-ColoredOutput "`n🎉 ERP Prototype services are now running!" "Green"
    Write-ColoredOutput "Press any key to continue or Ctrl+C to exit..." "Gray"
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
}
