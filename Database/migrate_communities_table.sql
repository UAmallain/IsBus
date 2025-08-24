-- =====================================================
-- Migration Script: Communities Table
-- From: phonebook_db
-- To: bor_db
-- Date: 2025-08-22
-- =====================================================

-- STEP 1: Create communities table in bor_db if it doesn't exist
USE bor_db;

-- Drop backup table if it exists (will recreate with correct structure)
DROP TABLE IF EXISTS communities_backup;

-- Create the communities table by copying structure from source
CREATE TABLE IF NOT EXISTS communities LIKE phonebook_db.communities;

-- Create backup table with same structure as source
CREATE TABLE IF NOT EXISTS communities_backup LIKE phonebook_db.communities;

-- STEP 2: Backup existing data if any exists in bor_db.communities
INSERT IGNORE INTO communities_backup 
SELECT * FROM communities 
WHERE (SELECT COUNT(*) FROM communities) > 0;

-- STEP 3: Migrate data from phonebook_db
-- Using SELECT * to copy all columns regardless of structure
INSERT IGNORE INTO communities 
SELECT * FROM phonebook_db.communities;

-- STEP 4: Verify migration
SELECT 'Migration Summary:' as Info;
SELECT CONCAT('Records in phonebook_db.communities: ', COUNT(*)) as Source_Count 
FROM phonebook_db.communities;
SELECT CONCAT('Records in bor_db.communities: ', COUNT(*)) as Target_Count 
FROM bor_db.communities;

-- STEP 5: Check for any related tables or foreign keys that might need updating
SELECT 
    TABLE_NAME,
    COLUMN_NAME,
    CONSTRAINT_NAME,
    REFERENCED_TABLE_NAME,
    REFERENCED_COLUMN_NAME
FROM
    INFORMATION_SCHEMA.KEY_COLUMN_USAGE
WHERE
    REFERENCED_TABLE_NAME = 'communities'
    AND TABLE_SCHEMA = 'phonebook_db';