# Simple script to set up province mapping table

Write-Host "Setting up Province Mapping Table" -ForegroundColor Cyan
Write-Host "=================================" -ForegroundColor Cyan
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
        Write-Host "  Database/create_province_mapping.sql" -ForegroundColor White
        Write-Host ""
        Write-Host "Press any key to exit..."
        $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
        exit 1
    }
}

# Execute the SQL file
Write-Host "Creating province mapping table..." -ForegroundColor Yellow

$sqlCommand = "source Database/create_province_mapping.sql"
$mysqlParams = @("-h", $dbHost, "-u", $dbUser, "-p$dbPassword", $dbName, "-e", $sqlCommand)

try {
    $result = & $mysqlCmd @mysqlParams 2>&1
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Province mapping table created successfully!" -ForegroundColor Green
    } else {
        Write-Host "Error executing SQL. Output:" -ForegroundColor Red
        Write-Host $result
    }
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
}

Write-Host ""
Write-Host "Setup complete. You can now use the street search API with province support." -ForegroundColor Green
Write-Host ""
Write-Host "Press any key to exit..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")