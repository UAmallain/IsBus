-- Check existing tables in bor_db
USE bor_db;

-- List all tables
SHOW TABLES;

-- Check if any of our proposed tables already exist
SELECT TABLE_NAME, TABLE_ROWS 
FROM information_schema.tables 
WHERE table_schema = 'bor_db' 
AND table_name IN (
    'street_types', 
    'business_endings', 
    'province_codes', 
    'skip_words', 
    'road_indicators', 
    'suite_indicators',
    'business_context_words'
);

-- Check if we have any existing reference data tables
SELECT TABLE_NAME 
FROM information_schema.tables 
WHERE table_schema = 'bor_db' 
AND (
    TABLE_NAME LIKE '%street%' OR
    TABLE_NAME LIKE '%business%' OR
    TABLE_NAME LIKE '%province%' OR
    TABLE_NAME LIKE '%indicator%' OR
    TABLE_NAME LIKE '%skip%' OR
    TABLE_NAME LIKE '%suite%' OR
    TABLE_NAME LIKE '%road%'
);