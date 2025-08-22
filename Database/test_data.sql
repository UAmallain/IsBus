-- =============================================
-- Test Data for Business Name Detection API
-- =============================================

USE phonebook_db;

-- Insert sample business words with various frequencies
-- This simulates what the database might look like after processing many business names

INSERT INTO words (word_lower, word_count) VALUES
-- Common technology companies
('microsoft', 15),
('google', 12),
('apple', 18),
('amazon', 10),
('facebook', 8),
('meta', 5),
('tesla', 7),
('netflix', 6),
('adobe', 9),
('oracle', 11),

-- Common business words
('technologies', 45),
('solutions', 38),
('services', 52),
('consulting', 29),
('global', 41),
('international', 33),
('systems', 27),
('software', 35),
('digital', 22),
('innovations', 19),

-- Industry specific
('financial', 14),
('healthcare', 16),
('retail', 11),
('logistics', 8),
('manufacturing', 13),
('engineering', 17),
('construction', 9),
('hospitality', 7),
('automotive', 10),
('pharmaceutical', 6),

-- Geographic/Regional
('american', 21),
('united', 18),
('national', 15),
('regional', 9),
('pacific', 11),
('atlantic', 7),
('northern', 8),
('southern', 8),
('eastern', 6),
('western', 6),

-- Common company name components
('group', 48),
('holdings', 25),
('partners', 31),
('associates', 27),
('enterprises', 34),
('ventures', 20),
('capital', 23),
('management', 28),
('development', 24),
('research', 17),

-- Action/descriptor words
('advanced', 12),
('premier', 8),
('elite', 6),
('professional', 14),
('quality', 10),
('innovative', 9),
('strategic', 11),
('dynamic', 7),
('integrated', 13),
('comprehensive', 8),

-- Single occurrence words (simulate less common names)
('zenith', 1),
('apex', 1),
('pinnacle', 1),
('nexus', 1),
('synergy', 2),
('paradigm', 1),
('catalyst', 1),
('momentum', 1),
('vanguard', 2),
('frontier', 1)
ON DUPLICATE KEY UPDATE 
    word_count = word_count + VALUES(word_count);

-- Show summary of test data
SELECT 
    'Test data loaded' AS status,
    COUNT(*) AS total_words,
    SUM(word_count) AS total_occurrences,
    AVG(word_count) AS avg_occurrence,
    MAX(word_count) AS max_occurrence,
    MIN(word_count) AS min_occurrence
FROM words;

-- Show top 10 most frequent words
SELECT 
    word_lower,
    word_count
FROM words
ORDER BY word_count DESC
LIMIT 10;