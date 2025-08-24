#!/usr/bin/env pwsh

# Test comprehensive phonebook parsing with various name formats

$apiUrl = "http://localhost:5000/api/parser/parse/batch"
$testFile = "test_phonebook_comprehensive.json"

Write-Host "Testing comprehensive phonebook parsing..." -ForegroundColor Cyan
Write-Host "Loading test data from $testFile" -ForegroundColor Yellow

# Read the test JSON file
$jsonContent = Get-Content $testFile -Raw
$testData = $jsonContent | ConvertFrom-Json

# Prepare the batch request
$batchRequest = @{
    inputs = $testData.inputs
    province = $testData.province
}

$jsonBody = $batchRequest | ConvertTo-Json -Depth 10

Write-Host "`nSending batch request with $($testData.inputs.Count) entries..." -ForegroundColor Yellow

try {
    $response = Invoke-RestMethod -Uri $apiUrl -Method Post -Body $jsonBody -ContentType "application/json"
    
    Write-Host "`nResults:" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Gray
    
    # Convert response to show all properties
    $responseJson = $response | ConvertTo-Json -Depth 10
    Write-Host "Raw Response (first 1000 chars):" -ForegroundColor Yellow
    Write-Host $responseJson.Substring(0, [Math]::Min(1000, $responseJson.Length))
    
    $index = 0
    foreach ($result in $response) {
        $input = $testData.inputs[$index]
        Write-Host "`nInput $($index + 1): " -NoNewline -ForegroundColor Cyan
        Write-Host $input
        
        if ($result.error) {
            Write-Host "  ERROR: " -NoNewline -ForegroundColor Red
            Write-Host $result.error
        } else {
            # Display parsed components
            if ($result.businessName) {
                Write-Host "  Business: " -NoNewline -ForegroundColor Yellow
                Write-Host $result.businessName
            }
            if ($result.personName) {
                Write-Host "  Person: " -NoNewline -ForegroundColor Yellow
                Write-Host $result.personName
            }
            if ($result.streetNumber) {
                Write-Host "  Street #: " -NoNewline -ForegroundColor Gray
                Write-Host $result.streetNumber
            }
            if ($result.unitNumber) {
                Write-Host "  Unit: " -NoNewline -ForegroundColor Gray
                Write-Host $result.unitNumber
            }
            if ($result.streetName) {
                Write-Host "  Street: " -NoNewline -ForegroundColor Green
                Write-Host $result.streetName
            }
            if ($result.streetType) {
                Write-Host "  Type: " -NoNewline -ForegroundColor Green
                Write-Host $result.streetType
            }
            if ($result.community) {
                Write-Host "  Community: " -NoNewline -ForegroundColor Magenta
                Write-Host $result.community
            }
            if ($result.phoneNumber) {
                Write-Host "  Phone: " -NoNewline -ForegroundColor Blue
                Write-Host $result.phoneNumber
            }
            
            # Show the raw parsed result for debugging
            if ($result.remainingText) {
                Write-Host "  Remaining: " -NoNewline -ForegroundColor DarkGray
                Write-Host $result.remainingText
            }
        }
        
        $index++
    }
    
    Write-Host "`n========================================" -ForegroundColor Gray
    Write-Host "Total processed: $($response.results.Count)" -ForegroundColor Cyan
    
    # Count successes and failures
    $successes = $response.results | Where-Object { -not $_.error } | Measure-Object
    $failures = $response.results | Where-Object { $_.error } | Measure-Object
    
    Write-Host "Successful: $($successes.Count)" -ForegroundColor Green
    Write-Host "Failed: $($failures.Count)" -ForegroundColor Red
    
    # Analyze name parsing patterns
    Write-Host "`nName Pattern Analysis:" -ForegroundColor Cyan
    Write-Host "----------------------------------------" -ForegroundColor Gray
    
    $personNames = $response.results | Where-Object { $_.personName } | Select-Object -ExpandProperty personName
    $nameWordCounts = $personNames | ForEach-Object { ($_ -split ' ').Count }
    
    if ($nameWordCounts) {
        $wordCountGroups = $nameWordCounts | Group-Object | Sort-Object Name
        
        foreach ($group in $wordCountGroups) {
            Write-Host "  $($group.Name)-word names: $($group.Count)" -ForegroundColor Yellow
        }
        
        Write-Host "`nExample names by word count:" -ForegroundColor Cyan
        foreach ($group in $wordCountGroups) {
            $examples = $personNames | Where-Object { ($_ -split ' ').Count -eq [int]$group.Name } | Select-Object -First 3
            Write-Host "  $($group.Name) words: $($examples -join ', ')" -ForegroundColor Gray
        }
    }
    
} catch {
    Write-Host "`nError calling API:" -ForegroundColor Red
    Write-Host $_.Exception.Message
    
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $responseBody = $reader.ReadToEnd()
        Write-Host "`nResponse body:" -ForegroundColor Yellow
        Write-Host $responseBody
    }
}

Write-Host "`nTest complete!" -ForegroundColor Green