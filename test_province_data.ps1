#!/usr/bin/env pwsh

# Test what province data looks like in the database

$apiUrl = "http://localhost:5000"

Write-Host "`nTesting Province Stats..." -ForegroundColor Cyan

# Test without province (should work)
Write-Host "`n1. All provinces (no filter):" -ForegroundColor Yellow
$allStats = Invoke-RestMethod -Uri "$apiUrl/api/StreetSearch/stats" -Method Get
Write-Host "   Total Streets: $($allStats.statistics.totalStreets)" -ForegroundColor Green
Write-Host "   Unique Names: $($allStats.statistics.uniqueStreetNames)" -ForegroundColor Green
Write-Host "   Communities: $($allStats.statistics.uniqueCommunities)" -ForegroundColor Green

# Test with different province formats
$provinces = @("NB", "ON", "QC", "NS", "PE", "NL", "MB", "SK", "AB", "BC", "YT", "NT", "NU")

Write-Host "`n2. Testing individual provinces:" -ForegroundColor Yellow
foreach ($prov in $provinces) {
    try {
        $stats = Invoke-RestMethod -Uri "$apiUrl/api/StreetSearch/stats?province=$prov" -Method Get
        if ($stats.statistics.totalStreets -gt 0) {
            Write-Host "   $prov : $($stats.statistics.totalStreets) streets" -ForegroundColor Green
        } else {
            Write-Host "   $prov : No data (0 streets)" -ForegroundColor Red
        }
    } catch {
        Write-Host "   $prov : Error - $_" -ForegroundColor Red
    }
}

# Test searching for a known NB street to see what province code is returned
Write-Host "`n3. Testing street search to see province format:" -ForegroundColor Yellow
$searchResults = Invoke-RestMethod -Uri "$apiUrl/api/StreetSearch/search?name=Main&limit=5" -Method Get
if ($searchResults.results) {
    foreach ($result in $searchResults.results) {
        Write-Host "   Street: $($result.streetName)" -ForegroundColor Gray
        Write-Host "   Province Code: $($result.provinceCode)" -ForegroundColor Cyan
        Write-Host "   Province Name: $($result.provinceName)" -ForegroundColor Cyan
        Write-Host "   ---"
    }
}

Write-Host "`nTest complete!" -ForegroundColor Green