# Direct database query test to see what's really in there

Write-Host "Direct Database Query Test" -ForegroundColor Cyan
Write-Host "==========================" -ForegroundColor Cyan
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
        Write-Host "MySQL client not found. Trying to use Python instead..." -ForegroundColor Yellow
        
        # Use Python to query the database
        $pythonScript = @"
import pymysql
import sys

connection = pymysql.connect(
    host='localhost',
    user='root',
    password='D0ntfw!thm01MA',
    database='bor_db',
    charset='utf8mb4'
)

cursor = connection.cursor()

# Check for 'ABC' as a street name
print("Checking for 'ABC' in road_network table...")
cursor.execute("SELECT COUNT(*) FROM road_network WHERE LOWER(name) = 'abc'")
count = cursor.fetchone()[0]
print(f"  Count of streets named 'ABC': {count}")

if count > 0:
    cursor.execute("SELECT ngd_uid, name, type, csd_name_left, csd_name_right, province_uid_left FROM road_network WHERE LOWER(name) = 'abc' LIMIT 5")
    print("  Sample records:")
    for row in cursor.fetchall():
        print(f"    {row}")

# Check for 'Company' as a street name
print("\nChecking for 'Company' in road_network table...")
cursor.execute("SELECT COUNT(*) FROM road_network WHERE LOWER(name) = 'company'")
count = cursor.fetchone()[0]
print(f"  Count of streets named 'Company': {count}")

if count > 0:
    cursor.execute("SELECT ngd_uid, name, type, csd_name_left, csd_name_right, province_uid_left FROM road_network WHERE LOWER(name) = 'company' LIMIT 5")
    print("  Sample records:")
    for row in cursor.fetchall():
        print(f"    {row}")

# Check for 'Mountain' as a street name
print("\nChecking for 'Mountain' in road_network table...")
cursor.execute("SELECT COUNT(*) FROM road_network WHERE LOWER(name) = 'mountain'")
count = cursor.fetchone()[0]
print(f"  Count of streets named 'Mountain': {count}")

if count > 0:
    cursor.execute("SELECT ngd_uid, name, type, csd_name_left, csd_name_right, province_uid_left FROM road_network WHERE LOWER(name) = 'mountain' LIMIT 5")
    print("  Sample records:")
    for row in cursor.fetchall():
        print(f"    {row}")

# Also check street_names table (old table)
print("\nChecking street_names table (if it exists)...")
try:
    cursor.execute("SELECT COUNT(*) FROM street_names WHERE name_lower = 'abc'")
    count = cursor.fetchone()[0]
    print(f"  Count in street_names table for 'ABC': {count}")
except:
    print("  street_names table not found or error")

connection.close()
"@
        
        $pythonScript | Out-File -FilePath "check_db.py" -Encoding UTF8
        python check_db.py
        Remove-Item "check_db.py"
        
        Write-Host ""
        Write-Host "Press any key to exit..."
        $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
        exit
    }
}

# Use MySQL to run queries
Write-Host "Running direct SQL queries..." -ForegroundColor Yellow
Write-Host ""

$queries = @(
    "SELECT COUNT(*) as count FROM road_network WHERE LOWER(name) = 'abc'",
    "SELECT COUNT(*) as count FROM road_network WHERE LOWER(name) = 'company'",
    "SELECT COUNT(*) as count FROM road_network WHERE LOWER(name) = 'mountain'"
)

foreach ($query in $queries) {
    Write-Host "Query: $query" -ForegroundColor Gray
    $result = & $mysqlCmd -h $dbHost -u $dbUser -p$dbPassword $dbName -e $query 2>$null
    Write-Host $result
    Write-Host ""
}

Write-Host "Checking what tables exist..." -ForegroundColor Yellow
$tablesQuery = "SHOW TABLES LIKE '%street%'"
$result = & $mysqlCmd -h $dbHost -u $dbUser -p$dbPassword $dbName -e $tablesQuery 2>$null
Write-Host $result
Write-Host ""

Write-Host "Press any key to exit..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")