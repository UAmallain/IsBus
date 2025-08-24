-- =====================================================
-- Simple Export/Import Script for Communities Table
-- =====================================================

-- To execute this migration:
-- 1. First export the structure and data:
--    mysqldump -u root -p phonebook_db communities > communities_export.sql
--
-- 2. Then import into bor_db:
--    mysql -u root -p bor_db < communities_export.sql
--
-- Or use this all-in-one command:
--    mysqldump -u root -p phonebook_db communities | mysql -u root -p bor_db

-- Alternative: Direct copy within MySQL
-- Run this script directly in MySQL:

-- Ensure we're using the target database
USE bor_db;

-- Drop table if you want a clean migration (uncomment if needed)
-- DROP TABLE IF EXISTS communities;

-- Create table structure by copying from source
CREATE TABLE IF NOT EXISTS communities LIKE phonebook_db.communities;

-- Copy all data from source to target
INSERT IGNORE INTO communities 
SELECT * FROM phonebook_db.communities;

-- Show migration results
SELECT 'Migration Complete!' as Status;
SELECT COUNT(*) as 'Total Records Migrated' FROM communities;