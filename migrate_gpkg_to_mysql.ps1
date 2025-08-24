# PowerShell script to migrate GeoPackage data to MySQL
# Requires Python 3 with pymysql library installed

Write-Host "===========================================" -ForegroundColor Cyan
Write-Host "GeoPackage to MySQL Migration Script" -ForegroundColor Cyan
Write-Host "===========================================" -ForegroundColor Cyan
Write-Host ""

# Check if Python is installed
$pythonCmd = Get-Command python -ErrorAction SilentlyContinue
if (-not $pythonCmd) {
    $pythonCmd = Get-Command python3 -ErrorAction SilentlyContinue
}

if (-not $pythonCmd) {
    Write-Host "Error: Python is not installed or not in PATH" -ForegroundColor Red
    exit 1
}

Write-Host "Python found at: $($pythonCmd.Source)" -ForegroundColor Green

# Check if required Python packages are installed
Write-Host "Checking required Python packages..." -ForegroundColor Yellow
$packages = @("pymysql")

foreach ($package in $packages) {
    $installed = & $pythonCmd.Source -c "import $package; print('installed')" 2>$null
    if ($installed -ne "installed") {
        Write-Host "Installing $package..." -ForegroundColor Yellow
        & $pythonCmd.Source -m pip install $package
    } else {
        Write-Host "✓ $package is installed" -ForegroundColor Green
    }
}

# Check if GeoPackage file exists
if (-not (Test-Path "lrnf000r25p_e.gpkg")) {
    Write-Host "Error: GeoPackage file 'lrnf000r25p_e.gpkg' not found in current directory" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Starting migration..." -ForegroundColor Cyan
Write-Host "Source: lrnf000r25p_e.gpkg" -ForegroundColor White
Write-Host "Target: MySQL bor_db database" -ForegroundColor White
Write-Host ""

# Run the Python migration script
& $pythonCmd.Source migrate_gpkg_to_mysql.py

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "Migration completed successfully!" -ForegroundColor Green
} else {
    Write-Host ""
    Write-Host "Migration failed with error code: $LASTEXITCODE" -ForegroundColor Red
}

Write-Host ""
Write-Host "Press any key to exit..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")