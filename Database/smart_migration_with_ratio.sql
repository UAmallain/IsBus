-- =============================================
-- Smart Migration: Uses ratio-based logic to properly categorize words
-- =============================================

USE bor_db;

-- =============================================
-- Step 1: Migrate TRUE first names only
-- Excludes obvious business words miscategorized as names
-- =============================================

INSERT INTO bor_db.names (name_lower, name_type, name_count, last_seen, created_at, updated_at)
SELECT 
    fn.word_lower AS name_lower,
    'first' AS name_type,
    fn.count AS name_count,
    fn.last_seen AS last_seen,
    fn.first_seen AS created_at,
    fn.last_seen AS updated_at
FROM phonebook_db.word_scores fn
LEFT JOIN phonebook_db.word_scores bk 
    ON fn.word_lower = bk.word_lower AND bk.category = 'business_keyword'
WHERE fn.category = 'first_name'
  -- Exclude if it's obviously a business word
  AND fn.word_lower NOT IN ('ltd', 'inc', 'corp', 'llc', 'call', 'no', 'charge', 
                            'fax', 'phone', 'tel', 'office', 'services', 'company',
                            'canada', 'health', 'school', 'community', 'centre', 'center',
                            'sans', 'frais', 'composez', 'téléc', 'aliant')
  -- Exclude if business usage is way higher than name usage
  AND (
    bk.word_lower IS NULL  -- Not a business keyword at all
    OR bk.count < 100 * fn.count  -- Business use is not 100x name use
    OR fn.count > 50  -- Substantial name usage regardless
  )
ON DUPLICATE KEY UPDATE
    bor_db.names.name_count = bor_db.names.name_count + VALUES(name_count),
    bor_db.names.last_seen = VALUES(last_seen),
    bor_db.names.updated_at = CURRENT_TIMESTAMP;

SELECT 
    'True First Names Migrated' AS Status,
    COUNT(*) AS count
FROM bor_db.names
WHERE name_type = 'first';

-- =============================================
-- Step 2: Migrate TRUE last names only
-- =============================================

INSERT INTO bor_db.names (name_lower, name_type, name_count, last_seen, created_at, updated_at)
SELECT 
    ln.word_lower AS name_lower,
    'last' AS name_type,
    ln.count AS name_count,
    ln.last_seen AS last_seen,
    ln.first_seen AS created_at,
    ln.last_seen AS updated_at
FROM phonebook_db.word_scores ln
LEFT JOIN phonebook_db.word_scores bk 
    ON ln.word_lower = bk.word_lower AND bk.category = 'business_keyword'
WHERE ln.category = 'last_name'
  -- Exclude obvious business words
  AND ln.word_lower NOT IN ('ltd', 'inc', 'corp', 'llc', 'call', 'no', 'charge', 
                            'fax', 'phone', 'tel', 'office', 'services', 'company',
                            'canada', 'health', 'school', 'community', 'centre', 'center',
                            'sans', 'frais', 'composez', 'téléc', 'aliant')
  -- Exclude if business usage is way higher
  AND (
    bk.word_lower IS NULL  -- Not a business keyword at all
    OR bk.count < 100 * ln.count  -- Business use is not 100x name use
    OR ln.count > 50  -- Substantial name usage regardless
  )
ON DUPLICATE KEY UPDATE
    bor_db.names.name_count = bor_db.names.name_count + VALUES(name_count),
    bor_db.names.last_seen = VALUES(last_seen),
    bor_db.names.updated_at = CURRENT_TIMESTAMP;

SELECT 
    'True Last Names Migrated' AS Status,
    COUNT(*) AS count
FROM bor_db.names
WHERE name_type = 'last';

-- =============================================
-- Step 3: Identify and mark names that can be both first and last
-- =============================================

UPDATE bor_db.names n1
INNER JOIN bor_db.names n2 
    ON n1.name_lower = n2.name_lower
    AND n1.name_type = 'first'
    AND n2.name_type = 'last'
SET n1.name_type = 'both',
    n1.name_count = n1.name_count + n2.name_count;

DELETE n2 FROM bor_db.names n1
INNER JOIN bor_db.names n2
    ON n1.name_lower = n2.name_lower
    AND n1.name_type = 'both'
    AND n2.name_type = 'last';

-- =============================================
-- Step 4: Migrate business keywords (ALL of them, including those that are also legitimate names)
-- The ratio check happens at runtime in the API
-- =============================================

INSERT INTO bor_db.words (word_lower, word_count, last_seen, created_at, updated_at)
SELECT 
    word_lower,
    count,
    last_seen,
    first_seen,
    last_seen
FROM phonebook_db.word_scores
WHERE category = 'business_keyword'
ON DUPLICATE KEY UPDATE
    bor_db.words.word_count = bor_db.words.word_count + VALUES(word_count),
    bor_db.words.last_seen = VALUES(last_seen),
    bor_db.words.updated_at = CURRENT_TIMESTAMP;

SELECT 
    'Business Keywords Migrated' AS Status,
    COUNT(*) AS count
FROM bor_db.words;

-- =============================================
-- Step 5: Final Summary
-- =============================================

SELECT '=== FINAL MIGRATION SUMMARY ===' AS Report;

-- Names breakdown
SELECT 
    'Names Table' AS Table_Name,
    name_type,
    COUNT(*) AS records,
    SUM(name_count) AS total_occurrences
FROM bor_db.names
GROUP BY name_type
WITH ROLLUP;

-- Words summary
SELECT 
    'Words Table' AS Table_Name,
    COUNT(*) AS records,
    SUM(word_count) AS total_occurrences,
    MAX(word_count) AS max_count
FROM bor_db.words;

-- Show examples of words that are in BOTH tables (legitimate dual use)
SELECT 
    'Words in BOTH Tables (Legitimate Dual Use)' AS Report;

SELECT 
    w.word_lower,
    w.word_count AS business_usage,
    n.name_count AS name_usage,
    n.name_type,
    ROUND(w.word_count / n.name_count, 2) AS business_to_name_ratio
FROM bor_db.words w
INNER JOIN bor_db.names n ON w.word_lower = n.name_lower
WHERE n.name_count > 50  -- Legitimate name usage
ORDER BY n.name_count DESC
LIMIT 20;

-- Show pure business words (not in names table)
SELECT 
    'Pure Business Words (Top 20)' AS Report;

SELECT 
    w.word_lower,
    w.word_count
FROM bor_db.words w
LEFT JOIN bor_db.names n ON w.word_lower = n.name_lower
WHERE n.name_lower IS NULL
ORDER BY w.word_count DESC
LIMIT 20;