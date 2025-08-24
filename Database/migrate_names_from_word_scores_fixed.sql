-- =============================================
-- Migration Script: Copy names from phonebook_db.word_scores to bor_db.names
-- Based on actual table structure with 'category' column
-- =============================================

USE bor_db;

-- First, let's see what categories exist for names
SELECT DISTINCT category, COUNT(*) as count
FROM phonebook_db.word_scores
WHERE category IN ('first_name', 'last_name', 'surname', 'given_name', 'firstname', 'lastname')
   OR category LIKE '%name%'
GROUP BY category;

-- =============================================
-- Step 1: Migrate first names from word_scores
-- =============================================
INSERT INTO bor_db.names (name_lower, name_type, name_count, last_seen, created_at, updated_at)
SELECT 
    word_lower AS name_lower,
    'first' AS name_type,
    count AS name_count,
    last_seen AS last_seen,
    first_seen AS created_at,
    last_seen AS updated_at
FROM phonebook_db.word_scores
WHERE category = 'first_name'
   OR category = 'firstname' 
   OR category = 'given_name'
ON DUPLICATE KEY UPDATE
    bor_db.names.name_count = bor_db.names.name_count + VALUES(name_count),
    bor_db.names.last_seen = VALUES(last_seen),
    bor_db.names.updated_at = CURRENT_TIMESTAMP;

-- Show first names migration results
SELECT 
    'First Names Migrated' AS Status,
    COUNT(*) AS count,
    SUM(name_count) AS total_occurrences
FROM bor_db.names
WHERE name_type = 'first';

-- =============================================
-- Step 2: Migrate last names from word_scores
-- =============================================
INSERT INTO bor_db.names (name_lower, name_type, name_count, last_seen, created_at, updated_at)
SELECT 
    word_lower AS name_lower,
    'last' AS name_type,
    count AS name_count,
    last_seen AS last_seen,
    first_seen AS created_at,
    last_seen AS updated_at
FROM phonebook_db.word_scores
WHERE category = 'last_name'
   OR category = 'lastname'
   OR category = 'surname'
   OR category = 'family_name'
ON DUPLICATE KEY UPDATE
    bor_db.names.name_count = bor_db.names.name_count + VALUES(name_count),
    bor_db.names.last_seen = VALUES(last_seen),
    bor_db.names.updated_at = CURRENT_TIMESTAMP;

-- Show last names migration results
SELECT 
    'Last Names Migrated' AS Status,
    COUNT(*) AS count,
    SUM(name_count) AS total_occurrences
FROM bor_db.names
WHERE name_type = 'last';

-- =============================================
-- Step 3: Identify names that exist as both first and last
-- =============================================
UPDATE bor_db.names n1
INNER JOIN bor_db.names n2 
    ON n1.name_lower = n2.name_lower
    AND n1.name_type = 'first'
    AND n2.name_type = 'last'
SET n1.name_type = 'both',
    n1.name_count = n1.name_count + n2.name_count;

-- Delete the duplicate 'last' entry for names marked as 'both'
DELETE n2 FROM bor_db.names n1
INNER JOIN bor_db.names n2
    ON n1.name_lower = n2.name_lower
    AND n1.name_type = 'both'
    AND n2.name_type = 'last';

-- =============================================
-- Step 4: Migrate business keywords to words table
-- EXCLUDING any that are also first or last names
-- =============================================

-- First, show what business keywords are also names (for review)
SELECT 
    'Business Keywords That Are Also Names (EXCLUDED from words table)' AS Report;

SELECT 
    bk.word_lower,
    bk.count AS business_count,
    CASE 
        WHEN fn.word_lower IS NOT NULL AND ln.word_lower IS NOT NULL THEN 'Both first and last name'
        WHEN fn.word_lower IS NOT NULL THEN 'First name'
        WHEN ln.word_lower IS NOT NULL THEN 'Last name'
    END AS name_type
FROM phonebook_db.word_scores bk
LEFT JOIN phonebook_db.word_scores fn 
    ON bk.word_lower = fn.word_lower AND fn.category = 'first_name'
LEFT JOIN phonebook_db.word_scores ln 
    ON bk.word_lower = ln.word_lower AND ln.category = 'last_name'
WHERE bk.category = 'business_keyword'
  AND (fn.word_lower IS NOT NULL OR ln.word_lower IS NOT NULL)
ORDER BY bk.count DESC
LIMIT 20;

-- Now migrate ONLY business keywords that are NOT names
INSERT INTO bor_db.words (word_lower, word_count, last_seen, created_at, updated_at)
SELECT 
    bk.word_lower,
    bk.count,
    bk.last_seen,
    bk.first_seen,
    bk.last_seen
FROM phonebook_db.word_scores bk
WHERE bk.category = 'business_keyword'
  -- Exclude if it exists as a first name
  AND NOT EXISTS (
    SELECT 1 FROM phonebook_db.word_scores fn 
    WHERE fn.word_lower = bk.word_lower 
    AND fn.category = 'first_name'
  )
  -- Exclude if it exists as a last name
  AND NOT EXISTS (
    SELECT 1 FROM phonebook_db.word_scores ln 
    WHERE ln.word_lower = bk.word_lower 
    AND ln.category = 'last_name'
  )
ON DUPLICATE KEY UPDATE
    bor_db.words.word_count = bor_db.words.word_count + VALUES(word_count),
    bor_db.words.last_seen = VALUES(last_seen),
    bor_db.words.updated_at = CURRENT_TIMESTAMP;

-- Show count of business keywords migrated
SELECT 
    'Business Keywords Migrated (excluding names)' AS Status,
    COUNT(*) AS keywords_migrated
FROM bor_db.words;

-- =============================================
-- Step 5: Final migration summary
-- =============================================
SELECT 
    '=== MIGRATION SUMMARY ===' AS Report;

-- Names summary
SELECT 
    'Names Table Summary' AS Report,
    name_type,
    COUNT(*) AS total_names,
    SUM(name_count) AS total_occurrences,
    AVG(name_count) AS avg_count,
    MAX(name_count) AS max_count
FROM bor_db.names
GROUP BY name_type
WITH ROLLUP;

-- Words summary
SELECT 
    'Words Table Summary' AS Report,
    COUNT(*) AS total_words,
    SUM(word_count) AS total_occurrences,
    AVG(word_count) AS avg_count,
    MAX(word_count) AS max_count
FROM bor_db.words;

-- Top 15 most common first names
SELECT 
    '=== TOP 15 FIRST NAMES ===' AS Report;
    
SELECT 
    name_lower AS name,
    name_count AS occurrences
FROM bor_db.names
WHERE name_type IN ('first', 'both')
ORDER BY name_count DESC
LIMIT 15;

-- Top 15 most common last names
SELECT 
    '=== TOP 15 LAST NAMES ===' AS Report;
    
SELECT 
    name_lower AS name,
    name_count AS occurrences
FROM bor_db.names
WHERE name_type IN ('last', 'both')
ORDER BY name_count DESC
LIMIT 15;

-- Top 15 business keywords
SELECT 
    '=== TOP 15 BUSINESS KEYWORDS ===' AS Report;
    
SELECT 
    word_lower AS keyword,
    word_count AS occurrences
FROM bor_db.words
ORDER BY word_count DESC
LIMIT 15;

-- Show all available categories from source
SELECT 
    '=== ALL CATEGORIES IN SOURCE ===' AS Report;

SELECT 
    category,
    COUNT(*) as total_records,
    SUM(count) as total_occurrences
FROM phonebook_db.word_scores
GROUP BY category
ORDER BY total_occurrences DESC;