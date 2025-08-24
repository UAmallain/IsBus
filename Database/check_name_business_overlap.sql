-- =============================================
-- Check overlap between business keywords and names
-- =============================================

USE phonebook_db;

-- Count overlaps
SELECT 
    'Overlap Statistics' AS Report,
    (SELECT COUNT(DISTINCT bk.word_lower)
     FROM word_scores bk
     INNER JOIN word_scores n ON bk.word_lower = n.word_lower
     WHERE bk.category = 'business_keyword'
       AND n.category IN ('first_name', 'last_name')) AS keywords_that_are_names,
    
    (SELECT COUNT(DISTINCT word_lower)
     FROM word_scores
     WHERE category = 'business_keyword') AS total_business_keywords,
     
    (SELECT COUNT(DISTINCT bk.word_lower)
     FROM word_scores bk
     WHERE bk.category = 'business_keyword'
       AND NOT EXISTS (SELECT 1 FROM word_scores n 
                       WHERE n.word_lower = bk.word_lower 
                       AND n.category IN ('first_name', 'last_name'))) AS keywords_not_names;

-- Show business keywords that are ALSO first names
SELECT 
    'Business Keywords That Are Also FIRST Names' AS Report;

SELECT 
    bk.word_lower AS keyword,
    bk.count AS business_count,
    fn.count AS first_name_count,
    bk.count + fn.count AS total_count
FROM word_scores bk
INNER JOIN word_scores fn ON bk.word_lower = fn.word_lower
WHERE bk.category = 'business_keyword'
  AND fn.category = 'first_name'
ORDER BY bk.count DESC
LIMIT 15;

-- Show business keywords that are ALSO last names
SELECT 
    'Business Keywords That Are Also LAST Names' AS Report;

SELECT 
    bk.word_lower AS keyword,
    bk.count AS business_count,
    ln.count AS last_name_count,
    bk.count + ln.count AS total_count
FROM word_scores bk
INNER JOIN word_scores ln ON bk.word_lower = ln.word_lower
WHERE bk.category = 'business_keyword'
  AND ln.category = 'last_name'
ORDER BY bk.count DESC
LIMIT 15;

-- Show business keywords that are BOTH first AND last names
SELECT 
    'Business Keywords That Are BOTH First AND Last Names' AS Report;

SELECT 
    bk.word_lower AS keyword,
    bk.count AS business_count,
    fn.count AS first_name_count,
    ln.count AS last_name_count,
    bk.count + fn.count + ln.count AS total_count
FROM word_scores bk
INNER JOIN word_scores fn ON bk.word_lower = fn.word_lower
INNER JOIN word_scores ln ON bk.word_lower = ln.word_lower
WHERE bk.category = 'business_keyword'
  AND fn.category = 'first_name'
  AND ln.category = 'last_name'
ORDER BY bk.count DESC
LIMIT 15;

-- Examples of pure business keywords (NOT names)
SELECT 
    'Pure Business Keywords (NOT Names) - Top 20' AS Report;

SELECT 
    word_lower AS keyword,
    count AS occurrences
FROM word_scores bk
WHERE category = 'business_keyword'
  AND NOT EXISTS (SELECT 1 FROM word_scores n 
                  WHERE n.word_lower = bk.word_lower 
                  AND n.category IN ('first_name', 'last_name'))
ORDER BY count DESC
LIMIT 20;