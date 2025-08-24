-- =============================================
-- Check all categories in word_scores table
-- =============================================

USE phonebook_db;

-- Show all distinct categories and their counts
SELECT 
    category,
    COUNT(*) as record_count,
    SUM(count) as total_occurrences,
    AVG(count) as avg_occurrences,
    MAX(count) as max_occurrences,
    MIN(count) as min_occurrences
FROM word_scores
GROUP BY category
ORDER BY total_occurrences DESC;

-- Check specifically for name-related categories
SELECT 
    'Name-related Categories' AS Report;
    
SELECT 
    category,
    COUNT(*) as records
FROM word_scores
WHERE category LIKE '%name%'
   OR category LIKE '%first%'
   OR category LIKE '%last%'
   OR category LIKE '%surname%'
GROUP BY category;

-- Show sample data for each category
SELECT 
    'Sample Data by Category' AS Report;

-- Business keywords sample
SELECT 'Business Keywords Sample' AS Category_Sample;
SELECT word, word_lower, count, confidence
FROM word_scores
WHERE category = 'business_keyword'
ORDER BY count DESC
LIMIT 5;

-- City names sample
SELECT 'City Names Sample' AS Category_Sample;
SELECT word, word_lower, count, confidence
FROM word_scores  
WHERE category = 'city_name'
ORDER BY count DESC
LIMIT 5;

-- Street names sample
SELECT 'Street Names Sample' AS Category_Sample;
SELECT word, word_lower, count, confidence
FROM word_scores
WHERE category = 'street_name'
ORDER BY count DESC
LIMIT 5;

-- Check if there are any first_name or last_name categories
SELECT 'First Name Category Check' AS Check_Type;
SELECT word, word_lower, count
FROM word_scores
WHERE category = 'first_name'
LIMIT 5;

SELECT 'Last Name Category Check' AS Check_Type;
SELECT word, word_lower, count
FROM word_scores
WHERE category = 'last_name'
LIMIT 5;