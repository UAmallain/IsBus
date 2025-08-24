# Clean debug test for parser

Write-Host "Parser Debug Test" -ForegroundColor Cyan
Write-Host "=================" -ForegroundColor Cyan
Write-Host ""

# Start the API
Write-Host "Starting the API..." -ForegroundColor Yellow
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
Write-Host "Checking database for street names..." -ForegroundColor Yellow
Write-Host ""

# Check each potential street name
$streetChecks = @(
    "Company",
    "Company Mountain", 
    "Mountain",
    "ABC",
    "ABC Company",
    "ABC Company Mountain"
)

foreach ($street in $streetChecks) {
    $encoded = [uri]::EscapeDataString($street)
    try {
        $result = Invoke-RestMethod -Uri "http://localhost:5000/api/StreetSearch/exists?name=$encoded" -Method Get
        if ($result.exists) {
            Write-Host "  [EXISTS] '$street'" -ForegroundColor Green
        } else {
            Write-Host "  [NOT IN DB] '$street'" -ForegroundColor Gray
        }
    } catch {
        Write-Host "  [ERROR] '$street': $_" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "Testing parser with: 'ABC Company Mountain Road ON 416 555-1234'" -ForegroundColor Yellow
Write-Host ""

$testInput = @{
    input = "ABC Company Mountain Road ON 416 555-1234"
} | ConvertTo-Json

try {
    $result = Invoke-RestMethod -Uri "http://localhost:5000/api/parser/parse" -Method Post -Body $testInput -ContentType "application/json"
    
    Write-Host "Result:" -ForegroundColor White
    Write-Host "  Name: '$($result.name)'" -ForegroundColor White
    Write-Host "  Address: '$($result.address)'" -ForegroundColor White
    Write-Host "  Phone: '$($result.phone)'" -ForegroundColor White
    Write-Host ""
    
    # Check if correct
    $nameCorrect = $result.name -eq "ABC Company"
    $addressCorrect = $result.address -eq "Mountain Road ON"
    $phoneCorrect = $result.phone -eq "416 555-1234"
    
    if ($nameCorrect -and $addressCorrect -and $phoneCorrect) {
        Write-Host "PASS - Parser working correctly!" -ForegroundColor Green
    } else {
        Write-Host "FAIL - Parser not splitting correctly!" -ForegroundColor Red
        if (-not $nameCorrect) {
            Write-Host "  Expected Name: 'ABC Company'" -ForegroundColor Yellow
        }
        if (-not $addressCorrect) {
            Write-Host "  Expected Address: 'Mountain Road ON'" -ForegroundColor Yellow
        }
    }
} catch {
    Write-Host "Error calling parser: $_" -ForegroundColor Red
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