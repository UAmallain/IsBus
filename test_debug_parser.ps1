# Debug test for the parser to see what's happening with database lookups

Write-Host "Debug Parser Test" -ForegroundColor Cyan
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

# Test 1: Check if "Company Mountain" exists as a street
Write-Host "Test 1: Checking if 'Company Mountain' exists in database..." -ForegroundColor Yellow
try {
    $result = Invoke-RestMethod -Uri "http://localhost:5000/api/StreetSearch/exists?name=Company%20Mountain" -Method Get
    Write-Host "  'Company Mountain' exists: $($result.exists)" -ForegroundColor White
    
    if ($result.exists) {
        Write-Host "  WARNING: This street should NOT exist!" -ForegroundColor Red
    }
} catch {
    Write-Host "  Error: $_" -ForegroundColor Red
}

Write-Host ""

# Test 2: Check if "Mountain" exists as a street
Write-Host "Test 2: Checking if 'Mountain' exists in database..." -ForegroundColor Yellow
try {
    $result = Invoke-RestMethod -Uri "http://localhost:5000/api/StreetSearch/exists?name=Mountain" -Method Get
    Write-Host "  'Mountain' exists: $($result.exists)" -ForegroundColor White
} catch {
    Write-Host "  Error: $_" -ForegroundColor Red
}

Write-Host ""

# Test 3: Search for streets containing "Company"
Write-Host "Test 3: Searching for streets containing 'Company'..." -ForegroundColor Yellow
try {
    $result = Invoke-RestMethod -Uri "http://localhost:5000/api/StreetSearch/search?searchTerm=Company`&maxResults=10" -Method Get
    Write-Host "  Found $($result.totalResults) streets containing 'Company':" -ForegroundColor White
    if ($result.totalResults -gt 0) {
        $result.results | ForEach-Object {
            Write-Host "    - $($_.streetName) ($($_.streetType))" -ForegroundColor Gray
        }
    }
} catch {
    Write-Host "  Error: $_" -ForegroundColor Red
}

Write-Host ""

# Test 4: Parse the problematic string
Write-Host "Test 4: Parsing 'ABC Company Mountain Road ON 416 555-1234'..." -ForegroundColor Yellow
$testInput = @{
    input = "ABC Company Mountain Road ON 416 555-1234"
} | ConvertTo-Json

try {
    $result = Invoke-RestMethod -Uri "http://localhost:5000/api/parser/parse" -Method Post -Body $testInput -ContentType "application/json"
    Write-Host "  Parser result:" -ForegroundColor White
    Write-Host "    Name: '$($result.name)'" -ForegroundColor Gray
    Write-Host "    Address: '$($result.address)'" -ForegroundColor Gray
    Write-Host "    Phone: '$($result.phone)'" -ForegroundColor Gray
    
    if ($result.name -ne "ABC Company") {
        Write-Host "  ERROR: Name should be 'ABC Company' not '$($result.name)'" -ForegroundColor Red
    }
    if ($result.address -ne "Mountain Road ON") {
        Write-Host "  ERROR: Address should be 'Mountain Road ON' not '$($result.address)'" -ForegroundColor Red
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