#!/usr/bin/env pwsh

# Direct test to see what province values exist in the database

$apiUrl = "http://localhost:5000"

Write-Host "`nTesting Province Values in Database..." -ForegroundColor Cyan

# Search for a known New Brunswick street
Write-Host "`n1. Searching for Moncton streets (should be in NB):" -ForegroundColor Yellow
try {
    $searchResults = Invoke-RestMethod -Uri "$apiUrl/api/StreetSearch/search?name=Main&community=Moncton&limit=3" -Method Get
    if ($searchResults.results) {
        foreach ($result in $searchResults.results) {
            Write-Host "   Street: $($result.streetName) $($result.streetType)" -ForegroundColor Gray
            Write-Host "   Community: $($result.communities -join ', ')" -ForegroundColor Gray
            Write-Host "   Province Code: '$($result.provinceCode)'" -ForegroundColor Cyan
            Write-Host "   Province Name: $($result.provinceName)" -ForegroundColor Cyan
            Write-Host "   ---"
        }
    } else {
        Write-Host "   No results found" -ForegroundColor Red
    }
} catch {
    Write-Host "   Error: $_" -ForegroundColor Red
}

# Try different province code formats
Write-Host "`n2. Testing stats with different province formats:" -ForegroundColor Yellow

$testFormats = @(
    @{Code="NB"; Description="Two-letter code"},
    @{Code="13"; Description="Numeric code for NB"},
    @{Code="New Brunswick"; Description="Full name"},
    @{Code="new brunswick"; Description="Lowercase name"}
)

foreach ($test in $testFormats) {
    Write-Host "`n   Testing: $($test.Description) ('$($test.Code)')" -ForegroundColor Gray
    try {
        $stats = Invoke-RestMethod -Uri "$apiUrl/api/StreetSearch/stats?province=$([System.Web.HttpUtility]::UrlEncode($test.Code))" -Method Get
        Write-Host "   Result: $($stats.statistics.totalStreets) streets" -ForegroundColor $(if ($stats.statistics.totalStreets -gt 0) { "Green" } else { "Red" })
    } catch {
        Write-Host "   Error: $_" -ForegroundColor Red
    }
}

Write-Host "`nTest complete!" -ForegroundColor Green