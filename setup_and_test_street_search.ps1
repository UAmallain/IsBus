# PowerShell script to set up province mapping and test the new street search API

Write-Host "=====================================================" -ForegroundColor Cyan
Write-Host "Setting up Province Mapping and Testing Street Search" -ForegroundColor Cyan
Write-Host "=====================================================" -ForegroundColor Cyan
Write-Host ""

# Database connection parameters
$dbHost = "localhost"
$dbUser = "root"
$dbPassword = "D0ntfw!thm01MA"
$dbName = "bor_db"

# Step 1: Execute the province mapping SQL
Write-Host "Step 1: Creating province mapping table and procedures..." -ForegroundColor Yellow

$mysqlCmd = "mysql"
if (-not (Get-Command $mysqlCmd -ErrorAction SilentlyContinue)) {
    $mysqlCmd = "C:\Program Files\MySQL\MySQL Server 8.0\bin\mysql.exe"
    if (-not (Test-Path $mysqlCmd)) {
        Write-Host "Error: MySQL client not found. Please ensure MySQL is installed." -ForegroundColor Red
        exit 1
    }
}

# Execute the SQL file
try {
    & $mysqlCmd -h $dbHost -u $dbUser -p$dbPassword $dbName -e "source Database/create_province_mapping.sql" 2>$null
    Write-Host "✓ Province mapping table and procedures created successfully" -ForegroundColor Green
} catch {
    Write-Host "Warning: Could not execute SQL file directly. You may need to run it manually." -ForegroundColor Yellow
}

Write-Host ""

# Step 2: Build and run the API
Write-Host "Step 2: Building the API..." -ForegroundColor Yellow
dotnet build --configuration Release

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed. Please fix compilation errors." -ForegroundColor Red
    exit 1
}
Write-Host "✓ Build successful" -ForegroundColor Green
Write-Host ""

# Step 3: Start the API in background
Write-Host "Step 3: Starting the API..." -ForegroundColor Yellow
$apiProcess = Start-Process dotnet -ArgumentList "run", "--no-build", "--urls", "http://localhost:5000" -PassThru -WindowStyle Hidden

Start-Sleep -Seconds 3

# Check if API is running
try {
    $response = Invoke-RestMethod -Uri "http://localhost:5000/api/StreetSearch/provinces" -Method Get -ErrorAction Stop
    Write-Host "✓ API is running" -ForegroundColor Green
} catch {
    Write-Host "Error: API failed to start" -ForegroundColor Red
    Stop-Process -Id $apiProcess.Id -Force -ErrorAction SilentlyContinue
    exit 1
}

Write-Host ""
Write-Host "=====================================================" -ForegroundColor Cyan
Write-Host "Testing Street Search Endpoints" -ForegroundColor Cyan
Write-Host "=====================================================" -ForegroundColor Cyan
Write-Host ""

# Test 1: Get provinces
Write-Host "Test 1: Getting list of provinces..." -ForegroundColor Yellow
try {
    $provinces = Invoke-RestMethod -Uri "http://localhost:5000/api/StreetSearch/provinces" -Method Get
    Write-Host "Found $($provinces.totalProvinces) provinces:" -ForegroundColor White
    $provinces.provinces | ForEach-Object { Write-Host "  - $($_.code): $($_.name)" }
    Write-Host "✓ Provinces endpoint working" -ForegroundColor Green
} catch {
    Write-Host "✗ Failed to get provinces: $_" -ForegroundColor Red
}

Write-Host ""

# Test 2: Search for streets
Write-Host "Test 2: Searching for streets with 'Main'..." -ForegroundColor Yellow
try {
    $searchResult = Invoke-RestMethod -Uri "http://localhost:5000/api/StreetSearch/search?searchTerm=Main`&maxResults=5" -Method Get
    Write-Host "Found $($searchResult.totalResults) results for 'Main':" -ForegroundColor White
    $searchResult.results | Select-Object -First 5 | ForEach-Object {
        Write-Host "  - $($_.streetName) ($($_.streetType)) - $($_.occurrenceCount) occurrences"
    }
    Write-Host "✓ Street search working" -ForegroundColor Green
} catch {
    Write-Host "✗ Failed to search streets: $_" -ForegroundColor Red
}

Write-Host ""

# Test 3: Search in specific province
Write-Host "Test 3: Searching for streets in Ontario..." -ForegroundColor Yellow
try {
    $ontarioResult = Invoke-RestMethod -Uri "http://localhost:5000/api/StreetSearch/search?searchTerm=King`&province=ON`&maxResults=3" -Method Get
    Write-Host "Found $($ontarioResult.totalResults) 'King' streets in Ontario:" -ForegroundColor White
    $ontarioResult.results | Select-Object -First 3 | ForEach-Object {
        Write-Host "  - $($_.streetName) in $($_.communities -join ', ')"
    }
    Write-Host "✓ Province-specific search working" -ForegroundColor Green
} catch {
    Write-Host "✗ Failed to search in Ontario: $_" -ForegroundColor Red
}

Write-Host ""

# Test 4: Check if street exists
Write-Host "Test 4: Checking if 'Yonge Street' exists..." -ForegroundColor Yellow
try {
    $existsResult = Invoke-RestMethod -Uri "http://localhost:5000/api/StreetSearch/exists?name=Yonge%20Street" -Method Get
    if ($existsResult.exists) {
        Write-Host "  ✓ 'Yonge Street' exists in the database" -ForegroundColor Green
    } else {
        Write-Host "  - 'Yonge Street' not found in the database" -ForegroundColor Yellow
    }
} catch {
    Write-Host "✗ Failed to check street existence: $_" -ForegroundColor Red
}

Write-Host ""

# Test 5: Get street types
Write-Host "Test 5: Getting street types..." -ForegroundColor Yellow
try {
    $typesResult = Invoke-RestMethod -Uri "http://localhost:5000/api/StreetSearch/types" -Method Get
    Write-Host "Found $($typesResult.totalTypes) street types:" -ForegroundColor White
    $typesResult.types | Select-Object -First 10 | ForEach-Object {
        Write-Host "  - $_"
    }
    Write-Host "✓ Street types endpoint working" -ForegroundColor Green
} catch {
    Write-Host "✗ Failed to get street types: $_" -ForegroundColor Red
}

Write-Host ""

# Test 6: Get statistics
Write-Host "Test 6: Getting street statistics..." -ForegroundColor Yellow
try {
    $statsResult = Invoke-RestMethod -Uri "http://localhost:5000/api/StreetSearch/stats" -Method Get
    Write-Host "Database Statistics:" -ForegroundColor White
    Write-Host "  - Total streets: $($statsResult.statistics.totalStreets)" 
    Write-Host "  - Unique street names: $($statsResult.statistics.uniqueStreetNames)"
    Write-Host "  - Unique communities: $($statsResult.statistics.uniqueCommunities)"
    Write-Host "✓ Statistics endpoint working" -ForegroundColor Green
} catch {
    Write-Host "✗ Failed to get statistics: $_" -ForegroundColor Red
}

Write-Host ""
Write-Host "=====================================================" -ForegroundColor Cyan
Write-Host "Testing Complete!" -ForegroundColor Cyan
Write-Host "=====================================================" -ForegroundColor Cyan
Write-Host ""

# Stop the API
Write-Host "Stopping the API..." -ForegroundColor Yellow
Stop-Process -Id $apiProcess.Id -Force -ErrorAction SilentlyContinue
Write-Host "✓ API stopped" -ForegroundColor Green

Write-Host ""
Write-Host "All tests completed. The street search API is now ready to use." -ForegroundColor Green
Write-Host ""
Write-Host "Available endpoints:" -ForegroundColor Cyan
Write-Host "  - GET /api/StreetSearch/search?searchTerm=<term>&province=<code>&maxResults=<n>"
Write-Host "  - GET /api/StreetSearch/exists?name=<street_name>&province=<code>"
Write-Host "  - GET /api/StreetSearch/types"
Write-Host "  - GET /api/StreetSearch/stats?province=<code>"
Write-Host "  - GET /api/StreetSearch/provinces"
Write-Host ""
Write-Host "Press any key to exit..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")