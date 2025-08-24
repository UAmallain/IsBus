# Test the final parser implementation with backwards checking
Write-Host "Testing Final Parser with Backwards Checking" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host ""

$tests = @(
    @{
        input = "ABC Company Mountain Road ON 416 555-1234"
        expectedName = "ABC Company"
        expectedAddress = "Mountain Road ON"
    },
    @{
        input = "ABC Company Indian Mountain Road NB 506 555-1234"
        expectedName = "ABC Company"
        expectedAddress = "Indian Mountain Road NB"
    },
    @{
        input = "Quality Carpets 223 1057 Beaverhill Blvd 780-449-2835"
        expectedName = "Quality Carpets"
        expectedAddress = "223 1057 Beaverhill Blvd"
    },
    @{
        input = "John Doe 123 Company Road 555-1234"
        expectedName = "John Doe"
        expectedAddress = "123 Company Road"
    },
    @{
        input = "Indian Mountain Road Company 123 Main St ON 705-123-4567"
        expectedName = "Indian Mountain Road Company"
        expectedAddress = "123 Main St ON"
    }
)

$passed = 0
$failed = 0

foreach ($test in $tests) {
    Write-Host "Input: " -NoNewline
    Write-Host $test.input -ForegroundColor Yellow
    
    try {
        $result = Invoke-RestMethod -Uri 'http://localhost:5000/api/parser/parse' `
                                    -Method POST `
                                    -ContentType 'application/json' `
                                    -Body "{`"input`": `"$($test.input)`"}"
        
        $nameMatch = $result.name -eq $test.expectedName
        $addressMatch = $result.address -eq $test.expectedAddress
        
        if ($nameMatch -and $addressMatch) {
            $passed++
            Write-Host "  PASS" -ForegroundColor Green
        } else {
            $failed++
            Write-Host "  FAIL" -ForegroundColor Red
        }
        
        if ($nameMatch) {
            Write-Host "    Name: $($result.name)" -ForegroundColor Green
        } else {
            Write-Host "    Name: $($result.name) (expected: $($test.expectedName))" -ForegroundColor Red
        }
        
        if ($addressMatch) {
            Write-Host "    Address: $($result.address)" -ForegroundColor Green
        } else {
            Write-Host "    Address: $($result.address) (expected: $($test.expectedAddress))" -ForegroundColor Red
        }
        
        Write-Host "    Phone: $($result.phone)" -ForegroundColor Blue
    }
    catch {
        $failed++
        Write-Host "  ERROR: $_" -ForegroundColor Red
    }
    
    Write-Host ""
}

Write-Host "===============================================" -ForegroundColor Cyan
Write-Host "Results: $passed passed, $failed failed" -ForegroundColor $(if ($failed -eq 0) { "Green" } else { "Yellow" })
Write-Host ""
Write-Host "Press any key to exit..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")