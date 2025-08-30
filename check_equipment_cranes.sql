-- Check word counts for 'cranes' and 'equipment'
SELECT word_lower, word_type, word_count 
FROM word_data 
WHERE word_lower IN ('cranes', 'equipment', 'ltd', 'leil')
ORDER BY word_lower, word_type;