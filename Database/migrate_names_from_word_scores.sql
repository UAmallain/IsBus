-- =============================================
-- Migration Script: Copy names from phonebook_db.word_scores to bor_db.names
-- Transfers first_name and last_name data to the names table
-- =============================================

USE bor_db;

-- =============================================
-- Step 1: Migrate first names from word_scores
-- =============================================
INSERT INTO bor_db.names (name_lower, name_type, name_count, last_seen, created_at, updated_at)
SELECT 
    LOWER(TRIM(word)) AS name_lower,
    'first' AS name_type,
    score AS name_count,  -- Assuming score represents frequency/count
    NOW() AS last_seen,
    NOW() AS created_at,
    NOW() AS updated_at
FROM phonebook_db.word_scores
WHERE word_type = 'first_name'  -- Adjust this condition based on your actual column values
   OR word_type = 'firstname'
   OR word_type = 'first'
ON DUPLICATE KEY UPDATE
    bor_db.names.name_count = bor_db.names.name_count + VALUES(name_count),
    bor_db.names.last_seen = NOW(),
    bor_db.names.updated_at = CURRENT_TIMESTAMP;

-- Show first names migration results
SELECT 
    'First Names Migration' AS Status,
    COUNT(*) AS first_names_migrated
FROM bor_db.names
WHERE name_type = 'first';

-- =============================================
-- Step 2: Migrate last names from word_scores
-- =============================================
INSERT INTO bor_db.names (name_lower, name_type, name_count, last_seen, created_at, updated_at)
SELECT 
    LOWER(TRIM(word)) AS name_lower,
    'last' AS name_type,
    score AS name_count,  -- Assuming score represents frequency/count
    NOW() AS last_seen,
    NOW() AS created_at,
    NOW() AS updated_at
FROM phonebook_db.word_scores
WHERE word_type = 'last_name'  -- Adjust this condition based on your actual column values
   OR word_type = 'lastname'
   OR word_type = 'last'
   OR word_type = 'surname'
ON DUPLICATE KEY UPDATE
    bor_db.names.name_count = bor_db.names.name_count + VALUES(name_count),
    bor_db.names.last_seen = NOW(),
    bor_db.names.updated_at = CURRENT_TIMESTAMP;

-- Show last names migration results
SELECT 
    'Last Names Migration' AS Status,
    COUNT(*) AS last_names_migrated
FROM bor_db.names
WHERE name_type = 'last';

-- =============================================
-- Step 3: Handle any names that might be both first and last
-- (e.g., "Taylor", "Jordan", "Morgan" can be both)
-- =============================================
-- First, identify names that appear as both types
INSERT INTO bor_db.names (name_lower, name_type, name_count, last_seen, created_at, updated_at)
SELECT 
    LOWER(TRIM(fn.word)) AS name_lower,
    'both' AS name_type,
    fn.score + IFNULL(ln.score, 0) AS name_count,
    NOW() AS last_seen,
    NOW() AS created_at,
    NOW() AS updated_at
FROM phonebook_db.word_scores fn
INNER JOIN phonebook_db.word_scores ln 
    ON LOWER(TRIM(fn.word)) = LOWER(TRIM(ln.word))
WHERE (fn.word_type = 'first_name' OR fn.word_type = 'firstname' OR fn.word_type = 'first')
  AND (ln.word_type = 'last_name' OR ln.word_type = 'lastname' OR ln.word_type = 'last' OR ln.word_type = 'surname')
ON DUPLICATE KEY UPDATE
    bor_db.names.name_count = bor_db.names.name_count + VALUES(name_count),
    bor_db.names.last_seen = NOW(),
    bor_db.names.updated_at = CURRENT_TIMESTAMP;

-- =============================================
-- Step 4: Final migration summary
-- =============================================
SELECT 
    'Final Migration Summary' AS Report;

SELECT 
    name_type,
    COUNT(*) AS total_names,
    SUM(name_count) AS total_occurrences,
    AVG(name_count) AS avg_count,
    MAX(name_count) AS max_count
FROM bor_db.names
GROUP BY name_type;

-- Show top 10 most common first names
SELECT 
    'Top 10 First Names' AS Report;
    
SELECT 
    name_lower,
    name_count
FROM bor_db.names
WHERE name_type = 'first'
ORDER BY name_count DESC
LIMIT 10;

-- Show top 10 most common last names
SELECT 
    'Top 10 Last Names' AS Report;
    
SELECT 
    name_lower,
    name_count
FROM bor_db.names
WHERE name_type = 'last'
ORDER BY name_count DESC
LIMIT 10;

-- Show names that can be both first and last
SELECT 
    'Names That Can Be Both First and Last' AS Report;
    
SELECT 
    name_lower,
    name_count
FROM bor_db.names
WHERE name_type = 'both'
ORDER BY name_count DESC
LIMIT 20;

-- =============================================
-- Step 5: Data quality check - find any potential issues
-- =============================================
SELECT 
    'Data Quality Check' AS Report;

-- Check for very short names that might be errors
SELECT 
    'Short Names (potential issues)' AS Check_Type,
    name_lower,
    name_type,
    name_count
FROM bor_db.names
WHERE LENGTH(name_lower) <= 2
ORDER BY name_count DESC
LIMIT 10;

-- Check for names with special characters
SELECT 
    'Names with Special Characters' AS Check_Type,
    name_lower,
    name_type,
    name_count
FROM bor_db.names
WHERE name_lower REGEXP '[^a-z\-\']'
ORDER BY name_count DESC
LIMIT 10;