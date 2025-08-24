# Test the final parser implementation
Write-Host "Testing Final Parser Implementation" -ForegroundColor Cyan
Write-Host "===================================" -ForegroundColor Cyan
Write-Host ""

$tests = @(
    @{
        input = "ABC Company Mountain Road ON 416 555-1234"
        expectedName = "ABC Company"
        expectedAddress = "Mountain Road ON"
    },
    @{
        input = "Indian Mountain Road Company 123 Main St ON 705-123-4567"
        expectedName = "Indian Mountain Road Company"
        expectedAddress = "123 Main St ON"
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
    }
)

foreach ($test in $tests) {
    Write-Host "Input: $($test.input)" -ForegroundColor Yellow
    
    try {
        $result = Invoke-RestMethod -Uri 'http://localhost:5000/api/parser/parse' `
                                    -Method POST `
                                    -ContentType 'application/json' `
                                    -Body "{`"input`": `"$($test.input)`"}"
        
        $nameMatch = $result.name -eq $test.expectedName
        $addressMatch = $result.address -eq $test.expectedAddress
        
        if ($nameMatch) {
            Write-Host "  ✓ Name: $($result.name)" -ForegroundColor Green
        } else {
            Write-Host "  ✗ Name: $($result.name) (expected: $($test.expectedName))" -ForegroundColor Red
        }
        
        if ($addressMatch) {
            Write-Host "  ✓ Address: $($result.address)" -ForegroundColor Green
        } else {
            Write-Host "  ✗ Address: $($result.address) (expected: $($test.expectedAddress))" -ForegroundColor Red
        }
        
        Write-Host "  Phone: $($result.phone)" -ForegroundColor Blue
    }
    catch {
        Write-Host "  ✗ Error: $_" -ForegroundColor Red
    }
    
    Write-Host ""
}

Write-Host "Press any key to exit..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")