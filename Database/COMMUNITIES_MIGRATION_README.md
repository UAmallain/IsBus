# Communities Table Migration Guide

## Overview
This guide explains how to migrate the `communities` table from `phonebook_db` to `bor_db`.

## Files Created
1. **migrate_communities_table.sql** - Main SQL migration script
2. **migrate_communities.ps1** - PowerShell automation script
3. **migrate_communities.bat** - Windows batch file for easy execution
4. **export_import_communities.sql** - Simple export/import alternative
5. **verify_communities_migration.sql** - Verification queries

## Migration Methods

### Method 1: Using Batch File (Easiest for Windows)
```batch
cd Database
migrate_communities.bat
```

### Method 2: Using PowerShell Script
```powershell
cd Database
.\migrate_communities.ps1 -User root -Password Boring321
```

### Method 3: Direct SQL Execution
```bash
mysql -u root -p < migrate_communities_table.sql
```

### Method 4: Using mysqldump (Manual)
```bash
# Export from source
mysqldump -u root -p phonebook_db communities > communities_export.sql

# Import to target
mysql -u root -p bor_db < communities_export.sql
```

### Method 5: Direct Copy (One-liner)
```bash
mysqldump -u root -p phonebook_db communities | mysql -u root -p bor_db
```

## Verification
After migration, verify the results:

```sql
-- Check record counts
mysql -u root -p bor_db -e "SELECT COUNT(*) FROM communities"

-- Or run the verification script
mysql -u root -p < verify_communities_migration.sql
```

## What Gets Migrated
- Table structure (all columns)
- All data records
- Indexes
- Constraints

## Safety Features
- Creates backup before migration
- Uses INSERT IGNORE to prevent duplicates
- Preserves existing data if table already exists
- Shows record count comparison

## Troubleshooting

### MySQL command not found
Update the path in the batch/PowerShell script to point to your MySQL installation:
- Default: `C:\Program Files\MySQL\MySQL Server 8.0\bin`
- XAMPP: `C:\xampp\mysql\bin`
- MariaDB: `C:\Program Files\MariaDB 10.x\bin`

### Access denied error
Ensure you're using the correct username and password:
- Default in scripts: `root` / `Boring321`

### Table already exists
The script handles existing tables, but if you want a clean migration:
1. Backup existing data first
2. Drop the table: `DROP TABLE bor_db.communities;`
3. Run migration again

## Post-Migration Steps
1. Update your application connection string to use `bor_db`
2. Test application functionality with the new database
3. Update any stored procedures or views that reference `communities`
4. Consider dropping the old table from `phonebook_db` after verification