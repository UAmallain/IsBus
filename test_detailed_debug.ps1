# Detailed debug test for parser

Write-Host "Detailed Parser Debug" -ForegroundColor Cyan
Write-Host "=====================" -ForegroundColor Cyan
Write-Host ""

# Start the API with debug logging
Write-Host "Starting the API with debug logging..." -ForegroundColor Yellow
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:Logging__LogLevel__Default = "Debug"
$apiProcess = Start-Process dotnet -ArgumentList "run", "--no-build", "--urls", "http://localhost:5000" -PassThru -WindowStyle Hidden

Start-Sleep -Seconds 5

# Check if API is running
try {
    $response = Invoke-RestMethod -Uri "http://localhost:5000/api/StreetSearch/provinces" -Method Get -ErrorAction Stop
    Write-Host "API is running" -ForegroundColor Green
} catch {
    Write-Host "Error: API failed to start" -ForegroundColor Red
    if ($apiProcess) {
        Stop-Process -Id $apiProcess.Id -Force -ErrorAction SilentlyContinue
    }
    exit 1
}

Write-Host ""
Write-Host "Checking what the database knows about these streets..." -ForegroundColor Yellow
Write-Host ""

# Check each potential street name combination
$streetChecks = @(
    "Company",
    "Company Mountain", 
    "Mountain",
    "ABC",
    "ABC Company",
    "ABC Company Mountain"
)

foreach ($street in $streetChecks) {
    # URL encode the street name
    $encoded = [uri]::EscapeDataString($street)
    try {
        $result = Invoke-RestMethod -Uri "http://localhost:5000/api/StreetSearch/exists?name=$encoded" -Method Get
        if ($result.exists) {
            Write-Host "  '$street' EXISTS in database" -ForegroundColor Green
        } else {
            Write-Host "  '$street' NOT in database" -ForegroundColor Gray
        }
    } catch {
        Write-Host "  Error checking '$street': $_" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "Now testing the parser..." -ForegroundColor Yellow
Write-Host ""

# Test the problematic input
$testInput = @{
    input = "ABC Company Mountain Road ON 416 555-1234"
} | ConvertTo-Json

Write-Host "Input: 'ABC Company Mountain Road ON 416 555-1234'" -ForegroundColor White
Write-Host ""

try {
    $result = Invoke-RestMethod -Uri "http://localhost:5000/api/parser/parse" -Method Post -Body $testInput -ContentType "application/json"
    
    Write-Host "Parser Result:" -ForegroundColor Yellow
    Write-Host "  Name: '$($result.name)'" -ForegroundColor $(if ($result.name -eq "ABC Company") { "Green" } else { "Red" })
    Write-Host "  Address: '$($result.address)'" -ForegroundColor $(if ($result.address -eq "Mountain Road ON") { "Green" } else { "Red" })
    Write-Host "  Phone: '$($result.phone)'" -ForegroundColor $(if ($result.phone -eq "416 555-1234") { "Green" } else { "Red" })
    Write-Host ""
    
    if ($result.name -ne "ABC Company" -or $result.address -ne "Mountain Road ON") {
        Write-Host "PARSING ERROR:" -ForegroundColor Red
        Write-Host "  Expected Name: 'ABC Company'" -ForegroundColor Gray
        Write-Host "  Expected Address: 'Mountain Road ON'" -ForegroundColor Gray
        Write-Host ""
        Write-Host "The parser is not splitting correctly!" -ForegroundColor Red
    } else {
        Write-Host "✓ Parser is working correctly!" -ForegroundColor Green
    }
} catch {
    Write-Host "  Error: $_" -ForegroundColor Red
}

Write-Host ""

# Stop the API
Write-Host "Stopping the API..." -ForegroundColor Yellow
if ($apiProcess) {
    Stop-Process -Id $apiProcess.Id -Force -ErrorAction SilentlyContinue
}
Write-Host "API stopped" -ForegroundColor Green

Write-Host ""
Write-Host "Press any key to exit..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")