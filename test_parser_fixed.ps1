# Test script for improved string parsing with better street name matching

Write-Host "=====================================================" -ForegroundColor Cyan
Write-Host "Testing Improved String Parser" -ForegroundColor Cyan
Write-Host "=====================================================" -ForegroundColor Cyan
Write-Host ""

# Build the project first
Write-Host "Building the project..." -ForegroundColor Yellow
dotnet build --configuration Release --nologo --verbosity quiet

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed. Please fix compilation errors." -ForegroundColor Red
    exit 1
}
Write-Host "Build successful" -ForegroundColor Green
Write-Host ""

# Start the API
Write-Host "Starting the API..." -ForegroundColor Yellow
$apiProcess = Start-Process dotnet -ArgumentList "run", "--no-build", "--urls", "http://localhost:5000" -PassThru -WindowStyle Hidden

Start-Sleep -Seconds 5

# Check if API is running
$testBody = @{input = "test 506 123-4567"} | ConvertTo-Json
try {
    $response = Invoke-RestMethod -Uri "http://localhost:5000/api/parser/parse" -Method Post -Body $testBody -ContentType "application/json" -ErrorAction Stop
    Write-Host "API is running" -ForegroundColor Green
} catch {
    Write-Host "Error: API failed to start" -ForegroundColor Red
    if ($apiProcess) {
        Stop-Process -Id $apiProcess.Id -Force -ErrorAction SilentlyContinue
    }
    exit 1
}

Write-Host ""
Write-Host "=====================================================" -ForegroundColor Cyan
Write-Host "Testing Problem Cases" -ForegroundColor Cyan
Write-Host "=====================================================" -ForegroundColor Cyan
Write-Host ""

# Test cases
$testCases = @()

$testCases += @{
    Input = "Atlantic Sunrooms Indian Mountain Road NB  506 863-3438"
    ExpectedName = "Atlantic Sunrooms"
    ExpectedAddress = "Indian Mountain Road NB"
    ExpectedPhone = "506 863-3438"
    Description = "Multi-word street name (Indian Mountain Road)"
}

$testCases += @{
    Input = "Zukewich T 223 1057 Beaverhill Blvd 257-7365"
    ExpectedName = "Zukewich T"
    ExpectedAddress = "223 1057 Beaverhill Blvd"
    ExpectedPhone = "257-7365"
    Description = "Unit number + civic number"
}

$testCases += @{
    Input = "ABC Company Mountain Road ON 416 555-1234"
    ExpectedName = "ABC Company"
    ExpectedAddress = "Mountain Road ON"
    ExpectedPhone = "416 555-1234"
    Description = "Single-word street name (Mountain Road)"
}

$testCases += @{
    Input = "John Smith 45 678 Main Street 555-1234"
    ExpectedName = "John Smith"
    ExpectedAddress = "45 678 Main Street"
    ExpectedPhone = "555-1234"
    Description = "Unit + civic with common street"
}

$testCases += @{
    Input = "Pacific Industries 5000 King George Highway 604 555-9999"
    ExpectedName = "Pacific Industries"
    ExpectedAddress = "5000 King George Highway"
    ExpectedPhone = "604 555-9999"
    Description = "Multi-word street with civic number"
}

$testCases += @{
    Input = "Quality Services Indian River Road PE 902 123-4567"
    ExpectedName = "Quality Services"
    ExpectedAddress = "Indian River Road PE"
    ExpectedPhone = "902 123-4567"
    Description = "Another multi-word street name"
}

$passCount = 0
$failCount = 0

foreach ($test in $testCases) {
    Write-Host "Test: $($test.Description)" -ForegroundColor Yellow
    Write-Host "  Input: $($test.Input)" -ForegroundColor Gray
    
    $body = @{input = $test.Input} | ConvertTo-Json
    
    try {
        $result = Invoke-RestMethod -Uri "http://localhost:5000/api/parser/parse" -Method Post -Body $body -ContentType "application/json"
        
        if ($result.success) {
            $nameMatch = $result.name -eq $test.ExpectedName
            $addressMatch = $result.address -eq $test.ExpectedAddress
            $phoneMatch = $result.phone -eq $test.ExpectedPhone
            
            if ($nameMatch -and $addressMatch -and $phoneMatch) {
                Write-Host "  PASS" -ForegroundColor Green
                $passCount++
            } else {
                Write-Host "  FAIL" -ForegroundColor Red
                $failCount++
                
                if (-not $nameMatch) {
                    Write-Host "    Name: Expected '$($test.ExpectedName)' but got '$($result.name)'" -ForegroundColor Red
                }
                if (-not $addressMatch) {
                    Write-Host "    Address: Expected '$($test.ExpectedAddress)' but got '$($result.address)'" -ForegroundColor Red
                }
                if (-not $phoneMatch) {
                    Write-Host "    Phone: Expected '$($test.ExpectedPhone)' but got '$($result.phone)'" -ForegroundColor Red
                }
            }
            
            Write-Host "  Result:" -ForegroundColor Gray
            Write-Host "    Name: $($result.name)" -ForegroundColor Gray
            Write-Host "    Address: $($result.address)" -ForegroundColor Gray
            Write-Host "    Phone: $($result.phone)" -ForegroundColor Gray
            Write-Host "    Business: $($result.isBusinessName), Residential: $($result.isResidentialName)" -ForegroundColor Gray
        } else {
            Write-Host "  FAIL - Parsing failed: $($result.errorMessage)" -ForegroundColor Red
            $failCount++
        }
    } catch {
        Write-Host "  FAIL - API error: $_" -ForegroundColor Red
        $failCount++
    }
    
    Write-Host ""
}

Write-Host "=====================================================" -ForegroundColor Cyan
Write-Host "Test Results Summary" -ForegroundColor Cyan
Write-Host "=====================================================" -ForegroundColor Cyan
Write-Host "  Passed: $passCount" -ForegroundColor White
Write-Host "  Failed: $failCount" -ForegroundColor White
Write-Host "  Total:  $($testCases.Count)" -ForegroundColor White

if ($failCount -eq 0) {
    Write-Host ""
    Write-Host "All tests passed!" -ForegroundColor Green
} else {
    Write-Host ""
    Write-Host "Some tests failed. Review the results above." -ForegroundColor Red
}

Write-Host ""

# Stop the API
Write-Host "Stopping the API..." -ForegroundColor Yellow
if ($apiProcess) {
    Stop-Process -Id $apiProcess.Id -Force -ErrorAction SilentlyContinue
}
Write-Host "API stopped" -ForegroundColor Green

Write-Host ""
Write-Host "Key improvements tested:" -ForegroundColor Cyan
Write-Host "  1. Prefers longest street name matches"
Write-Host "  2. Handles unit + civic numbers" 
Write-Host "  3. Better business name recognition"
Write-Host ""
Write-Host "Press any key to exit..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")