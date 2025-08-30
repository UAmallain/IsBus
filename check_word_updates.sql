-- Check word counts for Abraham and Kaine
SELECT 
    word_lower,
    word_type,
    word_count,
    last_seen,
    updated_at
FROM word_data 
WHERE word_lower IN ('abraham', 'kaine')
ORDER BY word_lower, word_type;

-- Check if last_seen has been updated recently (last 10 minutes)
SELECT 
    word_lower,
    word_type,
    word_count,
    last_seen,
    TIMESTAMPDIFF(MINUTE, last_seen, NOW()) as minutes_ago
FROM word_data 
WHERE word_lower IN ('abraham', 'kaine')
  AND last_seen >= DATE_SUB(NOW(), INTERVAL 10 MINUTE)
ORDER BY last_seen DESC;