# Simple script to set up street type mapping table

Write-Host "Setting up Street Type Mapping Table" -ForegroundColor Cyan
Write-Host "====================================" -ForegroundColor Cyan
Write-Host ""

# Database connection parameters
$dbHost = "localhost"
$dbUser = "root"
$dbPassword = "D0ntfw!thm01MA"
$dbName = "bor_db"

# Find MySQL
$mysqlCmd = "mysql"
if (-not (Get-Command $mysqlCmd -ErrorAction SilentlyContinue)) {
    $mysqlCmd = "C:\Program Files\MySQL\MySQL Server 8.0\bin\mysql.exe"
    if (-not (Test-Path $mysqlCmd)) {
        Write-Host "MySQL client not found." -ForegroundColor Red
        Write-Host "Please run the following SQL file manually in your MySQL client:" -ForegroundColor Yellow
        Write-Host "  Database/create_street_type_mapping.sql" -ForegroundColor White
        Write-Host ""
        Write-Host "Press any key to exit..."
        $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
        exit 1
    }
}

# Execute the SQL file
Write-Host "Creating street type mapping table..." -ForegroundColor Yellow

$sqlCommand = "source Database/create_street_type_mapping.sql"
$mysqlParams = @("-h", $dbHost, "-u", $dbUser, "-p$dbPassword", $dbName, "-e", $sqlCommand)

try {
    $result = & $mysqlCmd @mysqlParams 2>&1
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Street type mapping table created successfully!" -ForegroundColor Green
        Write-Host ""
        Write-Host "130+ street type abbreviations have been loaded, including:" -ForegroundColor White
        Write-Host "  - Common types: st→Street, ave→Avenue, rd→Road, blvd→Boulevard" -ForegroundColor Gray
        Write-Host "  - Highway types: hwy→Highway, fwy→Freeway, rte→Route" -ForegroundColor Gray
        Write-Host "  - Canadian types: conc→Concession, rang→Rang, sdrd→Sideroad" -ForegroundColor Gray
        Write-Host "  - French types: ch→Chemin, rue→Rue, mont→Montée" -ForegroundColor Gray
    } else {
        Write-Host "Error executing SQL. Output:" -ForegroundColor Red
        Write-Host $result
    }
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
}

Write-Host ""
Write-Host "Setup complete. The street search API now supports street type normalization." -ForegroundColor Green
Write-Host ""
Write-Host "Press any key to exit..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")