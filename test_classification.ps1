# Test Classification Endpoints
# Run this PowerShell script to test the classification API

$baseUrl = "http://localhost:5000/api/classification"

Write-Host "Testing Classification API" -ForegroundColor Green
Write-Host "=========================" -ForegroundColor Green

# Test cases
$testCases = @(
    @{Input="Driftwood Park Retreat Inc"; Expected="business"; Reason="Contains 'Inc'"},
    @{Input="Smith"; Expected="business"; Reason="Single word - no first name"},
    @{Input="Johnson"; Expected="business"; Reason="Single word - no first name"},
    @{Input="John Smith"; Expected="residential"; Reason="First + Last name"},
    @{Input="J Smith"; Expected="residential"; Reason="Initial + Last name"},
    @{Input="J. Smith"; Expected="residential"; Reason="Initial with period + Last name"},
    @{Input="John & Mary Smith"; Expected="residential"; Reason="Multiple first names + Last"},
    @{Input="J & M Smith"; Expected="residential"; Reason="Multiple initials + Last"},
    @{Input="ABC Corporation"; Expected="business"; Reason="Contains 'Corporation'"},
    @{Input="John`'s Pizza"; Expected="business"; Reason="Possessive + business type"},
    @{Input="The Johnsons"; Expected="residential"; Reason="Family pattern"},
    @{Input="Burger King"; Expected="business"; Reason="Business name"},
    @{Input="Fenety Marketing Services"; Expected="business"; Reason="Marketing + Services"},
    @{Input="Allain`'s Office Supplies"; Expected="business"; Reason="Possessive + business words"},
    @{Input="Smith & Associates"; Expected="business"; Reason="Name + business indicator"},
    @{Input="Park Dental Clinic Ltd"; Expected="business"; Reason="Contains 'Ltd'"},
    @{Input="Mary and John Smith"; Expected="residential"; Reason="Multiple first names pattern"},
    @{Input="McDonald"; Expected="business"; Reason="Single word - no first name"},
    @{Input="Amazon"; Expected="business"; Reason="Single word - business name"}
)

$correct = 0
$total = $testCases.Count

foreach ($test in $testCases) {
    Write-Host "`nTesting: '$($test.Input)'" -ForegroundColor Yellow
    Write-Host "Expected: $($test.Expected) ($($test.Reason))" -ForegroundColor Cyan
    
    $body = @{
        input = $test.Input
        includeDetails = $true
    } | ConvertTo-Json
    
    try {
        $response = Invoke-RestMethod -Uri "$baseUrl/classify" -Method Post -Body $body -ContentType "application/json"
        
        $classification = $response.classification
        $confidence = $response.confidence
        $reason = $response.reason
        
        if ($classification -eq $test.Expected) {
            Write-Host "✓ PASS: Classified as $classification (Confidence: $confidence%)" -ForegroundColor Green
            Write-Host "  Reason: $reason" -ForegroundColor Gray
            $correct++
        } else {
            Write-Host "✗ FAIL: Classified as $classification instead of $($test.Expected)" -ForegroundColor Red
            Write-Host "  Confidence: $confidence%" -ForegroundColor Red
            Write-Host "  Reason: $reason" -ForegroundColor Red
            
            # Show detailed scores if failed
            if ($response.detailedAnalysis) {
                Write-Host "  Business Score: $($response.businessScore), Residential Score: $($response.residentialScore)" -ForegroundColor Gray
                Write-Host "  Detailed Scores:" -ForegroundColor Gray
                foreach ($score in $response.detailedAnalysis.scores.GetEnumerator()) {
                    if ($score.Value -ne 0) {
                        Write-Host "    $($score.Key): $($score.Value)" -ForegroundColor Gray
                    }
                }
            }
        }
    } catch {
        Write-Host "✗ ERROR: $_" -ForegroundColor Red
    }
}

Write-Host "`n=========================" -ForegroundColor Green
Write-Host "Results: $correct/$total tests passed" -ForegroundColor $(if ($correct -eq $total) { "Green" } else { "Yellow" })
$percentage = [math]::Round(($correct / $total) * 100, 2)
Write-Host "Success Rate: $percentage%" -ForegroundColor $(if ($percentage -ge 80) { "Green" } elseif ($percentage -ge 60) { "Yellow" } else { "Red" })

# Test batch endpoint
Write-Host "`n=========================" -ForegroundColor Green
Write-Host "Testing Batch Classification" -ForegroundColor Green

$batchBody = @{
    inputs = @(
        "Smith",
        "ABC Corp",
        "John's Pizza",
        "The Johnsons",
        "Park Ltd"
    )
} | ConvertTo-Json

try {
    $batchResponse = Invoke-RestMethod -Uri "$baseUrl/classify/batch" -Method Post -Body $batchBody -ContentType "application/json"
    Write-Host "Batch processing successful:" -ForegroundColor Green
    Write-Host "  Total: $($batchResponse.totalProcessed)" -ForegroundColor Gray
    Write-Host "  Business: $($batchResponse.businessCount)" -ForegroundColor Gray
    Write-Host "  Residential: $($batchResponse.residentialCount)" -ForegroundColor Gray
} catch {
    Write-Host "Batch processing failed: $_" -ForegroundColor Red
}