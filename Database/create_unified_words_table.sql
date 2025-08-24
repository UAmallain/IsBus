-- =============================================
-- Create Unified Words Table and Migrate Data
-- Combines names and words tables with word_type
-- =============================================

USE bor_db;

-- =============================================
-- Step 1: Create the new unified table
-- =============================================
CREATE TABLE IF NOT EXISTS word_data (
    word_id INT PRIMARY KEY AUTO_INCREMENT,
    word_lower VARCHAR(255) NOT NULL,
    word_type ENUM('first', 'last', 'both', 'business', 'indeterminate') NOT NULL,
    word_count INT DEFAULT 1,
    last_seen DATETIME DEFAULT CURRENT_TIMESTAMP,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    UNIQUE KEY unique_word_type (word_lower, word_type),
    INDEX idx_word_lower (word_lower),
    INDEX idx_word_type (word_type),
    INDEX idx_word_count (word_count DESC)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- =============================================
-- Step 2: Migrate data from names table
-- =============================================
INSERT INTO word_data (word_lower, word_type, word_count, last_seen, created_at, updated_at)
SELECT 
    name_lower,
    name_type,
    name_count,
    last_seen,
    created_at,
    updated_at
FROM names
ON DUPLICATE KEY UPDATE
    word_data.word_count = word_data.word_count + VALUES(word_count),
    word_data.last_seen = VALUES(last_seen),
    word_data.updated_at = CURRENT_TIMESTAMP;

-- =============================================
-- Step 3: Migrate data from words table (business words)
-- =============================================
INSERT INTO word_data (word_lower, word_type, word_count, last_seen, created_at, updated_at)
SELECT 
    word_lower,
    'business' AS word_type,
    word_count,
    last_seen,
    created_at,
    updated_at
FROM words
ON DUPLICATE KEY UPDATE
    word_data.word_count = word_data.word_count + VALUES(word_count),
    word_data.last_seen = VALUES(last_seen),
    word_data.updated_at = CURRENT_TIMESTAMP;

-- =============================================
-- Step 4: Add indeterminate entries for common words
-- =============================================
INSERT INTO word_data (word_lower, word_type, word_count)
VALUES 
    ('a', 'indeterminate', 0),
    ('an', 'indeterminate', 0),
    ('the', 'indeterminate', 0),
    ('of', 'indeterminate', 0),
    ('in', 'indeterminate', 0),
    ('on', 'indeterminate', 0),
    ('at', 'indeterminate', 0),
    ('to', 'indeterminate', 0),
    ('for', 'indeterminate', 0),
    ('by', 'indeterminate', 0),
    ('with', 'indeterminate', 0),
    ('from', 'indeterminate', 0),
    ('and', 'indeterminate', 0),
    ('or', 'indeterminate', 0),
    ('but', 'indeterminate', 0),
    ('&', 'indeterminate', 0)
ON DUPLICATE KEY UPDATE word_type = word_type;

-- =============================================
-- Step 5: Create view for easy context mapping
-- =============================================
CREATE OR REPLACE VIEW word_context AS
SELECT 
    word_lower,
    MAX(CASE WHEN word_type = 'first' THEN word_count ELSE 0 END) AS first_count,
    MAX(CASE WHEN word_type = 'last' THEN word_count ELSE 0 END) AS last_count,
    MAX(CASE WHEN word_type = 'both' THEN word_count ELSE 0 END) AS both_count,
    MAX(CASE WHEN word_type = 'business' THEN word_count ELSE 0 END) AS business_count,
    MAX(CASE WHEN word_type = 'indeterminate' THEN word_count ELSE 0 END) AS indeterminate_count,
    -- Determine primary type based on highest count
    CASE 
        WHEN MAX(CASE WHEN word_type = 'business' THEN word_count ELSE 0 END) > 
             GREATEST(
                MAX(CASE WHEN word_type = 'first' THEN word_count ELSE 0 END),
                MAX(CASE WHEN word_type = 'last' THEN word_count ELSE 0 END),
                MAX(CASE WHEN word_type = 'both' THEN word_count ELSE 0 END)
             ) * 2 THEN 'business'
        WHEN MAX(CASE WHEN word_type = 'both' THEN word_count ELSE 0 END) >= 
             GREATEST(
                MAX(CASE WHEN word_type = 'first' THEN word_count ELSE 0 END),
                MAX(CASE WHEN word_type = 'last' THEN word_count ELSE 0 END),
                MAX(CASE WHEN word_type = 'business' THEN word_count ELSE 0 END)
             ) THEN 'both'
        WHEN MAX(CASE WHEN word_type = 'last' THEN word_count ELSE 0 END) >= 
             MAX(CASE WHEN word_type = 'first' THEN word_count ELSE 0 END) THEN 'last'
        WHEN MAX(CASE WHEN word_type = 'first' THEN word_count ELSE 0 END) > 0 THEN 'first'
        WHEN MAX(CASE WHEN word_type = 'indeterminate' THEN word_count ELSE 0 END) >= 0 THEN 'indeterminate'
        ELSE 'unknown'
    END AS primary_type,
    MAX(word_count) AS max_count
FROM word_data
GROUP BY word_lower;

-- =============================================
-- Step 6: Show migration results
-- =============================================
SELECT 'Migration Summary' AS Report;
SELECT 
    word_type,
    COUNT(*) AS record_count,
    SUM(word_count) AS total_occurrences
FROM word_data
GROUP BY word_type
WITH ROLLUP;

-- Show sample context mappings
SELECT 'Sample Context Mappings' AS Report;
SELECT * FROM word_context
WHERE word_lower IN ('smith', 'jack', 'jones', 'soda', 'hair', 'affair', 'with', 'addy')
ORDER BY max_count DESC;