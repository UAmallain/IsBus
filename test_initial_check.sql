-- Test to see if "marc" and "amhed" are in word_data as names
SELECT 
    word_lower,
    word_type,
    word_count
FROM word_data 
WHERE word_lower IN ('marc', 'amhed', 'a', 's', 'smith', 'johnson', 'k', 'j')
ORDER BY word_lower, word_type;

-- Check if they are in Names table
SELECT 
    name_lower AS word_lower,
    name_type AS word_type,
    'Names table' AS source
FROM names
WHERE name_lower IN ('marc', 'amhed', 'smith', 'johnson')
ORDER BY name_lower, name_type;