# =====================================================
# PowerShell Script to Migrate Communities Table
# From: phonebook_db to bor_db
# =====================================================

param(
    [string]$MySqlPath = "C:\Program Files\MySQL\MySQL Server 8.0\bin\mysql.exe",
    [string]$User = "root",
    [string]$Password = "Boring321",
    [switch]$SkipBackup
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Communities Table Migration Script" -ForegroundColor Cyan
Write-Host "From: phonebook_db -> To: bor_db" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check if MySQL is accessible
if (-not (Test-Path $MySqlPath)) {
    Write-Host "MySQL not found at: $MySqlPath" -ForegroundColor Red
    Write-Host "Please update the MySqlPath parameter or install MySQL" -ForegroundColor Yellow
    exit 1
}

# Function to execute MySQL command
function Execute-MySQLCommand {
    param([string]$Command, [string]$Database = "")
    
    $dbParam = if ($Database) { $Database } else { "" }
    $result = & $MySqlPath -u $User -p"$Password" $dbParam -e "$Command" 2>&1
    return $result
}

# Step 1: Check if source table exists
Write-Host "Step 1: Checking source table..." -ForegroundColor Green
$sourceCheck = Execute-MySQLCommand -Command "SELECT COUNT(*) as count FROM communities" -Database "phonebook_db"
if ($LASTEXITCODE -ne 0) {
    Write-Host "Error: Could not access communities table in phonebook_db" -ForegroundColor Red
    Write-Host $sourceCheck -ForegroundColor Red
    exit 1
}

# Extract count using regex
$sourceCount = 0
if ($sourceCheck -match '(\d+)') {
    $sourceCount = [int]$matches[1]
}
Write-Host "Found $sourceCount records in phonebook_db.communities" -ForegroundColor Yellow

# Step 2: Check the structure of the source table
Write-Host "`nStep 2: Analyzing source table structure..." -ForegroundColor Green
$structure = Execute-MySQLCommand -Command "DESCRIBE communities" -Database "phonebook_db"
Write-Host "Table structure:" -ForegroundColor Cyan
Write-Host $structure

# Step 3: Backup existing data if requested
if (-not $SkipBackup) {
    Write-Host "`nStep 3: Creating backup of existing data (if any)..." -ForegroundColor Green
    $backupFile = "communities_backup_$(Get-Date -Format 'yyyyMMdd_HHmmss').sql"
    $backupPath = Join-Path (Get-Location) $backupFile
    
    # Export existing bor_db.communities if it exists
    & $MySqlPath.Replace("mysql.exe", "mysqldump.exe") -u $User -p"$Password" bor_db communities > $backupPath 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Backup created: $backupPath" -ForegroundColor Green
    } else {
        Write-Host "No existing data to backup or backup skipped" -ForegroundColor Yellow
    }
} else {
    Write-Host "`nStep 3: Skipping backup (SkipBackup flag set)" -ForegroundColor Yellow
}

# Step 4: Run the migration SQL script
Write-Host "`nStep 4: Running migration script..." -ForegroundColor Green
$sqlFile = "migrate_communities_table.sql"
if (-not (Test-Path $sqlFile)) {
    Write-Host "Error: Migration script not found: $sqlFile" -ForegroundColor Red
    exit 1
}

$migrationResult = & $MySqlPath -u $User -p"$Password" < $sqlFile 2>&1
if ($LASTEXITCODE -eq 0) {
    Write-Host "Migration completed successfully!" -ForegroundColor Green
    Write-Host $migrationResult
} else {
    Write-Host "Error during migration:" -ForegroundColor Red
    Write-Host $migrationResult -ForegroundColor Red
    exit 1
}

# Step 5: Verify migration
Write-Host "`nStep 5: Verifying migration..." -ForegroundColor Green
$targetCheck = Execute-MySQLCommand -Command "SELECT COUNT(*) as count FROM communities" -Database "bor_db"
$targetCount = 0
if ($targetCheck -match '(\d+)') {
    $targetCount = [int]$matches[1]
}

Write-Host "Records in bor_db.communities: $targetCount" -ForegroundColor Yellow

if ($targetCount -eq $sourceCount) {
    Write-Host "`nMigration SUCCESSFUL!" -ForegroundColor Green
    Write-Host "All $sourceCount records migrated successfully." -ForegroundColor Green
} elseif ($targetCount -gt $sourceCount) {
    Write-Host "`nMigration completed with existing records preserved." -ForegroundColor Yellow
    Write-Host "Source: $sourceCount records, Target: $targetCount records" -ForegroundColor Yellow
} else {
    Write-Host "`nWarning: Record count mismatch!" -ForegroundColor Yellow
    Write-Host "Source: $sourceCount records, Target: $targetCount records" -ForegroundColor Yellow
}

# Step 6: Check for dependencies
Write-Host "`nStep 6: Checking for dependencies..." -ForegroundColor Green
$dependencies = Execute-MySQLCommand -Command "SELECT TABLE_NAME, COLUMN_NAME FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE WHERE REFERENCED_TABLE_NAME = 'communities' AND TABLE_SCHEMA = 'bor_db'" -Database "information_schema"
if ($dependencies) {
    Write-Host "Found the following tables referencing communities:" -ForegroundColor Yellow
    Write-Host $dependencies
} else {
    Write-Host "No dependent tables found." -ForegroundColor Green
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Migration Process Complete!" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan