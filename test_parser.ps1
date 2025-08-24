# Test script for String Parser API
param(
    [string]$BaseUrl = "http://localhost:5000",
    [string]$Province = "NB"  # Default to New Brunswick
)

$testCases = @(
    "DICKINSON K 392-2870",
    "Doran Anne Spencers Island  392-2959",
    "Driftwood Park Retreat Inc 47 Driftwood Ln 392-2008",
    "Drysdale K 165 Highway 209 392-2956",
    "Dunbar Etta 392-2549",
    "Dunbar John 3826 Highway 209 392-3333",
    "Dunham Glenda Darrell Spencers Island  392-2124",
    "Elliott Gordon E & Sandra 3582 Hwy 209 392-2328",
    "Ells Brian  392-2804",
    "Ells Dale E 392-2981",
    "Emberley A M 2418 Apple River Rd Apple River  392-2934",
    "Field Erma Apple River 392-2774",
    "Field G M West Advocate 392-2614",
    "Alaimo Calogero 2 Farlinger Bay 338-4159",
    "Adeoye Abiodun 431 570-2868",
    "Advocate Country Store  392-2292",
    "Advocate Library 93 Mills Rd 392-2214",
    "Berry Cathy & Fallon Fraserville  392-2110",
    "Nova Communications Moncton 506 854-7070"  # Test case with community name
)

Write-Host "Testing String Parser API" -ForegroundColor Cyan
Write-Host "=========================" -ForegroundColor Cyan
Write-Host ""

foreach ($testCase in $testCases) {
    Write-Host "Input: " -NoNewline -ForegroundColor Yellow
    Write-Host $testCase
    
    try {
        $body = @{ input = $testCase; province = $Province } | ConvertTo-Json
        $response = Invoke-RestMethod -Uri "$BaseUrl/api/parser/parse" -Method POST -Body $body -ContentType "application/json"
        
        if ($response.success) {
            Write-Host "  Name: " -NoNewline -ForegroundColor Green
            Write-Host $response.name
            Write-Host "  Address: " -NoNewline -ForegroundColor Green
            Write-Host $(if ($response.address) { $response.address } else { "(none)" })
            Write-Host "  Phone: " -NoNewline -ForegroundColor Green
            Write-Host $response.phone
            
            if ($response.isBusinessName) {
                Write-Host "  Type: Business" -ForegroundColor Cyan
            } elseif ($response.isResidentialName) {
                Write-Host "  Type: Residential" -ForegroundColor Cyan
            }
        } else {
            Write-Host "  ERROR: $($response.errorMessage)" -ForegroundColor Red
        }
    } catch {
        Write-Host "  ERROR: $($_.Exception.Message)" -ForegroundColor Red
    }
    
    Write-Host ""
}

# Test batch parsing
Write-Host "Testing Batch Parsing..." -ForegroundColor Cyan
Write-Host "========================" -ForegroundColor Cyan

$batchRequest = @{
    inputs = $testCases[0..4]
    province = $Province
} | ConvertTo-Json

try {
    $batchResponse = Invoke-RestMethod -Uri "$BaseUrl/api/parser/parse/batch" -Method POST -Body $batchRequest -ContentType "application/json"
    Write-Host "Processed: $($batchResponse.totalProcessed)" -ForegroundColor Green
    Write-Host "Success: $($batchResponse.successCount)" -ForegroundColor Green
    Write-Host "Failed: $($batchResponse.failureCount)" -ForegroundColor Red
} catch {
    Write-Host "Batch parsing failed: $($_.Exception.Message)" -ForegroundColor Red
}