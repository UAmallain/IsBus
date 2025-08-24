-- =============================================
-- Update Views and Functions for word_data Table
-- =============================================

USE bor_db;

-- =============================================
-- Drop existing views and functions
-- =============================================
DROP VIEW IF EXISTS v_recent_activity;
DROP VIEW IF EXISTS v_top_business_words;
DROP VIEW IF EXISTS v_top_names;
DROP FUNCTION IF EXISTS update_name_count;
DROP FUNCTION IF EXISTS update_word_count;

-- =============================================
-- Create new unified function: update_word_data_count
-- Replaces both update_name_count and update_word_count
-- =============================================
DELIMITER $$

CREATE FUNCTION update_word_data_count(
    p_word VARCHAR(255),
    p_type VARCHAR(20),  -- 'first', 'last', 'both', 'business', 'indeterminate'
    p_increment INT
) RETURNS INT
DETERMINISTIC
BEGIN
    DECLARE v_count INT DEFAULT 0;
    
    -- Normalize the word
    SET p_word = LOWER(TRIM(p_word));
    
    -- Insert or update the word_data entry
    INSERT INTO word_data (word_lower, word_type, word_count, last_seen)
    VALUES (p_word, p_type, p_increment, NOW())
    ON DUPLICATE KEY UPDATE
        word_count = word_count + p_increment,
        last_seen = NOW(),
        updated_at = NOW();
    
    -- Get the updated count
    SELECT word_count INTO v_count
    FROM word_data
    WHERE word_lower = p_word AND word_type = p_type;
    
    RETURN v_count;
END$$

DELIMITER ;

-- =============================================
-- Create backward compatibility functions
-- =============================================
DELIMITER $$

-- Wrapper function for name updates (backward compatibility)
CREATE FUNCTION update_name_count(
    p_name VARCHAR(255),
    p_type VARCHAR(10),  -- 'first', 'last', or 'both'
    p_increment INT
) RETURNS INT
DETERMINISTIC
BEGIN
    RETURN update_word_data_count(p_name, p_type, p_increment);
END$$

-- Wrapper function for word updates (backward compatibility)
CREATE FUNCTION update_word_count(
    p_word VARCHAR(255),
    p_increment INT
) RETURNS INT
DETERMINISTIC
BEGIN
    RETURN update_word_data_count(p_word, 'business', p_increment);
END$$

DELIMITER ;

-- =============================================
-- Create updated view: v_recent_activity
-- Shows recent activity across all word types
-- =============================================
CREATE VIEW v_recent_activity AS
SELECT 
    word_lower,
    word_type,
    word_count,
    last_seen,
    CASE word_type
        WHEN 'first' THEN 'First Name'
        WHEN 'last' THEN 'Last Name'
        WHEN 'both' THEN 'First/Last Name'
        WHEN 'business' THEN 'Business Word'
        WHEN 'indeterminate' THEN 'Common Word'
        ELSE 'Unknown'
    END AS type_description,
    TIMESTAMPDIFF(MINUTE, last_seen, NOW()) AS minutes_ago
FROM word_data
WHERE last_seen >= DATE_SUB(NOW(), INTERVAL 24 HOUR)
ORDER BY last_seen DESC
LIMIT 100;

-- =============================================
-- Create updated view: v_top_business_words
-- Shows top business words
-- =============================================
CREATE VIEW v_top_business_words AS
SELECT 
    word_lower,
    word_count,
    last_seen,
    RANK() OVER (ORDER BY word_count DESC) AS rank_by_count,
    ROUND(word_count * 100.0 / SUM(word_count) OVER(), 2) AS percentage_of_total
FROM word_data
WHERE word_type = 'business'
    AND word_count > 10  -- Filter out low-frequency words
ORDER BY word_count DESC
LIMIT 500;

-- =============================================
-- Create updated view: v_top_names
-- Shows top names by type
-- =============================================
CREATE VIEW v_top_names AS
SELECT 
    word_lower AS name,
    word_type AS name_type,
    word_count AS occurrence_count,
    last_seen,
    RANK() OVER (PARTITION BY word_type ORDER BY word_count DESC) AS rank_in_type,
    ROUND(word_count * 100.0 / SUM(word_count) OVER (PARTITION BY word_type), 2) AS percentage_in_type
FROM word_data
WHERE word_type IN ('first', 'last', 'both')
    AND word_count > 5  -- Filter out rare names
ORDER BY word_type, word_count DESC;

-- =============================================
-- Create additional helpful views
-- =============================================

-- View to show words that appear in multiple categories
CREATE OR REPLACE VIEW v_multi_type_words AS
SELECT 
    word_lower,
    GROUP_CONCAT(CONCAT(word_type, ':', word_count) ORDER BY word_count DESC SEPARATOR ', ') AS type_counts,
    COUNT(DISTINCT word_type) AS type_count,
    SUM(word_count) AS total_count
FROM word_data
GROUP BY word_lower
HAVING COUNT(DISTINCT word_type) > 1
ORDER BY total_count DESC;

-- View to show word classification (similar to word_context from before)
CREATE OR REPLACE VIEW v_word_classification AS
SELECT 
    word_lower,
    MAX(CASE WHEN word_type = 'first' THEN word_count ELSE 0 END) AS first_count,
    MAX(CASE WHEN word_type = 'last' THEN word_count ELSE 0 END) AS last_count,
    MAX(CASE WHEN word_type = 'both' THEN word_count ELSE 0 END) AS both_count,
    MAX(CASE WHEN word_type = 'business' THEN word_count ELSE 0 END) AS business_count,
    MAX(CASE WHEN word_type = 'indeterminate' THEN word_count ELSE 0 END) AS indeterminate_count,
    -- Determine primary classification
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
    END AS primary_classification,
    SUM(word_count) AS total_count
FROM word_data
GROUP BY word_lower;

-- =============================================
-- Test the new functions and views
-- =============================================
SELECT 'Testing Functions' AS Test;

-- Test update_word_data_count
SELECT update_word_data_count('test_word', 'business', 5) AS test_business_update;
SELECT update_word_data_count('test_name', 'first', 3) AS test_name_update;

-- Test backward compatibility functions
SELECT update_word_count('test_business', 10) AS test_word_compat;
SELECT update_name_count('test_firstname', 'first', 7) AS test_name_compat;

-- Clean up test data
DELETE FROM word_data WHERE word_lower LIKE 'test_%';

SELECT 'Testing Views' AS Test;

-- Test views
SELECT 'Recent Activity (Top 5)' AS View_Test;
SELECT * FROM v_recent_activity LIMIT 5;

SELECT 'Top Business Words (Top 5)' AS View_Test;
SELECT * FROM v_top_business_words LIMIT 5;

SELECT 'Top Names (Top 5 per type)' AS View_Test;
SELECT * FROM v_top_names WHERE rank_in_type <= 5;

SELECT 'Multi-Type Words (Top 5)' AS View_Test;
SELECT * FROM v_multi_type_words LIMIT 5;

SELECT 'All views and functions updated successfully!' AS Status;