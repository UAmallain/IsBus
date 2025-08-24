@echo off
REM =====================================================
REM Batch Script to Migrate Communities Table
REM From: phonebook_db to bor_db
REM =====================================================

echo ========================================
echo Communities Table Migration
echo From: phonebook_db to bor_db
echo ========================================
echo.

REM Set MySQL credentials (update these if needed)
set MYSQL_USER=root
set MYSQL_PASS=D0ntfw!thm01MA
set MYSQL_PATH="C:\Program Files\MariaDB 11.7\bin"

REM Check if MySQL is in PATH or use the full path
where mysql >nul 2>nul
if %errorlevel% == 0 (
    set MYSQL_CMD=mysql
    set MYSQLDUMP_CMD=mysqldump
) else (
    set MYSQL_CMD=%MYSQL_PATH%\mysql.exe
    set MYSQLDUMP_CMD=%MYSQL_PATH%\mysqldump.exe
)

echo Step 1: Creating backup of communities table...
%MYSQLDUMP_CMD% -u %MYSQL_USER% -p%MYSQL_PASS% phonebook_db communities > communities_backup_%date:~-4%%date:~4,2%%date:~7,2%.sql
if %errorlevel% == 0 (
    echo Backup created successfully!
) else (
    echo Warning: Could not create backup
    pause
)

echo.
echo Step 2: Checking source table...
%MYSQL_CMD% -u %MYSQL_USER% -p%MYSQL_PASS% phonebook_db -e "SELECT COUNT(*) as 'Records in phonebook_db.communities' FROM communities"
if %errorlevel% neq 0 (
    echo Error: Could not access phonebook_db.communities
    pause
    exit /b 1
)

echo.
echo Step 3: Running migration...
echo Using simplified migration script...
%MYSQL_CMD% -u %MYSQL_USER% -p%MYSQL_PASS% < migrate_communities_simple.sql
if %errorlevel% == 0 (
    echo Migration completed successfully!
) else (
    echo Error during migration! Trying alternative script...
    %MYSQL_CMD% -u %MYSQL_USER% -p%MYSQL_PASS% < migrate_communities_table.sql
    if %errorlevel% == 0 (
        echo Migration completed with alternative script!
    ) else (
        echo Both migration scripts failed!
        pause
        exit /b 1
    )
)

echo.
echo Step 4: Verifying migration...
%MYSQL_CMD% -u %MYSQL_USER% -p%MYSQL_PASS% bor_db -e "SELECT COUNT(*) as 'Records in bor_db.communities' FROM communities"

echo.
echo ========================================
echo Migration Complete!
echo ========================================
echo.
echo You can verify the migration by running:
echo mysql -u %MYSQL_USER% -p bor_db
echo Then: SELECT * FROM communities LIMIT 10;
echo.
pause