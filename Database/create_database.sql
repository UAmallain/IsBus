-- =============================================
-- MariaDB/MySQL Database Setup Script
-- Business Name Detection API
-- =============================================

-- Create database if it doesn't exist
CREATE DATABASE IF NOT EXISTS phonebook_db
    CHARACTER SET utf8mb4
    COLLATE utf8mb4_unicode_ci;

-- Use the database
USE phonebook_db;

-- Drop existing table if needed (uncomment if you want to recreate)
-- DROP TABLE IF EXISTS words;

-- Create the words table for storing business name words
CREATE TABLE IF NOT EXISTS words (
    word_id INT AUTO_INCREMENT PRIMARY KEY,
    word_lower VARCHAR(255) NOT NULL,
    word_count INT NOT NULL DEFAULT 1,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    
    -- Add unique index on word_lower for faster lookups and to prevent duplicates
    UNIQUE KEY idx_word_lower (word_lower),
    
    -- Add index on word_count for potential queries by frequency
    KEY idx_word_count (word_count)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Optional: Create a separate table for business indicators (if you want database-driven configuration)
CREATE TABLE IF NOT EXISTS business_indicators (
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

-- Insert default business indicators (optional - can be managed via appsettings.json instead)
INSERT INTO business_indicators (indicator_text, indicator_type, weight) VALUES
-- Primary suffixes (high weight)
('LLC', 'primary_suffix', 25),
('Inc', 'primary_suffix', 25),
('Corp', 'primary_suffix', 25),
('Corporation', 'primary_suffix', 25),
('Co', 'primary_suffix', 25),
('Company', 'primary_suffix', 25),
('Ltd', 'primary_suffix', 25),
('Limited', 'primary_suffix', 25),
('PLC', 'primary_suffix', 25),
('P.C.', 'primary_suffix', 25),
('PC', 'primary_suffix', 25),

-- Secondary indicators (medium weight)
('Group', 'secondary_indicator', 15),
('Services', 'secondary_indicator', 15),
('Solutions', 'secondary_indicator', 15),
('Enterprises', 'secondary_indicator', 15),
('Associates', 'secondary_indicator', 15),
('Partners', 'secondary_indicator', 15),
('Industries', 'secondary_indicator', 15),
('International', 'secondary_indicator', 15),
('Global', 'secondary_indicator', 15),
('Holdings', 'secondary_indicator', 15),
('Ventures', 'secondary_indicator', 15),
('Capital', 'secondary_indicator', 15),
('Management', 'secondary_indicator', 15),
('Consulting', 'secondary_indicator', 15),
('Technologies', 'secondary_indicator', 15),
('Systems', 'secondary_indicator', 15),
('Software', 'secondary_indicator', 15),
('Development', 'secondary_indicator', 15),
('Marketing', 'secondary_indicator', 15),
('Design', 'secondary_indicator', 15),
('Studio', 'secondary_indicator', 15),
('Agency', 'secondary_indicator', 15),
('Center', 'secondary_indicator', 15),
('Centre', 'secondary_indicator', 15),
('Institute', 'secondary_indicator', 15),
('Foundation', 'secondary_indicator', 15),
('Trust', 'secondary_indicator', 15),

-- Stop words (to be excluded)
('the', 'stop_word', 0),
('and', 'stop_word', 0),
('of', 'stop_word', 0),
('in', 'stop_word', 0),
('to', 'stop_word', 0),
('a', 'stop_word', 0),
('an', 'stop_word', 0),
('for', 'stop_word', 0),
('with', 'stop_word', 0),
('by', 'stop_word', 0),
('at', 'stop_word', 0),
('from', 'stop_word', 0),
('on', 'stop_word', 0),
('as', 'stop_word', 0),
('or', 'stop_word', 0),
('is', 'stop_word', 0),
('are', 'stop_word', 0),
('was', 'stop_word', 0),
('were', 'stop_word', 0)
ON DUPLICATE KEY UPDATE weight = VALUES(weight);

-- Create stored procedure for word processing (optional enhancement)
DELIMITER $$

CREATE PROCEDURE IF NOT EXISTS process_business_word(
    IN p_word VARCHAR(255)
)
BEGIN
    DECLARE v_word_lower VARCHAR(255);
    SET v_word_lower = LOWER(TRIM(p_word));
    
    -- Insert new word or increment count if exists
    INSERT INTO words (word_lower, word_count)
    VALUES (v_word_lower, 1)
    ON DUPLICATE KEY UPDATE 
        word_count = word_count + 1,
        updated_at = CURRENT_TIMESTAMP;
END$$

DELIMITER ;

-- Create view for most common business words (useful for analytics)
CREATE OR REPLACE VIEW v_top_business_words AS
SELECT 
    word_id,
    word_lower,
    word_count,
    created_at,
    updated_at
FROM words
WHERE word_count > 1
ORDER BY word_count DESC
LIMIT 100;

-- Create view for recently added words
CREATE OR REPLACE VIEW v_recent_business_words AS
SELECT 
    word_id,
    word_lower,
    word_count,
    created_at,
    updated_at
FROM words
WHERE created_at >= DATE_SUB(NOW(), INTERVAL 7 DAY)
ORDER BY created_at DESC;

-- Grant appropriate permissions (adjust user as needed)
-- GRANT SELECT, INSERT, UPDATE ON phonebook_db.words TO 'api_user'@'localhost';
-- GRANT SELECT ON phonebook_db.business_indicators TO 'api_user'@'localhost';
-- GRANT EXECUTE ON PROCEDURE phonebook_db.process_business_word TO 'api_user'@'localhost';

-- Display table information
SELECT 'Database and tables created successfully!' AS Status;
SHOW TABLES;
DESCRIBE words;
DESCRIBE business_indicators;