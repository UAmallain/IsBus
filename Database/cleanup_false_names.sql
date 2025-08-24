-- =============================================
-- Cleanup Script: Remove business words miscategorized as names
-- Identifies words where business_count >> name_count
-- =============================================

USE phonebook_db;

-- =============================================
-- Step 1: Identify false "first names" (business words miscategorized)
-- Using ratio: if business_count > 100 * first_name_count, it's not a name
-- =============================================

DROP TEMPORARY TABLE IF EXISTS false_first_names;
CREATE TEMPORARY TABLE false_first_names AS
SELECT 
    bk.word_lower,
    bk.count AS business_count,
    fn.count AS name_count,
    ROUND(bk.count / fn.count, 2) AS ratio
FROM word_scores bk
INNER JOIN word_scores fn ON bk.word_lower = fn.word_lower
WHERE bk.category = 'business_keyword'
  AND fn.category = 'first_name'
  AND (
    -- Extreme ratio: business use is 100x more than name use
    (bk.count > 100 * fn.count)
    -- OR very low name count with high business count
    OR (fn.count <= 10 AND bk.count > 1000)
    -- OR obvious business terms
    OR bk.word_lower IN ('ltd', 'inc', 'corp', 'llc', 'call', 'no', 'charge', 
                         'fax', 'phone', 'tel', 'office', 'services', 'company',
                         'canada', 'health', 'school', 'community', 'centre', 'center',
                         'sans', 'frais', 'composez', 'téléc', 'aliant')
  );

-- Show what will be removed from first names
SELECT 
    'FALSE FIRST NAMES TO BE REMOVED' AS Report;
    
SELECT 
    word_lower,
    business_count,
    name_count,
    ratio,
    CASE 
        WHEN word_lower IN ('ltd', 'inc', 'corp', 'llc', 'call', 'no', 'charge', 'fax') THEN 'Obvious business term'
        WHEN ratio > 1000 THEN 'Extreme ratio (>1000:1)'
        WHEN ratio > 100 THEN 'High ratio (>100:1)'
        WHEN name_count <= 10 AND business_count > 1000 THEN 'Low name count, high business'
        ELSE 'Other'
    END AS reason
FROM false_first_names
ORDER BY business_count DESC;

-- =============================================
-- Step 2: Identify false "last names" (business words miscategorized)
-- =============================================

DROP TEMPORARY TABLE IF EXISTS false_last_names;
CREATE TEMPORARY TABLE false_last_names AS
SELECT 
    bk.word_lower,
    bk.count AS business_count,
    ln.count AS name_count,
    ROUND(bk.count / ln.count, 2) AS ratio
FROM word_scores bk
INNER JOIN word_scores ln ON bk.word_lower = ln.word_lower
WHERE bk.category = 'business_keyword'
  AND ln.category = 'last_name'
  AND (
    -- Extreme ratio: business use is 100x more than name use
    (bk.count > 100 * ln.count)
    -- OR very low name count with high business count
    OR (ln.count <= 10 AND bk.count > 1000)
    -- OR obvious business terms
    OR bk.word_lower IN ('ltd', 'inc', 'corp', 'llc', 'call', 'no', 'charge', 
                         'fax', 'phone', 'tel', 'office', 'services', 'company',
                         'canada', 'health', 'school', 'community', 'centre', 'center',
                         'sans', 'frais', 'composez', 'téléc', 'aliant')
  );

-- Show what will be removed from last names
SELECT 
    'FALSE LAST NAMES TO BE REMOVED' AS Report;
    
SELECT 
    word_lower,
    business_count,
    name_count,
    ratio,
    CASE 
        WHEN word_lower IN ('ltd', 'inc', 'corp', 'llc', 'call', 'no', 'charge', 'fax') THEN 'Obvious business term'
        WHEN ratio > 1000 THEN 'Extreme ratio (>1000:1)'
        WHEN ratio > 100 THEN 'High ratio (>100:1)'
        WHEN name_count <= 10 AND business_count > 1000 THEN 'Low name count, high business'
        ELSE 'Other'
    END AS reason
FROM false_last_names
ORDER BY business_count DESC;

-- =============================================
-- Step 3: Show legitimate names that ALSO happen to be business words
-- These should be kept in BOTH tables
-- =============================================

SELECT 
    'LEGITIMATE NAMES THAT ARE ALSO BUSINESS WORDS (KEEP IN BOTH)' AS Report;

-- First names that are legitimately both
SELECT 
    bk.word_lower,
    bk.count AS business_count,
    fn.count AS first_name_count,
    ROUND(bk.count / fn.count, 2) AS ratio
FROM word_scores bk
INNER JOIN word_scores fn ON bk.word_lower = fn.word_lower
WHERE bk.category = 'business_keyword'
  AND fn.category = 'first_name'
  AND bk.word_lower NOT IN (SELECT word_lower FROM false_first_names)
  AND fn.count > 50  -- Reasonable name usage
ORDER BY fn.count DESC
LIMIT 20;

-- =============================================
-- Step 4: DELETE false names from word_scores
-- =============================================

-- Count before deletion
SELECT 
    'BEFORE CLEANUP' AS Status,
    (SELECT COUNT(*) FROM word_scores WHERE category = 'first_name') AS total_first_names,
    (SELECT COUNT(*) FROM word_scores WHERE category = 'last_name') AS total_last_names;

-- Delete false first names
DELETE FROM word_scores
WHERE category = 'first_name'
  AND word_lower IN (SELECT word_lower FROM false_first_names);

-- Delete false last names  
DELETE FROM word_scores
WHERE category = 'last_name'
  AND word_lower IN (SELECT word_lower FROM false_last_names);

-- Count after deletion
SELECT 
    'AFTER CLEANUP' AS Status,
    (SELECT COUNT(*) FROM word_scores WHERE category = 'first_name') AS total_first_names,
    (SELECT COUNT(*) FROM word_scores WHERE category = 'last_name') AS total_last_names,
    (SELECT COUNT(*) FROM false_first_names) AS removed_first_names,
    (SELECT COUNT(*) FROM false_last_names) AS removed_last_names;

-- =============================================
-- Step 5: Also clean up bor_db.names if already migrated
-- =============================================

USE bor_db;

-- Remove false first names from names table
DELETE FROM names
WHERE name_type = 'first'
  AND name_lower IN (SELECT word_lower FROM phonebook_db.false_first_names);

-- Remove false last names from names table
DELETE FROM names  
WHERE name_type = 'last'
  AND name_lower IN (SELECT word_lower FROM phonebook_db.false_last_names);

-- Update any 'both' entries that should now be just first or last
UPDATE names n
SET n.name_type = 'first'
WHERE n.name_type = 'both'
  AND n.name_lower IN (SELECT word_lower FROM phonebook_db.false_last_names)
  AND n.name_lower NOT IN (SELECT word_lower FROM phonebook_db.false_first_names);

UPDATE names n
SET n.name_type = 'last'
WHERE n.name_type = 'both'
  AND n.name_lower IN (SELECT word_lower FROM phonebook_db.false_first_names)
  AND n.name_lower NOT IN (SELECT word_lower FROM phonebook_db.false_last_names);

-- Remove entries that were false in both categories
DELETE FROM names
WHERE name_type = 'both'
  AND name_lower IN (SELECT word_lower FROM phonebook_db.false_first_names)
  AND name_lower IN (SELECT word_lower FROM phonebook_db.false_last_names);

SELECT 
    'BOR_DB CLEANUP COMPLETE' AS Status,
    COUNT(*) AS remaining_names
FROM names;

DROP TEMPORARY TABLE IF EXISTS phonebook_db.false_first_names;
DROP TEMPORARY TABLE IF EXISTS phonebook_db.false_last_names;