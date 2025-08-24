-- =============================================
-- Import Names from CSV with Cleaning
-- Handles titles, hyphenated names, and business words
-- =============================================

USE bor_db;

-- =============================================
-- Step 1: Create temporary staging table
-- =============================================
DROP TABLE IF EXISTS temp_name_import;
CREATE TEMPORARY TABLE temp_name_import (
    raw_name VARCHAR(255),
    name_type VARCHAR(10),
    name_count INT DEFAULT 1
);

-- =============================================
-- Step 2: Load raw data from CSV
-- MODIFY THE FILE PATH FOR YOUR SYSTEM
-- =============================================

-- For FIRST NAMES file
-- Expected format: NAME    COUNT (tab or multiple spaces)
LOAD DATA LOCAL INFILE 'C:/path/to/firstnames.csv'
INTO TABLE temp_name_import
FIELDS TERMINATED BY '\t'  -- Change to ',' if comma-delimited
LINES TERMINATED BY '\n'   -- Change to '\r\n' for Windows line endings
IGNORE 2 LINES             -- Skip header and separator line
(@name, @count)
SET 
    raw_name = TRIM(@name),
    name_type = 'first',
    name_count = IF(@count > 0, @count, 1);

-- Show sample of imported data
SELECT 'Sample of imported raw data' AS Status;
SELECT * FROM temp_name_import LIMIT 10;

-- =============================================
-- Step 3: Clean the names
-- =============================================

-- Create cleaned names table
DROP TABLE IF EXISTS temp_clean_names;
CREATE TEMPORARY TABLE temp_clean_names (
    clean_name VARCHAR(255),
    name_type VARCHAR(10),
    name_count INT
);

-- Insert cleaned names (removing titles, parentheses, etc.)
INSERT INTO temp_clean_names (clean_name, name_type, name_count)
SELECT 
    LOWER(TRIM(
        -- Remove common titles
        REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(
        REPLACE(REPLACE(REPLACE(REPLACE(
            -- Remove parenthetical content first
            REGEXP_REPLACE(raw_name, '\\([^)]*\\)', ''),
            'MR.', ''), 'MRS.', ''), 'MS.', ''), 'MISS', ''),
            'DR.', ''), 'PROF.', ''), 'REV.', ''),
            'MR ', ''), 'MRS ', ''), 'MS ', ''), 'DR ', '')
    )) AS clean_name,
    name_type,
    name_count
FROM temp_name_import
WHERE raw_name IS NOT NULL
  AND LENGTH(raw_name) > 1
  -- Exclude obvious business terms
  AND LOWER(raw_name) NOT IN (
    'ltd', 'inc', 'corp', 'llc', 'company', 'corporation',
    'limited', 'incorporated', 'enterprises', 'group', 'holdings',
    'mgr', 'manager', 'supervisor', 'director', 'president',
    'trainer', 'admin', 'administrator', 'office', 'center',
    'call', 'fax', 'phone', 'tel', 'mobile', 'cell'
  )
  -- Exclude entries with business indicators
  AND LOWER(raw_name) NOT LIKE '%ltd%'
  AND LOWER(raw_name) NOT LIKE '%inc%'
  AND LOWER(raw_name) NOT LIKE '%corp%'
  AND LOWER(raw_name) NOT LIKE '%(mgr)%'
  AND LOWER(raw_name) NOT LIKE '%manager%'
  AND LOWER(raw_name) NOT LIKE '%office%';

-- =============================================
-- Step 4: Handle hyphenated names
-- Split them into separate entries
-- =============================================

-- Add first part of hyphenated names
INSERT INTO temp_clean_names (clean_name, name_type, name_count)
SELECT 
    LOWER(TRIM(SUBSTRING_INDEX(clean_name, '-', 1))) AS first_part,
    name_type,
    name_count
FROM temp_clean_names
WHERE clean_name LIKE '%-%'
  AND LENGTH(SUBSTRING_INDEX(clean_name, '-', 1)) > 1;

-- Add second part of hyphenated names
INSERT INTO temp_clean_names (clean_name, name_type, name_count)
SELECT 
    LOWER(TRIM(SUBSTRING_INDEX(clean_name, '-', -1))) AS second_part,
    name_type,
    name_count
FROM temp_clean_names
WHERE clean_name LIKE '%-%'
  AND LENGTH(SUBSTRING_INDEX(clean_name, '-', -1)) > 1;

-- Remove the original hyphenated entries
DELETE FROM temp_clean_names
WHERE clean_name LIKE '%-%';

-- =============================================
-- Step 5: Handle multiple names in one entry
-- (like "SUSAN M HUTTON" -> extract "susan" and "hutton")
-- =============================================

-- Extract first word from multi-word entries
INSERT INTO temp_clean_names (clean_name, name_type, name_count)
SELECT 
    LOWER(TRIM(SUBSTRING_INDEX(clean_name, ' ', 1))) AS first_word,
    name_type,
    name_count
FROM temp_clean_names
WHERE clean_name LIKE '% %'
  AND LENGTH(SUBSTRING_INDEX(clean_name, ' ', 1)) > 2
  -- Don't include single letters
  AND SUBSTRING_INDEX(clean_name, ' ', 1) NOT REGEXP '^[a-z]$';

-- Extract last word from multi-word entries
INSERT INTO temp_clean_names (clean_name, name_type, name_count)
SELECT 
    LOWER(TRIM(SUBSTRING_INDEX(clean_name, ' ', -1))) AS last_word,
    name_type,
    name_count
FROM temp_clean_names
WHERE clean_name LIKE '% %'
  AND LENGTH(SUBSTRING_INDEX(clean_name, ' ', -1)) > 2
  -- Don't include single letters or suffixes
  AND SUBSTRING_INDEX(clean_name, ' ', -1) NOT REGEXP '^[a-z]$'
  AND LOWER(SUBSTRING_INDEX(clean_name, ' ', -1)) NOT IN ('jr', 'sr', 'ii', 'iii', 'iv');

-- Remove the original multi-word entries
DELETE FROM temp_clean_names
WHERE clean_name LIKE '% %';

-- =============================================
-- Step 6: Final cleanup and aggregation
-- =============================================

-- Remove any remaining invalid entries
DELETE FROM temp_clean_names
WHERE clean_name IS NULL
   OR clean_name = ''
   OR LENGTH(clean_name) < 2
   OR clean_name REGEXP '^[0-9]+$'  -- All numbers
   OR clean_name IN ('junior', 'senior', 'ii', 'iii', 'iv', 'esq');

-- Aggregate counts for duplicate names
DROP TABLE IF EXISTS temp_final_names;
CREATE TEMPORARY TABLE temp_final_names AS
SELECT 
    clean_name,
    name_type,
    SUM(name_count) AS total_count
FROM temp_clean_names
GROUP BY clean_name, name_type;

-- Show what will be imported
SELECT 'Names to be imported (top 20)' AS Status;
SELECT * FROM temp_final_names 
ORDER BY total_count DESC 
LIMIT 20;

SELECT 
    'Import Summary' AS Status,
    COUNT(*) AS unique_names,
    SUM(total_count) AS total_occurrences
FROM temp_final_names;

-- =============================================
-- Step 7: Insert into final names table
-- =============================================

INSERT INTO names (name_lower, name_type, name_count, last_seen, created_at, updated_at)
SELECT 
    clean_name,
    name_type,
    total_count,
    NOW(),
    NOW(),
    NOW()
FROM temp_final_names
ON DUPLICATE KEY UPDATE
    name_count = name_count + VALUES(name_count),
    last_seen = NOW(),
    updated_at = NOW();

-- =============================================
-- Step 8: Show results
-- =============================================

SELECT 
    'Import Complete' AS Status,
    name_type,
    COUNT(*) AS unique_names,
    SUM(name_count) AS total_occurrences
FROM names
GROUP BY name_type;

-- Show top 15 names after import
SELECT 'Top 15 First Names After Import' AS Report;
SELECT name_lower, name_count
FROM names
WHERE name_type = 'first'
ORDER BY name_count DESC
LIMIT 15;

-- Clean up temporary tables
DROP TEMPORARY TABLE IF EXISTS temp_name_import;
DROP TEMPORARY TABLE IF EXISTS temp_clean_names;
DROP TEMPORARY TABLE IF EXISTS temp_final_names;