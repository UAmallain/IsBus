-- =============================================
-- Check structure of word_scores table
-- Run this first to understand the table structure
-- =============================================

USE phonebook_db;

-- Show the structure of word_scores table
DESCRIBE word_scores;

-- Show column names and types
SELECT 
    COLUMN_NAME,
    DATA_TYPE,
    IS_NULLABLE,
    COLUMN_DEFAULT,
    COLUMN_KEY
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'phonebook_db'
  AND TABLE_NAME = 'word_scores'
ORDER BY ORDINAL_POSITION;

-- Show sample data to understand the content
SELECT * FROM word_scores LIMIT 10;

-- Show distinct word_type values to understand categorization
SELECT DISTINCT word_type, COUNT(*) as count
FROM word_scores
GROUP BY word_type;

-- Show sample of first names
SELECT word, word_type, score
FROM word_scores
WHERE word_type LIKE '%first%'
LIMIT 10;

-- Show sample of last names  
SELECT word, word_type, score
FROM word_scores
WHERE word_type LIKE '%last%' OR word_type LIKE '%surname%'
LIMIT 10;

-- Count total first and last names
SELECT 
    SUM(CASE WHEN word_type LIKE '%first%' THEN 1 ELSE 0 END) AS first_names,
    SUM(CASE WHEN word_type LIKE '%last%' OR word_type LIKE '%surname%' THEN 1 ELSE 0 END) AS last_names,
    COUNT(*) AS total_records
FROM word_scores;