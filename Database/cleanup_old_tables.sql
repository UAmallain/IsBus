-- =============================================
-- Cleanup Script: Remove old tables after migration to word_data
-- IMPORTANT: Only run this AFTER verifying word_data has all data!
-- =============================================

USE bor_db;

-- =============================================
-- Step 1: Verify data migration was successful
-- =============================================
SELECT 'Data Verification Before Cleanup' AS Report;

-- Check word_data has data
SELECT 
    'word_data table' AS table_name,
    COUNT(DISTINCT word_lower) AS unique_words,
    COUNT(*) AS total_records,
    SUM(word_count) AS total_occurrences
FROM word_data;

-- Compare with old tables
SELECT 
    'names table (old)' AS table_name,
    COUNT(DISTINCT name_lower) AS unique_words,
    COUNT(*) AS total_records,
    SUM(name_count) AS total_occurrences
FROM names;

SELECT 
    'words table (old)' AS table_name,
    COUNT(DISTINCT word_lower) AS unique_words,
    COUNT(*) AS total_records,
    SUM(word_count) AS total_occurrences
FROM words;

-- =============================================
-- Step 2: Backup old tables (just in case)
-- =============================================
-- Create backup tables with timestamp
SET @backup_date = DATE_FORMAT(NOW(), '%Y%m%d_%H%i%s');

-- Backup names table
SET @sql = CONCAT('CREATE TABLE names_backup_', @backup_date, ' AS SELECT * FROM names');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- Backup words table
SET @sql = CONCAT('CREATE TABLE words_backup_', @backup_date, ' AS SELECT * FROM words');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SELECT CONCAT('Backup tables created: names_backup_', @backup_date, ' and words_backup_', @backup_date) AS Status;

-- =============================================
-- Step 3: Drop old tables
-- =============================================
-- Comment out these lines if you want to keep the old tables for now
-- Uncomment when ready to delete

-- DROP TABLE IF EXISTS names;
-- DROP TABLE IF EXISTS words;

-- SELECT 'Old tables dropped' AS Status;

-- =============================================
-- Step 4: Drop old temporary tables if they exist
-- =============================================
DROP TABLE IF EXISTS temp_name_import;
DROP TABLE IF EXISTS temp_clean_names;
DROP TABLE IF EXISTS temp_aggregated_names;
DROP TABLE IF EXISTS temp_final_names;
DROP TABLE IF EXISTS temp_parsed_names;
DROP TABLE IF EXISTS temp_raw_import;
DROP TABLE IF EXISTS temp_batch_test;
DROP TABLE IF EXISTS false_first_names;
DROP TABLE IF EXISTS false_last_names;

SELECT 'Temporary tables cleaned up' AS Status;

-- =============================================
-- Step 5: Show final database structure
-- =============================================
SELECT 'Final Database Structure' AS Report;

SELECT 
    TABLE_NAME,
    TABLE_ROWS,
    ROUND(DATA_LENGTH / 1024 / 1024, 2) AS data_size_mb,
    ROUND(INDEX_LENGTH / 1024 / 1024, 2) AS index_size_mb
FROM information_schema.TABLES
WHERE TABLE_SCHEMA = 'bor_db'
    AND TABLE_TYPE = 'BASE TABLE'
ORDER BY DATA_LENGTH DESC;

-- =============================================
-- Step 6: Optimize the new table
-- =============================================
OPTIMIZE TABLE word_data;

SELECT 'Database cleanup complete!' AS Status;

-- =============================================
-- Note: To actually delete the old tables, uncomment the DROP statements in Step 3
-- The backup tables can be deleted later with:
-- DROP TABLE names_backup_YYYYMMDD_HHMMSS;
-- DROP TABLE words_backup_YYYYMMDD_HHMMSS;
-- =============================================