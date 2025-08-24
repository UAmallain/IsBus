# Direct database query to check for Company street
$dbHost = "localhost"
$dbUser = "root"
$dbPassword = "D0ntfw!thm01MA"
$dbName = "bor_db"

# Find MySQL
$mysqlCmd = "C:\Program Files\MySQL\MySQL Server 8.0\bin\mysql.exe"
if (-not (Test-Path $mysqlCmd)) {
    $mysqlCmd = "C:\Program Files\MySQL\MySQL Server 9.0\bin\mysql.exe"
    if (-not (Test-Path $mysqlCmd)) {
        Write-Host "MySQL not found at expected location" -ForegroundColor Red
        exit
    }
}

Write-Host "Checking for 'Company' in road_network table..." -ForegroundColor Cyan

$query = "SELECT ngd_uid, name, type, csd_name_left, province_uid_left FROM road_network WHERE LOWER(name) = 'company' LIMIT 10"
& $mysqlCmd -h $dbHost -u $dbUser -p$dbPassword $dbName -e $query 2>$null

$query2 = "SELECT COUNT(*) as count FROM road_network WHERE LOWER(name) = 'company'"
Write-Host "`nCount query:" -ForegroundColor Yellow
& $mysqlCmd -h $dbHost -u $dbUser -p$dbPassword $dbName -e $query2 2>$null

Write-Host "`nChecking for streets containing 'Company'..." -ForegroundColor Cyan
$query3 = "SELECT name, type FROM road_network WHERE LOWER(name) LIKE '%company%' LIMIT 10"
& $mysqlCmd -h $dbHost -u $dbUser -p$dbPassword $dbName -e $query3 2>$null

Write-Host "Press any key to exit..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")