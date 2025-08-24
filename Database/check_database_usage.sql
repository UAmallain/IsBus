-- =============================================
-- Check Database Usage and Identify What Can Be Deleted
-- =============================================

USE bor_db;

-- =============================================
-- Show all tables and their sizes
-- =============================================
SELECT 'Current Database Tables and Sizes' AS Report;

SELECT 
    TABLE_NAME,
    TABLE_ROWS AS row_count,
    ROUND(DATA_LENGTH / 1024 / 1024, 2) AS data_mb,
    ROUND(INDEX_LENGTH / 1024 / 1024, 2) AS index_mb,
    ROUND((DATA_LENGTH + INDEX_LENGTH) / 1024 / 1024, 2) AS total_mb,
    CASE 
        WHEN TABLE_NAME = 'word_data' THEN 'KEEP - New unified table'
        WHEN TABLE_NAME = 'names' THEN 'DELETE - Migrated to word_data'
        WHEN TABLE_NAME = 'words' THEN 'DELETE - Migrated to word_data'
        WHEN TABLE_NAME LIKE '%backup%' THEN 'KEEP TEMPORARILY - Backup'
        WHEN TABLE_NAME LIKE 'temp_%' THEN 'DELETE - Temporary table'
        ELSE 'REVIEW'
    END AS recommendation
FROM information_schema.TABLES
WHERE TABLE_SCHEMA = 'bor_db'
    AND TABLE_TYPE = 'BASE TABLE'
ORDER BY (DATA_LENGTH + INDEX_LENGTH) DESC;

-- =============================================
-- Calculate space that can be reclaimed
-- =============================================
SELECT 'Space That Can Be Reclaimed' AS Report;

SELECT 
    'Tables to delete' AS category,
    GROUP_CONCAT(TABLE_NAME SEPARATOR ', ') AS tables,
    COUNT(*) AS table_count,
    ROUND(SUM(DATA_LENGTH + INDEX_LENGTH) / 1024 / 1024, 2) AS total_mb_to_reclaim
FROM information_schema.TABLES
WHERE TABLE_SCHEMA = 'bor_db'
    AND TABLE_TYPE = 'BASE TABLE'
    AND TABLE_NAME IN ('names', 'words')
GROUP BY category

UNION ALL

SELECT 
    'Temporary tables' AS category,
    GROUP_CONCAT(TABLE_NAME SEPARATOR ', ') AS tables,
    COUNT(*) AS table_count,
    ROUND(SUM(DATA_LENGTH + INDEX_LENGTH) / 1024 / 1024, 2) AS total_mb_to_reclaim
FROM information_schema.TABLES
WHERE TABLE_SCHEMA = 'bor_db'
    AND TABLE_TYPE = 'BASE TABLE'
    AND (TABLE_NAME LIKE 'temp_%' OR TABLE_NAME LIKE '%_temp')
GROUP BY category;

-- =============================================
-- Check for duplicate data between old and new tables
-- =============================================
SELECT 'Data Comparison' AS Report;

-- Compare unique words
SELECT 
    'Unique words in old tables' AS metric,
    (SELECT COUNT(DISTINCT name_lower) FROM names) + 
    (SELECT COUNT(DISTINCT word_lower) FROM words) AS old_count,
    (SELECT COUNT(DISTINCT word_lower) FROM word_data) AS new_count,
    CASE 
        WHEN (SELECT COUNT(DISTINCT word_lower) FROM word_data) > 0 THEN 'OK to delete old tables'
        ELSE 'DO NOT DELETE - Migration may have failed'
    END AS status;

-- =============================================
-- List of what can be safely deleted
-- =============================================
SELECT 'Safe to Delete' AS Report;

SELECT 
    TABLE_NAME,
    'DROP TABLE IF EXISTS ' || TABLE_NAME || ';' AS drop_command
FROM information_schema.TABLES
WHERE TABLE_SCHEMA = 'bor_db'
    AND TABLE_TYPE = 'BASE TABLE'
    AND (
        TABLE_NAME IN ('names', 'words')  -- Old tables that were migrated
        OR TABLE_NAME LIKE 'temp_%'        -- Temporary tables
        OR TABLE_NAME LIKE '%_temp'        -- Temporary tables
    );

-- =============================================
-- What should be kept
-- =============================================
SELECT 'Tables to Keep' AS Report;

SELECT 
    TABLE_NAME,
    CASE 
        WHEN TABLE_NAME = 'word_data' THEN 'Primary data table (new unified structure)'
        WHEN TABLE_NAME = 'word_context' THEN 'View for context mapping'
        WHEN TABLE_NAME LIKE '%backup%' THEN 'Backup table (can delete after verification)'
        ELSE 'Unknown - review before deleting'
    END AS reason_to_keep
FROM information_schema.TABLES
WHERE TABLE_SCHEMA = 'bor_db'
    AND TABLE_NAME NOT IN ('names', 'words')
    AND TABLE_NAME NOT LIKE 'temp_%'
    AND TABLE_NAME NOT LIKE '%_temp'
ORDER BY TABLE_NAME;