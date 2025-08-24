-- =============================================
-- Direct Import Names from CSV with Advanced Cleaning
-- Handles all cleaning within SQL without external scripts
-- =============================================

USE bor_db;

-- =============================================
-- Configuration Variables
-- =============================================
SET @first_names_file = 'C:/path/to/firstnames.csv';
SET @last_names_file = 'C:/path/to/lastnames.csv';

-- =============================================
-- Step 1: Create staging table for raw import
-- =============================================
DROP TABLE IF EXISTS temp_raw_import;
CREATE TEMPORARY TABLE temp_raw_import (
    raw_line VARCHAR(500),
    name_type VARCHAR(10)
);

-- =============================================
-- Step 2: Import FIRST NAMES raw data
-- =============================================
SET @sql = CONCAT('LOAD DATA LOCAL INFILE \'', @first_names_file, '\' ',
    'INTO TABLE temp_raw_import ',
    'FIELDS TERMINATED BY \'\' ',
    'LINES TERMINATED BY \'\\n\' ',
    'IGNORE 2 LINES ',
    '(@line) ',
    'SET raw_line = @line, name_type = \'first\'');

PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- =============================================
-- Step 3: Parse raw data into structured format
-- =============================================
DROP TABLE IF EXISTS temp_parsed_names;
CREATE TEMPORARY TABLE temp_parsed_names (
    raw_name VARCHAR(255),
    name_type VARCHAR(10),
    name_count INT
);

-- Parse tab or space-separated format
INSERT INTO temp_parsed_names (raw_name, name_type, name_count)
SELECT 
    TRIM(SUBSTRING_INDEX(raw_line, CHAR(9), 1)) AS raw_name,  -- Tab separated
    name_type,
    CAST(TRIM(SUBSTRING_INDEX(raw_line, CHAR(9), -1)) AS UNSIGNED) AS name_count
FROM temp_raw_import
WHERE raw_line REGEXP '[0-9]+$'  -- Has a count at the end
  AND raw_line NOT LIKE '---%'   -- Not a separator line
UNION ALL
SELECT 
    TRIM(REGEXP_SUBSTR(raw_line, '^[^0-9]+')) AS raw_name,  -- Space separated
    name_type,
    CAST(TRIM(REGEXP_SUBSTR(raw_line, '[0-9]+$')) AS UNSIGNED) AS name_count
FROM temp_raw_import
WHERE raw_line REGEXP '^[^0-9]+[[:space:]]+[0-9]+$'  -- Format: NAME SPACES COUNT
  AND raw_line NOT LIKE '---%';

-- =============================================
-- Step 4: Clean names function-like processing
-- =============================================
DROP TABLE IF EXISTS temp_clean_names;
CREATE TEMPORARY TABLE temp_clean_names (
    clean_name VARCHAR(255),
    name_type VARCHAR(10),
    name_count INT
);

-- Process each name with cleaning rules
INSERT INTO temp_clean_names (clean_name, name_type, name_count)
SELECT 
    LOWER(TRIM(
        -- Remove parenthetical content
        REGEXP_REPLACE(
            -- Remove common titles and their variations
            REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(
            REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(
            REPLACE(REPLACE(REPLACE(REPLACE(
                raw_name,
                'MR.', ''), 'MRS.', ''), 'MS.', ''), 'MISS', ''),
                'DR.', ''), 'PROF.', ''), 'REV.', ''), 'FR.', ''),
                'SR.', ''), 'JR.', ''), 'SIR', ''), 'LORD', ''),
                'LADY', ''), 'MASTER', ''), 'MISTER', ''), 'MADAM', ''),
                'MR ', ''), 'MRS ', ''), 'MS ', ''), 'DR ', ''),
            '\\([^)]*\\)', ''
        )
    )) AS clean_name,
    name_type,
    name_count
FROM temp_parsed_names
WHERE raw_name IS NOT NULL
  AND LENGTH(raw_name) > 1
  -- Exclude obvious business terms
  AND LOWER(raw_name) NOT IN (
    'ltd', 'inc', 'corp', 'llc', 'company', 'corporation',
    'limited', 'incorporated', 'enterprises', 'group', 'holdings',
    'mgr', 'manager', 'supervisor', 'director', 'president',
    'trainer', 'admin', 'administrator', 'office', 'center',
    'call', 'fax', 'phone', 'tel', 'mobile', 'cell',
    'canada', 'health', 'school', 'community', 'foundation',
    'services', 'international', 'global', 'solutions', 'consulting',
    'management', 'partners', 'associates', 'industries', 'systems'
  )
  -- Exclude entries with business indicators
  AND LOWER(raw_name) NOT LIKE '%ltd%'
  AND LOWER(raw_name) NOT LIKE '%inc%'
  AND LOWER(raw_name) NOT LIKE '%corp%'
  AND LOWER(raw_name) NOT LIKE '%(mgr)%'
  AND LOWER(raw_name) NOT LIKE '%manager%'
  AND LOWER(raw_name) NOT LIKE '%office%'
  AND LOWER(raw_name) NOT LIKE '%company%';

-- =============================================
-- Step 5: Handle hyphenated names
-- =============================================

-- Add first part of hyphenated names
INSERT INTO temp_clean_names (clean_name, name_type, name_count)
SELECT 
    LOWER(TRIM(SUBSTRING_INDEX(clean_name, '-', 1))) AS first_part,
    name_type,
    name_count
FROM temp_clean_names
WHERE clean_name LIKE '%-%'
  AND LENGTH(SUBSTRING_INDEX(clean_name, '-', 1)) > 1
  AND SUBSTRING_INDEX(clean_name, '-', 1) NOT IN (
    SELECT clean_name FROM temp_clean_names 
    WHERE clean_name = SUBSTRING_INDEX(clean_name, '-', 1)
  );

-- Add second part of hyphenated names
INSERT INTO temp_clean_names (clean_name, name_type, name_count)
SELECT 
    LOWER(TRIM(SUBSTRING_INDEX(clean_name, '-', -1))) AS second_part,
    name_type,
    name_count
FROM temp_clean_names
WHERE clean_name LIKE '%-%'
  AND LENGTH(SUBSTRING_INDEX(clean_name, '-', -1)) > 1
  AND SUBSTRING_INDEX(clean_name, '-', -1) NOT IN (
    SELECT clean_name FROM temp_clean_names 
    WHERE clean_name = SUBSTRING_INDEX(clean_name, '-', -1)
  );

-- Remove original hyphenated entries
DELETE FROM temp_clean_names
WHERE clean_name LIKE '%-%';

-- =============================================
-- Step 6: Handle multi-word entries
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
  AND SUBSTRING_INDEX(clean_name, ' ', 1) NOT REGEXP '^[a-z]$'
  AND SUBSTRING_INDEX(clean_name, ' ', 1) NOT IN (
    SELECT clean_name FROM temp_clean_names 
    WHERE clean_name = SUBSTRING_INDEX(clean_name, ' ', 1)
  );

-- Extract last word from multi-word entries
INSERT INTO temp_clean_names (clean_name, name_type, name_count)
SELECT 
    LOWER(TRIM(SUBSTRING_INDEX(clean_name, ' ', -1))) AS last_word,
    name_type,
    name_count
FROM temp_clean_names
WHERE clean_name LIKE '% %'
  AND LENGTH(SUBSTRING_INDEX(clean_name, ' ', -1)) > 2
  AND SUBSTRING_INDEX(clean_name, ' ', -1) NOT REGEXP '^[a-z]$'
  AND LOWER(SUBSTRING_INDEX(clean_name, ' ', -1)) NOT IN ('jr', 'sr', 'ii', 'iii', 'iv', 'esq', 'phd', 'md')
  AND SUBSTRING_INDEX(clean_name, ' ', -1) NOT IN (
    SELECT clean_name FROM temp_clean_names 
    WHERE clean_name = SUBSTRING_INDEX(clean_name, ' ', -1)
  );

-- Remove original multi-word entries
DELETE FROM temp_clean_names
WHERE clean_name LIKE '% %';

-- =============================================
-- Step 7: Final cleanup
-- =============================================

-- Remove invalid entries
DELETE FROM temp_clean_names
WHERE clean_name IS NULL
   OR clean_name = ''
   OR LENGTH(clean_name) < 2
   OR clean_name REGEXP '^[0-9]+$'  -- All numbers
   OR clean_name IN ('junior', 'senior', 'ii', 'iii', 'iv', 'esq', 'phd', 'md', 'dds', 'cpa');

-- Remove apostrophes and clean special characters
UPDATE temp_clean_names
SET clean_name = REPLACE(REPLACE(clean_name, '''', ''), '.', '')
WHERE clean_name LIKE '%''%' OR clean_name LIKE '%.%';

-- =============================================
-- Step 8: Aggregate duplicate names
-- =============================================
DROP TABLE IF EXISTS temp_aggregated_names;
CREATE TEMPORARY TABLE temp_aggregated_names AS
SELECT 
    clean_name,
    name_type,
    SUM(name_count) AS total_count
FROM temp_clean_names
WHERE clean_name IS NOT NULL 
  AND clean_name != ''
  AND LENGTH(clean_name) >= 2
GROUP BY clean_name, name_type;

-- =============================================
-- Step 9: Show preview before import
-- =============================================
SELECT 'Preview of names to import (Top 20)' AS Status;
SELECT * FROM temp_aggregated_names 
ORDER BY total_count DESC 
LIMIT 20;

SELECT 
    'Import Summary' AS Status,
    name_type,
    COUNT(*) AS unique_names,
    SUM(total_count) AS total_occurrences
FROM temp_aggregated_names
GROUP BY name_type;

-- =============================================
-- Step 10: Insert into final names table
-- =============================================
INSERT INTO names (name_lower, name_type, name_count, last_seen, created_at, updated_at)
SELECT 
    clean_name,
    name_type,
    total_count,
    NOW(),
    NOW(),
    NOW()
FROM temp_aggregated_names
ON DUPLICATE KEY UPDATE
    name_count = name_count + VALUES(name_count),
    last_seen = NOW(),
    updated_at = NOW();

-- =============================================
-- Step 11: Import LAST NAMES (repeat process)
-- =============================================

-- Clear temp tables
TRUNCATE TABLE temp_raw_import;
TRUNCATE TABLE temp_parsed_names;
TRUNCATE TABLE temp_clean_names;

-- Load last names file
SET @sql = CONCAT('LOAD DATA LOCAL INFILE \'', @last_names_file, '\' ',
    'INTO TABLE temp_raw_import ',
    'FIELDS TERMINATED BY \'\' ',
    'LINES TERMINATED BY \'\\n\' ',
    'IGNORE 2 LINES ',
    '(@line) ',
    'SET raw_line = @line, name_type = \'last\'');

PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- Repeat parsing and cleaning for last names
INSERT INTO temp_parsed_names (raw_name, name_type, name_count)
SELECT 
    TRIM(SUBSTRING_INDEX(raw_line, CHAR(9), 1)) AS raw_name,
    name_type,
    CAST(TRIM(SUBSTRING_INDEX(raw_line, CHAR(9), -1)) AS UNSIGNED) AS name_count
FROM temp_raw_import
WHERE raw_line REGEXP '[0-9]+$'
  AND raw_line NOT LIKE '---%'
UNION ALL
SELECT 
    TRIM(REGEXP_SUBSTR(raw_line, '^[^0-9]+')) AS raw_name,
    name_type,
    CAST(TRIM(REGEXP_SUBSTR(raw_line, '[0-9]+$')) AS UNSIGNED) AS name_count
FROM temp_raw_import
WHERE raw_line REGEXP '^[^0-9]+[[:space:]]+[0-9]+$'
  AND raw_line NOT LIKE '---%';

-- Clean last names
INSERT INTO temp_clean_names (clean_name, name_type, name_count)
SELECT 
    LOWER(TRIM(
        REGEXP_REPLACE(
            REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(
            REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(
            REPLACE(REPLACE(REPLACE(REPLACE(
                raw_name,
                'MR.', ''), 'MRS.', ''), 'MS.', ''), 'MISS', ''),
                'DR.', ''), 'PROF.', ''), 'REV.', ''), 'FR.', ''),
                'SR.', ''), 'JR.', ''), 'SIR', ''), 'LORD', ''),
                'LADY', ''), 'MASTER', ''), 'MISTER', ''), 'MADAM', ''),
                'MR ', ''), 'MRS ', ''), 'MS ', ''), 'DR ', ''),
            '\\([^)]*\\)', ''
        )
    )) AS clean_name,
    name_type,
    name_count
FROM temp_parsed_names
WHERE raw_name IS NOT NULL
  AND LENGTH(raw_name) > 1
  AND LOWER(raw_name) NOT IN (
    'ltd', 'inc', 'corp', 'llc', 'company', 'corporation',
    'limited', 'incorporated', 'enterprises', 'group', 'holdings',
    'mgr', 'manager', 'supervisor', 'director', 'president',
    'trainer', 'admin', 'administrator', 'office', 'center',
    'call', 'fax', 'phone', 'tel', 'mobile', 'cell'
  )
  AND LOWER(raw_name) NOT LIKE '%ltd%'
  AND LOWER(raw_name) NOT LIKE '%inc%'
  AND LOWER(raw_name) NOT LIKE '%corp%';

-- Process hyphenated last names
INSERT INTO temp_clean_names (clean_name, name_type, name_count)
SELECT 
    LOWER(TRIM(SUBSTRING_INDEX(clean_name, '-', 1))),
    name_type,
    name_count
FROM temp_clean_names
WHERE clean_name LIKE '%-%'
  AND LENGTH(SUBSTRING_INDEX(clean_name, '-', 1)) > 1;

INSERT INTO temp_clean_names (clean_name, name_type, name_count)
SELECT 
    LOWER(TRIM(SUBSTRING_INDEX(clean_name, '-', -1))),
    name_type,
    name_count
FROM temp_clean_names
WHERE clean_name LIKE '%-%'
  AND LENGTH(SUBSTRING_INDEX(clean_name, '-', -1)) > 1;

DELETE FROM temp_clean_names WHERE clean_name LIKE '%-%';

-- Aggregate last names
TRUNCATE TABLE temp_aggregated_names;
INSERT INTO temp_aggregated_names
SELECT 
    clean_name,
    name_type,
    SUM(name_count) AS total_count
FROM temp_clean_names
WHERE clean_name IS NOT NULL 
  AND clean_name != ''
  AND LENGTH(clean_name) >= 2
GROUP BY clean_name, name_type;

-- Insert last names into final table
INSERT INTO names (name_lower, name_type, name_count, last_seen, created_at, updated_at)
SELECT 
    clean_name,
    name_type,
    total_count,
    NOW(),
    NOW(),
    NOW()
FROM temp_aggregated_names
ON DUPLICATE KEY UPDATE
    name_count = name_count + VALUES(name_count),
    last_seen = NOW(),
    updated_at = NOW();

-- =============================================
-- Step 12: Mark names that can be both first and last
-- =============================================
UPDATE names n1
INNER JOIN names n2 
    ON n1.name_lower = n2.name_lower
    AND n1.name_type = 'first'
    AND n2.name_type = 'last'
SET n1.name_type = 'both',
    n1.name_count = n1.name_count + n2.name_count;

DELETE n2 FROM names n1
INNER JOIN names n2
    ON n1.name_lower = n2.name_lower
    AND n1.name_type = 'both'
    AND n2.name_type = 'last';

-- =============================================
-- Step 13: Final report
-- =============================================
SELECT 
    'IMPORT COMPLETE' AS Status,
    name_type,
    COUNT(*) AS unique_names,
    SUM(name_count) AS total_occurrences
FROM names
GROUP BY name_type
WITH ROLLUP;

-- Show top names
SELECT 'Top 15 Names by Type' AS Report;
SELECT name_lower, name_type, name_count
FROM names
ORDER BY name_count DESC
LIMIT 15;

-- Clean up
DROP TEMPORARY TABLE IF EXISTS temp_raw_import;
DROP TEMPORARY TABLE IF EXISTS temp_parsed_names;
DROP TEMPORARY TABLE IF EXISTS temp_clean_names;
DROP TEMPORARY TABLE IF EXISTS temp_aggregated_names;