# PowerShell script to test the problematic records

$testCases = @(
    "Adshade Wilfred 231 Westminster Av Riverview 386-8496",
    "Adsett Robert B 210 Country View Rd 384-8266",
    "Adesina A Mountain Rd 830-7098",
    "Adesina 0 382-4955",
    "Adeoye Haqq 183 Oakfield Dr 386-4519",
    "Adebukola Bada 115 Lorne St 388-2674",
    "Addington Andrea 26 Fairlane Dr 851-1340",
    "Adao Aurora 858-8912",
    "Adams Walter 88 Sunset Rd 853-4115",
    "Adams Todd 1009 Cleveland Av 853-8202",
    "Adams T 925 Elmwood Dr 204-4110",
    "Adams S Grindstone Dr 854-8144",
    "Adams Nick 7 Parkwood Av 533-7099",
    "Adams M 97 Notingham Dr 204-8479",
    "Adams Jessica 19 Naples Dr 854-1588",
    "Adams D 115 Chateau Dr 857-0697",
    "Adair A 53 Dundee Dr 372-9518",
    "Ackman F Douglas Dr Cap - Brûlé 532-3836",
    "Abli R A22 21 Arden St 830-8056",
    "Aberathna B 150 Wynwood Dr 830-7163",
    "Abdelhadi Z 152 Holland Dr 830-2167"
)

$logContent = "=== DEBUG TEST RUN - $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') ===`n`n"

foreach ($input in $testCases) {
    Write-Host "`n$('=' * 80)`nTesting: $input`n$('=' * 80)" -ForegroundColor Yellow
    
    $body = @{
        Input = $input
        Province = "NB"
        AreaCode = "506"
    } | ConvertTo-Json
    
    try {
        $response = Invoke-RestMethod -Uri "http://localhost:5000/api/Parser/parse" `
            -Method Post `
            -Body $body `
            -ContentType "application/json"
        
        $classification = if ($response.isBusinessName) { "BUSINESS" } else { "RESIDENTIAL" }
        
        Write-Host "Result: $classification" -ForegroundColor $(if ($response.isBusinessName) { "Red" } else { "Green" })
        Write-Host "  Confidence: $($response.confidence.nameConfidence)%"
        Write-Host "  Name: $($response.name)"
        Write-Host "  Address: $($response.address)"
        Write-Host "  Phone: $($response.phone)"
        
        $logContent += "$('=' * 80)`nINPUT: $input`n$('=' * 80)`n"
        $logContent += "RESULT: $classification`n"
        $logContent += "  Confidence: $($response.confidence.nameConfidence)%`n"
        $logContent += "  Name: $($response.name)`n"
        if ($response.isResidentialName) {
            $logContent += "  First Name: $($response.firstName)`n"
            $logContent += "  Last Name: $($response.lastName)`n"
        }
        $logContent += "  Address: $($response.address)`n"
        $logContent += "  Phone: $($response.phone)`n`n"
    }
    catch {
        Write-Host "ERROR: $_" -ForegroundColor Red
        $logContent += "ERROR: $_`n`n"
    }
}

$logContent | Out-File -FilePath "debug_classification_log.txt" -Encoding UTF8
Write-Host "`n`nDebug log written to: debug_classification_log.txt" -ForegroundColor Cyan