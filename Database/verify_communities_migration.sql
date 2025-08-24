-- =====================================================
-- Verification Script for Communities Migration
-- =====================================================

-- Check if communities table exists in both databases
SELECT 
    'phonebook_db' as Database_Name,
    COUNT(*) as Table_Exists
FROM information_schema.tables 
WHERE table_schema = 'phonebook_db' 
    AND table_name = 'communities'
UNION ALL
SELECT 
    'bor_db' as Database_Name,
    COUNT(*) as Table_Exists
FROM information_schema.tables 
WHERE table_schema = 'bor_db' 
    AND table_name = 'communities';

-- Compare record counts
SELECT 
    'phonebook_db.communities' as Table_Name,
    COUNT(*) as Record_Count
FROM phonebook_db.communities
UNION ALL
SELECT 
    'bor_db.communities' as Table_Name,
    COUNT(*) as Record_Count
FROM bor_db.communities;

-- Show table structure in bor_db
DESCRIBE bor_db.communities;

-- Sample data from bor_db (first 10 records)
SELECT * FROM bor_db.communities LIMIT 10;

-- Check for any indexes
SHOW INDEXES FROM bor_db.communities;

-- Check for any foreign key relationships
SELECT 
    TABLE_NAME,
    COLUMN_NAME,
    CONSTRAINT_NAME,
    REFERENCED_TABLE_NAME,
    REFERENCED_COLUMN_NAME
FROM
    INFORMATION_SCHEMA.KEY_COLUMN_USAGE
WHERE
    (REFERENCED_TABLE_NAME = 'communities' OR TABLE_NAME = 'communities')
    AND TABLE_SCHEMA IN ('phonebook_db', 'bor_db');