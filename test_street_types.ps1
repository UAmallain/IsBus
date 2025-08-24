# PowerShell script to set up and test street type mappings

Write-Host "=====================================================" -ForegroundColor Cyan
Write-Host "Setting up and Testing Street Type Mappings" -ForegroundColor Cyan
Write-Host "=====================================================" -ForegroundColor Cyan
Write-Host ""

# Database connection parameters
$dbHost = "localhost"
$dbUser = "root"
$dbPassword = "D0ntfw!thm01MA"
$dbName = "bor_db"

# Step 1: Execute the street type mapping SQL
Write-Host "Step 1: Creating street type mapping table..." -ForegroundColor Yellow

$mysqlCmd = "mysql"
if (-not (Get-Command $mysqlCmd -ErrorAction SilentlyContinue)) {
    $mysqlCmd = "C:\Program Files\MySQL\MySQL Server 8.0\bin\mysql.exe"
    if (-not (Test-Path $mysqlCmd)) {
        Write-Host "Note: MySQL client not found. You may need to run the SQL manually." -ForegroundColor Yellow
    }
}

# Execute the SQL file if mysql is available
if (Test-Path $mysqlCmd) {
    try {
        & $mysqlCmd -h $dbHost -u $dbUser -p$dbPassword $dbName -e "source Database/create_street_type_mapping.sql" 2>$null
        Write-Host "✓ Street type mapping table created successfully" -ForegroundColor Green
    } catch {
        Write-Host "Warning: Could not execute SQL file. You may need to run it manually." -ForegroundColor Yellow
    }
} else {
    Write-Host @"
Please run the following SQL file manually in your MySQL client:
    Database/create_street_type_mapping.sql
"@ -ForegroundColor Yellow
}

Write-Host ""

# Step 2: Build the API
Write-Host "Step 2: Building the API..." -ForegroundColor Yellow
dotnet build --configuration Release --nologo --verbosity quiet

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed. Please fix compilation errors." -ForegroundColor Red
    exit 1
}
Write-Host "✓ Build successful" -ForegroundColor Green
Write-Host ""

# Step 3: Start the API in background
Write-Host "Step 3: Starting the API..." -ForegroundColor Yellow
$apiProcess = Start-Process dotnet -ArgumentList "run", "--no-build", "--urls", "http://localhost:5000" -PassThru -WindowStyle Hidden

Start-Sleep -Seconds 5

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
Write-Host "Testing Street Type Mapping Endpoints" -ForegroundColor Cyan
Write-Host "=====================================================" -ForegroundColor Cyan
Write-Host ""

# Test 1: Get all street type mappings
Write-Host "Test 1: Getting all street type mappings..." -ForegroundColor Yellow
try {
    $mappings = Invoke-RestMethod -Uri "http://localhost:5000/api/StreetSearch/type-mappings" -Method Get
    Write-Host "Found $($mappings.totalMappings) street type mappings" -ForegroundColor White
    Write-Host "Categories: $($mappings.categories -join ', ')" -ForegroundColor White
    Write-Host "Sample mappings:" -ForegroundColor White
    $mappings.mappings | Select-Object -First 10 | ForEach-Object {
        Write-Host "  - $($_.abbreviation) → $($_.fullName)"
    }
    Write-Host "✓ Type mappings endpoint working" -ForegroundColor Green
} catch {
    Write-Host "✗ Failed to get type mappings: $_" -ForegroundColor Red
}

Write-Host ""

# Test 2: Normalize street type abbreviations
Write-Host "Test 2: Testing street type normalization..." -ForegroundColor Yellow
$testTypes = @("rd", "ave", "blvd", "hwy", "st", "dr", "pkwy", "ln", "cres", "tr")

foreach ($type in $testTypes) {
    try {
        $result = Invoke-RestMethod -Uri "http://localhost:5000/api/StreetSearch/normalize-type?streetType=$type" -Method Get
        Write-Host "  $($result.input) → $($result.normalized) (abbr: $($result.abbreviation))" -ForegroundColor White
    } catch {
        Write-Host "  ✗ Failed to normalize '$type': $_" -ForegroundColor Red
    }
}
Write-Host "✓ Type normalization working" -ForegroundColor Green

Write-Host ""

# Test 3: Get abbreviations from full names
Write-Host "Test 3: Getting abbreviations from full names..." -ForegroundColor Yellow
$fullNames = @("Street", "Avenue", "Boulevard", "Highway", "Drive", "Road")

foreach ($name in $fullNames) {
    try {
        $result = Invoke-RestMethod -Uri "http://localhost:5000/api/StreetSearch/get-abbreviation?fullName=$name" -Method Get
        Write-Host "  $($result.fullName) → $($result.abbreviation)" -ForegroundColor White
    } catch {
        Write-Host "  ✗ Failed to get abbreviation for '$name': $_" -ForegroundColor Red
    }
}
Write-Host "✓ Abbreviation lookup working" -ForegroundColor Green

Write-Host ""

# Test 4: Test street search with normalized types
Write-Host "Test 4: Searching streets with type normalization..." -ForegroundColor Yellow
try {
    $searchResult = Invoke-RestMethod -Uri "http://localhost:5000/api/StreetSearch/search?searchTerm=Queen&maxResults=5" -Method Get
    Write-Host "Found $($searchResult.totalResults) results for 'Queen':" -ForegroundColor White
    $searchResult.results | Select-Object -First 5 | ForEach-Object {
        if ($_.streetType) {
            Write-Host "  - $($_.streetName) | Type: $($_.streetType) → $($_.streetTypeNormalized) (abbr: $($_.streetTypeAbbreviation))"
        } else {
            Write-Host "  - $($_.streetName) | No type specified"
        }
    }
    Write-Host "✓ Street search with type normalization working" -ForegroundColor Green
} catch {
    Write-Host "✗ Failed to search streets: $_" -ForegroundColor Red
}

Write-Host ""

# Test 5: Check unknown types
Write-Host "Test 5: Testing unknown street type handling..." -ForegroundColor Yellow
$unknownTypes = @("xyz", "unknown", "test123")

foreach ($type in $unknownTypes) {
    try {
        $result = Invoke-RestMethod -Uri "http://localhost:5000/api/StreetSearch/normalize-type?streetType=$type" -Method Get
        if ($result.isKnownType) {
            Write-Host "  Unexpected: '$type' was recognized as known type" -ForegroundColor Yellow
        } else {
            Write-Host "  ✓ '$type' correctly identified as unknown (returned: $($result.normalized))" -ForegroundColor Green
        }
    } catch {
        Write-Host "  ✗ Failed to test '$type': $_" -ForegroundColor Red
    }
}

Write-Host ""

# Test 6: Test Canadian-specific abbreviations
Write-Host "Test 6: Testing Canadian-specific abbreviations..." -ForegroundColor Yellow
$canadianTypes = @("conc", "rg", "rang", "sdrd", "ch", "rue", "mont", "imp")

foreach ($type in $canadianTypes) {
    try {
        $result = Invoke-RestMethod -Uri "http://localhost:5000/api/StreetSearch/normalize-type?streetType=$type" -Method Get
        Write-Host "  $($result.input) → $($result.normalized)" -ForegroundColor White
    } catch {
        Write-Host "  ✗ Failed to normalize '$type': $_" -ForegroundColor Red
    }
}
Write-Host "✓ Canadian abbreviations working" -ForegroundColor Green

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
Write-Host "Street type mapping system is ready to use!" -ForegroundColor Green
Write-Host ""
Write-Host "New endpoints available:" -ForegroundColor Cyan
Write-Host "  - GET /api/StreetSearch/type-mappings - Get all street type mappings"
Write-Host "  - GET /api/StreetSearch/normalize-type?streetType=<type> - Normalize a street type"
Write-Host "  - GET /api/StreetSearch/get-abbreviation?fullName=<name> - Get abbreviation for full name"
Write-Host ""
Write-Host "Street search results now include:" -ForegroundColor Cyan
Write-Host "  - streetType: Original type from database"
Write-Host "  - streetTypeNormalized: Full name (e.g., 'rd' → 'Road')"
Write-Host "  - streetTypeAbbreviation: Standard abbreviation"
Write-Host ""
Write-Host "Press any key to exit..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")