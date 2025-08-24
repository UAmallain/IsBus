-- Check names table
SELECT 'Names Table Stats:' as info;
SELECT name_type, COUNT(*) as count 
FROM names 
GROUP BY name_type;

SELECT 'Sample First Names:' as info;
SELECT name_lower, name_count 
FROM names 
WHERE name_type = 'first' 
ORDER BY name_count DESC 
LIMIT 10;

SELECT 'Sample Last Names:' as info;
SELECT name_lower, name_count 
FROM names 
WHERE name_type = 'last' 
ORDER BY name_count DESC 
LIMIT 10;

SELECT 'Sample Both Names:' as info;
SELECT name_lower, name_count 
FROM names 
WHERE name_type = 'both' 
ORDER BY name_count DESC 
LIMIT 10;

-- Check words table
SELECT 'Words Table Stats:' as info;
SELECT COUNT(*) as total_business_words FROM words;

SELECT 'Top Business Words:' as info;
SELECT word_lower, word_count 
FROM words 
ORDER BY word_count DESC 
LIMIT 20;