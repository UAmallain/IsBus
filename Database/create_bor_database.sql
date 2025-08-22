-- =============================================
-- Business or Residential (BOR) Database Setup
-- Creates new database with words and names tables
-- =============================================

-- Create the new database
CREATE DATABASE IF NOT EXISTS bor_db
    CHARACTER SET utf8mb4
    COLLATE utf8mb4_unicode_ci;

USE bor_db;

-- =============================================
-- Words table (business indicators)
-- =============================================
DROP TABLE IF EXISTS words;

CREATE TABLE words (
    word_id INT AUTO_INCREMENT PRIMARY KEY,
    word_lower VARCHAR(255) NOT NULL,
    word_count INT NOT NULL DEFAULT 1,
    last_seen DATETIME DEFAULT CURRENT_TIMESTAMP,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    
    UNIQUE KEY idx_word_lower (word_lower),
    KEY idx_word_count (word_count),
    KEY idx_last_seen (last_seen)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- =============================================
-- Names table (first and last names)
-- =============================================
DROP TABLE IF EXISTS names;

CREATE TABLE names (
    name_id INT AUTO_INCREMENT PRIMARY KEY,
    name_lower VARCHAR(255) NOT NULL,
    name_type ENUM('first', 'last', 'both') NOT NULL DEFAULT 'both',
    name_count INT NOT NULL DEFAULT 1,
    last_seen DATETIME DEFAULT CURRENT_TIMESTAMP,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    
    UNIQUE KEY idx_name_lower_type (name_lower, name_type),
    KEY idx_name_count (name_count),
    KEY idx_name_type (name_type),
    KEY idx_last_seen (last_seen)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- =============================================
-- Business indicators configuration table (optional)
-- =============================================
DROP TABLE IF EXISTS business_indicators;

CREATE TABLE business_indicators (
    indicator_id INT AUTO_INCREMENT PRIMARY KEY,
    indicator_text VARCHAR(100) NOT NULL,
    indicator_type ENUM('primary_suffix', 'secondary_indicator', 'stop_word') NOT NULL,
    weight INT DEFAULT 0,
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    
    UNIQUE KEY idx_indicator_text_type (indicator_text, indicator_type),
    KEY idx_indicator_type (indicator_type),
    KEY idx_is_active (is_active)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- =============================================
-- Stored procedures
-- =============================================
DELIMITER $$

-- Procedure to update word count and last seen
CREATE PROCEDURE IF NOT EXISTS update_word_count(
    IN p_word VARCHAR(255),
    IN p_increment INT
)
BEGIN
    INSERT INTO words (word_lower, word_count, last_seen)
    VALUES (LOWER(TRIM(p_word)), p_increment, NOW())
    ON DUPLICATE KEY UPDATE 
        word_count = word_count + p_increment,
        last_seen = NOW(),
        updated_at = CURRENT_TIMESTAMP;
END$$

-- Procedure to update name count and last seen
CREATE PROCEDURE IF NOT EXISTS update_name_count(
    IN p_name VARCHAR(255),
    IN p_type ENUM('first', 'last', 'both'),
    IN p_increment INT
)
BEGIN
    INSERT INTO names (name_lower, name_type, name_count, last_seen)
    VALUES (LOWER(TRIM(p_name)), p_type, p_increment, NOW())
    ON DUPLICATE KEY UPDATE 
        name_count = name_count + p_increment,
        last_seen = NOW(),
        updated_at = CURRENT_TIMESTAMP;
END$$

DELIMITER ;

-- =============================================
-- Views for analytics
-- =============================================

-- Top business words
CREATE OR REPLACE VIEW v_top_business_words AS
SELECT 
    word_id,
    word_lower,
    word_count,
    last_seen,
    DATEDIFF(NOW(), last_seen) AS days_since_seen
FROM words
WHERE word_count > 10
ORDER BY word_count DESC
LIMIT 100;

-- Top names
CREATE OR REPLACE VIEW v_top_names AS
SELECT 
    name_id,
    name_lower,
    name_type,
    name_count,
    last_seen,
    DATEDIFF(NOW(), last_seen) AS days_since_seen
FROM names
WHERE name_count > 5
ORDER BY name_count DESC
LIMIT 100;

-- Recently seen words
CREATE OR REPLACE VIEW v_recent_activity AS
SELECT 
    'word' AS item_type,
    word_lower AS item,
    word_count AS count,
    last_seen
FROM words
WHERE last_seen >= DATE_SUB(NOW(), INTERVAL 7 DAY)
UNION ALL
SELECT 
    'name' AS item_type,
    name_lower AS item,
    name_count AS count,
    last_seen
FROM names
WHERE last_seen >= DATE_SUB(NOW(), INTERVAL 7 DAY)
ORDER BY last_seen DESC;

-- =============================================
-- Indexes for performance
-- =============================================
CREATE INDEX idx_words_recent ON words(last_seen DESC, word_count DESC);
CREATE INDEX idx_names_recent ON names(last_seen DESC, name_count DESC);

-- =============================================
-- Show summary
-- =============================================
SELECT 'Database bor_db created successfully!' AS Status;
SHOW TABLES;
DESCRIBE words;
DESCRIBE names;