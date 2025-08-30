-- Verification script for migration
-- Run this to check if the migration was successful

-- 1. Check corporate suffixes (should have count >= 99999)
SELECT 'Corporate Suffixes:' as Test;
SELECT word_lower, word_type, word_count 
FROM word_data 
WHERE word_type = 'business' AND word_count >= 99999
ORDER BY word_lower
LIMIT 10;

-- 2. Check Abraham and Kaine word counts
SELECT '\nAbraham word data:' as Test;
SELECT word_lower, word_type, word_count 
FROM word_data 
WHERE word_lower = 'abraham';

SELECT '\nKaine word data:' as Test;
SELECT word_lower, word_type, word_count 
FROM word_data 
WHERE word_lower = 'kaine';

-- 3. Check some business words with specific counts
SELECT '\nBusiness words (sample):' as Test;
SELECT word_lower, word_type, word_count 
FROM word_data 
WHERE word_type = 'business' 
  AND word_count IN (5000, 3000, 2000)
ORDER BY word_count DESC, word_lower
LIMIT 10;

-- 4. Count total words by type
SELECT '\nWord counts by type:' as Test;
SELECT word_type, COUNT(*) as count, MIN(word_count) as min_count, MAX(word_count) as max_count
FROM word_data
GROUP BY word_type
ORDER BY word_type;

-- 5. Check if special categories exist (if enum was updated)
SELECT '\nSpecial word types (if enum updated):' as Test;
SELECT DISTINCT word_type 
FROM word_data 
WHERE word_type IN ('corporate', 'title', 'road', 'location', 'residential');