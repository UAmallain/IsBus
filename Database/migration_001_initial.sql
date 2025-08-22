-- =============================================
-- Migration 001: Initial Database Schema
-- Date: 2024
-- Description: Creates initial database schema for Business Name Detection API
-- =============================================

-- Create version tracking table if it doesn't exist
CREATE TABLE IF NOT EXISTS schema_migrations (
    version VARCHAR(50) PRIMARY KEY,
    applied_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    description TEXT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Check if migration has already been applied
SELECT COUNT(*) INTO @migration_exists 
FROM schema_migrations 
WHERE version = '001_initial';

-- Only run migration if it hasn't been applied
DELIMITER $$

CREATE PROCEDURE apply_migration_001()
BEGIN
    IF @migration_exists = 0 THEN
        -- Create the words table
        CREATE TABLE IF NOT EXISTS words (
            word_id INT AUTO_INCREMENT PRIMARY KEY,
            word_lower VARCHAR(255) NOT NULL,
            word_count INT NOT NULL DEFAULT 1,
            created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
            updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
            
            UNIQUE KEY idx_word_lower (word_lower),
            KEY idx_word_count (word_count)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        
        -- Record migration
        INSERT INTO schema_migrations (version, description) 
        VALUES ('001_initial', 'Create words table for business name detection');
        
        SELECT 'Migration 001_initial applied successfully' AS status;
    ELSE
        SELECT 'Migration 001_initial already applied' AS status;
    END IF;
END$$

DELIMITER ;

-- Execute the migration
CALL apply_migration_001();

-- Clean up
DROP PROCEDURE apply_migration_001;