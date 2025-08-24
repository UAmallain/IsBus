# Test if the database lookups are working correctly

Write-Host "Testing Database Lookups" -ForegroundColor Cyan
Write-Host "========================" -ForegroundColor Cyan
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

# Test 1: Check if "Indian Mountain" exists as a street name
Write-Host "Test 1: Checking if 'Indian Mountain' exists in database..." -ForegroundColor Yellow
try {
    $result = Invoke-RestMethod -Uri "http://localhost:5000/api/StreetSearch/exists?name=Indian%20Mountain" -Method Get
    Write-Host "  Indian Mountain exists: $($result.exists)" -ForegroundColor White
} catch {
    Write-Host "  Error: $_" -ForegroundColor Red
}

Write-Host ""

# Test 2: Search for streets containing "Indian"
Write-Host "Test 2: Searching for streets containing 'Indian'..." -ForegroundColor Yellow
try {
    $result = Invoke-RestMethod -Uri "http://localhost:5000/api/StreetSearch/search?searchTerm=Indian`&maxResults=10" -Method Get
    Write-Host "  Found $($result.totalResults) streets containing 'Indian':" -ForegroundColor White
    $result.results | ForEach-Object {
        Write-Host "    - $($_.streetName) ($($_.streetType))" -ForegroundColor Gray
    }
} catch {
    Write-Host "  Error: $_" -ForegroundColor Red
}

Write-Host ""

# Test 3: Check database stats
Write-Host "Test 3: Checking database statistics..." -ForegroundColor Yellow
try {
    $result = Invoke-RestMethod -Uri "http://localhost:5000/api/StreetSearch/stats" -Method Get
    Write-Host "  Total streets in database: $($result.statistics.totalStreets)" -ForegroundColor White
    Write-Host "  Unique street names: $($result.statistics.uniqueStreetNames)" -ForegroundColor White
    Write-Host "  Unique communities: $($result.statistics.uniqueCommunities)" -ForegroundColor White
} catch {
    Write-Host "  Error: $_" -ForegroundColor Red
}

Write-Host ""

# Test 4: Check street type mappings
Write-Host "Test 4: Checking street type mappings..." -ForegroundColor Yellow
try {
    $result = Invoke-RestMethod -Uri "http://localhost:5000/api/StreetSearch/type-mappings" -Method Get
    Write-Host "  Total type mappings: $($result.totalMappings)" -ForegroundColor White
    Write-Host "  Sample mappings:" -ForegroundColor White
    $result.mappings | Select-Object -First 5 | ForEach-Object {
        Write-Host "    - $($_.abbreviation) = $($_.fullName)" -ForegroundColor Gray
    }
} catch {
    Write-Host "  Error: $_" -ForegroundColor Red
}

Write-Host ""

# Test 5: Test direct parser endpoint with debug info
Write-Host "Test 5: Testing parser with 'Atlantic Sunrooms Indian Mountain Road NB'..." -ForegroundColor Yellow
$testInput = @{
    input = "Atlantic Sunrooms Indian Mountain Road NB  506 863-3438"
} | ConvertTo-Json

try {
    $result = Invoke-RestMethod -Uri "http://localhost:5000/api/parser/parse" -Method Post -Body $testInput -ContentType "application/json"
    Write-Host "  Parser result:" -ForegroundColor White
    Write-Host "    Name: $($result.name)" -ForegroundColor Gray
    Write-Host "    Address: $($result.address)" -ForegroundColor Gray
    Write-Host "    Phone: $($result.phone)" -ForegroundColor Gray
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
Write-Host "If the database shows 0 or very few streets, you need to run:" -ForegroundColor Yellow
Write-Host "  python migrate_gpkg_to_mysql.py" -ForegroundColor White
Write-Host "  python setup_all_mappings.py" -ForegroundColor White
Write-Host ""
Write-Host "Press any key to exit..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")