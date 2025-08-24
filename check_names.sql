USE bor_db;

-- Check if names table has data
SELECT 'Names Table Status' as Report;
SELECT COUNT(*) as total_names FROM names;

-- Check for specific names
SELECT 'Looking for Smith and Jack' as Report;
SELECT name_lower, name_type, name_count 
FROM names 
WHERE name_lower IN ('smith', 'jack')
ORDER BY name_count DESC;

-- Check words table for comparison
SELECT 'Words Table - Smith and Jack' as Report;
SELECT word_lower, word_count
FROM words
WHERE word_lower IN ('smith', 'jack')
ORDER BY word_count DESC;

-- Show top names to verify data exists
SELECT 'Top 10 Names' as Report;
SELECT name_lower, name_type, name_count
FROM names
ORDER BY name_count DESC
LIMIT 10;