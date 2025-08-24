-- =====================================================
-- Simple Migration Script: Communities Table
-- From: phonebook_db to bor_db
-- =====================================================

-- Use target database
USE bor_db;

-- Drop and recreate to ensure clean migration
DROP TABLE IF EXISTS communities;

-- Create table with exact structure from source
CREATE TABLE communities LIKE phonebook_db.communities;

-- Copy all data
INSERT INTO communities SELECT * FROM phonebook_db.communities;

-- Show results
SELECT COUNT(*) as 'Records Migrated' FROM communities;
SELECT 'Migration completed successfully!' as Status;