-- =============================================
-- Sample Queries for Business Name Detection Database
-- =============================================

USE phonebook_db;

-- 1. Get top 20 most common business words
SELECT 
    word_lower AS business_word,
    word_count AS frequency,
    updated_at AS last_seen
FROM words
ORDER BY word_count DESC
LIMIT 20;

-- 2. Get total unique business words
SELECT COUNT(*) AS total_unique_words FROM words;

-- 3. Get words added in the last 24 hours
SELECT 
    word_lower,
    word_count,
    created_at
FROM words
WHERE created_at >= DATE_SUB(NOW(), INTERVAL 24 HOUR)
ORDER BY created_at DESC;

-- 4. Search for specific business word patterns
SELECT 
    word_lower,
    word_count
FROM words
WHERE word_lower LIKE '%tech%'
   OR word_lower LIKE '%soft%'
   OR word_lower LIKE '%data%'
ORDER BY word_count DESC;

-- 5. Get statistics about word frequency distribution
SELECT 
    CASE 
        WHEN word_count = 1 THEN 'Seen once'
        WHEN word_count BETWEEN 2 AND 5 THEN 'Seen 2-5 times'
        WHEN word_count BETWEEN 6 AND 10 THEN 'Seen 6-10 times'
        WHEN word_count BETWEEN 11 AND 50 THEN 'Seen 11-50 times'
        ELSE 'Seen 50+ times'
    END AS frequency_range,
    COUNT(*) AS word_count,
    ROUND(COUNT(*) * 100.0 / (SELECT COUNT(*) FROM words), 2) AS percentage
FROM words
GROUP BY frequency_range
ORDER BY 
    CASE frequency_range
        WHEN 'Seen once' THEN 1
        WHEN 'Seen 2-5 times' THEN 2
        WHEN 'Seen 6-10 times' THEN 3
        WHEN 'Seen 11-50 times' THEN 4
        ELSE 5
    END;

-- 6. Find words that haven't been seen in the last 30 days
SELECT 
    word_lower,
    word_count,
    updated_at AS last_seen,
    DATEDIFF(NOW(), updated_at) AS days_since_last_seen
FROM words
WHERE updated_at < DATE_SUB(NOW(), INTERVAL 30 DAY)
ORDER BY updated_at ASC
LIMIT 50;

-- 7. Get all configured business indicators
SELECT 
    indicator_text,
    indicator_type,
    weight,
    is_active
FROM business_indicators
WHERE is_active = TRUE
ORDER BY indicator_type, weight DESC;

-- 8. Monthly word addition trend
SELECT 
    DATE_FORMAT(created_at, '%Y-%m') AS month,
    COUNT(*) AS new_words_added,
    SUM(word_count) AS total_occurrences
FROM words
GROUP BY DATE_FORMAT(created_at, '%Y-%m')
ORDER BY month DESC
LIMIT 12;

-- 9. Find potential business name patterns (words often seen together)
-- This would require tracking phrases, but we can look for common industry words
SELECT 
    w1.word_lower AS word,
    w1.word_count,
    CASE 
        WHEN EXISTS (SELECT 1 FROM business_indicators WHERE indicator_text = w1.word_lower) 
        THEN 'Business Indicator'
        ELSE 'Regular Word'
    END AS word_type
FROM words w1
WHERE w1.word_count > 5
ORDER BY w1.word_count DESC
LIMIT 30;

-- 10. Database size and performance metrics
SELECT 
    TABLE_NAME,
    ROUND(((DATA_LENGTH + INDEX_LENGTH) / 1024 / 1024), 2) AS 'Size (MB)',
    TABLE_ROWS AS 'Row Count',
    AVG_ROW_LENGTH AS 'Avg Row Size (bytes)',
    AUTO_INCREMENT AS 'Next ID'
FROM information_schema.TABLES 
WHERE TABLE_SCHEMA = 'phonebook_db'
    AND TABLE_NAME IN ('words', 'business_indicators');

-- 11. Clean up words with single occurrence older than 90 days (maintenance query)
-- SELECT COUNT(*) FROM words 
-- WHERE word_count = 1 
--   AND updated_at < DATE_SUB(NOW(), INTERVAL 90 DAY);

-- Uncomment to actually delete:
-- DELETE FROM words 
-- WHERE word_count = 1 
--   AND updated_at < DATE_SUB(NOW(), INTERVAL 90 DAY)
-- LIMIT 1000;

-- 12. Export top business words to CSV format
SELECT 
    CONCAT('"', word_lower, '",', word_count, ',"', DATE_FORMAT(updated_at, '%Y-%m-%d %H:%i:%s'), '"') AS csv_row
FROM words
ORDER BY word_count DESC
LIMIT 100;