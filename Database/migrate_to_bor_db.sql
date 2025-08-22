-- =============================================
-- Migration Script: Copy data from phonebook_db to bor_db
-- =============================================

-- First, make sure both databases exist
USE bor_db;

-- =============================================
-- Step 1: Copy words from phonebook_db.words to bor_db.words
-- =============================================
INSERT INTO bor_db.words (word_lower, word_count, last_seen, created_at, updated_at)
SELECT 
    word_lower,
    word_count,
    NOW() AS last_seen,  -- Set last_seen to current time for migration
    NOW() AS created_at,
    NOW() AS updated_at
FROM phonebook_db.words
ON DUPLICATE KEY UPDATE
    bor_db.words.word_count = bor_db.words.word_count + VALUES(word_count),
    bor_db.words.last_seen = VALUES(last_seen),
    bor_db.words.updated_at = CURRENT_TIMESTAMP;

-- Show migration results
SELECT 
    'Words Migration Complete' AS Status,
    COUNT(*) AS total_words_migrated,
    SUM(word_count) AS total_occurrences,
    MAX(word_count) AS highest_frequency
FROM bor_db.words;

-- =============================================
-- Step 2: Show top migrated words for verification
-- =============================================
SELECT 
    'Top 20 Migrated Words' AS Report;
    
SELECT 
    word_lower,
    word_count,
    last_seen
FROM bor_db.words
ORDER BY word_count DESC
LIMIT 20;

-- =============================================
-- Step 3: Create sample names data (optional - remove if you have a names file to import)
-- =============================================
-- Common first names (sample data)
INSERT INTO bor_db.names (name_lower, name_type, name_count) VALUES
('john', 'first', 100),
('mary', 'first', 95),
('robert', 'first', 90),
('patricia', 'first', 85),
('michael', 'first', 88),
('jennifer', 'first', 82),
('william', 'first', 87),
('linda', 'first', 80),
('david', 'first', 86),
('elizabeth', 'first', 78),
('richard', 'first', 84),
('barbara', 'first', 76),
('joseph', 'first', 83),
('susan', 'first', 75),
('thomas', 'first', 82),
('jessica', 'first', 74),
('charles', 'first', 81),
('sarah', 'first', 73),
('christopher', 'first', 80),
('karen', 'first', 72),
('sam', 'first', 70),
('tony', 'first', 68),
('mike', 'first', 75),
('jim', 'first', 65)
ON DUPLICATE KEY UPDATE 
    name_count = name_count + VALUES(name_count);

-- Common last names (sample data)
INSERT INTO bor_db.names (name_lower, name_type, name_count) VALUES
('smith', 'last', 150),
('johnson', 'last', 140),
('williams', 'last', 135),
('brown', 'last', 130),
('jones', 'last', 128),
('garcia', 'last', 125),
('miller', 'last', 123),
('davis', 'last', 120),
('rodriguez', 'last', 118),
('martinez', 'last', 115),
('hernandez', 'last', 113),
('lopez', 'last', 110),
('gonzalez', 'last', 108),
('wilson', 'last', 105),
('anderson', 'last', 103),
('thomas', 'last', 100),
('taylor', 'last', 98),
('moore', 'last', 95),
('jackson', 'last', 93),
('martin', 'last', 90),
('mcdonald', 'last', 85),
('thompson', 'last', 82),
('white', 'last', 80),
('harris', 'last', 78)
ON DUPLICATE KEY UPDATE 
    name_count = name_count + VALUES(name_count);

-- =============================================
-- Step 4: Verification queries
-- =============================================
SELECT 
    'Migration Summary' AS Report,
    (SELECT COUNT(*) FROM bor_db.words) AS total_words,
    (SELECT COUNT(*) FROM bor_db.names) AS total_names,
    (SELECT COUNT(*) FROM bor_db.names WHERE name_type = 'first') AS first_names,
    (SELECT COUNT(*) FROM bor_db.names WHERE name_type = 'last') AS last_names;

-- =============================================
-- Step 5: Data quality check
-- =============================================
-- Check for any potential names that ended up in words table
SELECT 
    'Potential names in words table (for manual review)' AS Report;

SELECT 
    w.word_lower,
    w.word_count,
    CASE 
        WHEN n.name_lower IS NOT NULL THEN 'Found in names table'
        ELSE 'Not in names table'
    END AS status
FROM bor_db.words w
LEFT JOIN bor_db.names n ON w.word_lower = n.name_lower
WHERE w.word_count > 100
    AND LENGTH(w.word_lower) <= 10
    AND w.word_lower REGEXP '^[a-z]+$'
ORDER BY w.word_count DESC
LIMIT 30;