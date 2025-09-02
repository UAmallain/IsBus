# Test script for cardinal direction and PTH patterns

$testCases = @(
    # Cardinal direction with hyphenated numbers
    "Interlake Full Gospel Assembly SE 16-18-3E Komarno 886-2700",
    "Sierens Equipment Ltd SE 31-6-10W 836-2243",
    "Sierens Equipment Ltd SE 31-6-10W Fax Line 836-2892",
    "New Generation Pork Glossop Inc NW 5-16-21W 365-2549",
    "Bridges Golf Course NE 19-9-1W 735-3000",
    
    # PTH (path) patterns
    "Days Inn 75 PTH 12 N 320-9200",
    "Bill's Esso & Carwash 226 PTH 2 723-2294",
    "Slater C Construction 94142 PTH 7 886-2549",
    "Mazergroup Ltd 300 PTH 12 N 326-9834",
    "Funk's Toyota Ltd 57 PTH 12 N 326-9808"
)

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Testing Cardinal Directions and PTH Keywords" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

foreach ($input in $testCases) {
    Write-Host "Input: " -NoNewline -ForegroundColor Yellow
    Write-Host $input
    
    $encodedInput = [System.Web.HttpUtility]::UrlEncode($input)
    $url = "http://localhost:5000/api/Parser/parse?input=$encodedInput"
    
    try {
        $response = Invoke-RestMethod -Uri $url -Method Get
        
        Write-Host "  Name: " -NoNewline -ForegroundColor Green
        Write-Host $response.name
        Write-Host "  Address: " -NoNewline -ForegroundColor Green
        Write-Host $response.address
        Write-Host "  Phone: " -NoNewline -ForegroundColor Green
        Write-Host $response.phone
        
        # Check if address extraction is correct
        if ($input -match "SE 16-18-3E" -and $response.address -eq "SE 16-18-3E Komarno") {
            Write-Host "  ✓ Correct!" -ForegroundColor Green
        }
        elseif ($input -match "SE 31-6-10W" -and ($response.address -match "^SE 31-6-10W")) {
            Write-Host "  ✓ Correct!" -ForegroundColor Green
        }
        elseif ($input -match "NW 5-16-21W" -and $response.address -eq "NW 5-16-21W") {
            Write-Host "  ✓ Correct!" -ForegroundColor Green
        }
        elseif ($input -match "NE 19-9-1W" -and $response.address -eq "NE 19-9-1W") {
            Write-Host "  ✓ Correct!" -ForegroundColor Green
        }
        elseif ($input -match "(\d+) PTH" -and ($response.address -match "^\d+ PTH")) {
            Write-Host "  ✓ Correct PTH pattern!" -ForegroundColor Green
        }
        else {
            # Check if the address at least starts correctly
            if ($response.address -match "^(SE|NE|NW|SW) \d+-\d+-\d+" -or $response.address -match "^\d+ PTH") {
                Write-Host "  ✓ Address starts correctly" -ForegroundColor Green
            }
            else {
                Write-Host "  ✗ Address might need adjustment" -ForegroundColor Red
            }
        }
    catch {
        Write-Host "  Error: $_" -ForegroundColor Red
    }
    
    Write-Host ""
}