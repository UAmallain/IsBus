-- Create street_names table for tracking and learning street names
USE bor_db;

CREATE TABLE IF NOT EXISTS `street_names` (
    `id` INT(11) NOT NULL AUTO_INCREMENT,
    `street_name` VARCHAR(255) NOT NULL COMMENT 'Street name (original case)',
    `street_name_lower` VARCHAR(255) NOT NULL COMMENT 'Lowercase version for matching',
    `street_type` VARCHAR(50) NULL COMMENT 'Type of street (Road, Street, Avenue, etc.)',
    `province_code` VARCHAR(2) NULL COMMENT 'Two-letter province code',
    `community` VARCHAR(255) NULL COMMENT 'Community/city where street is located',
    `occurrence_count` INT(11) NOT NULL DEFAULT 1 COMMENT 'Number of times this street has been seen',
    `created_at` TIMESTAMP NULL DEFAULT current_timestamp(),
    `updated_at` TIMESTAMP NULL DEFAULT current_timestamp() ON UPDATE current_timestamp(),
    PRIMARY KEY (`id`),
    INDEX `idx_street_name_lower` (`street_name_lower`),
    INDEX `idx_province` (`province_code`),
    INDEX `idx_occurrence` (`occurrence_count` DESC),
    UNIQUE KEY `unique_street_province` (`street_name_lower`, `province_code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Stores street names discovered during parsing for improved future detection';

-- Add some sample/known street names for NB
INSERT IGNORE INTO `street_names` (`street_name`, `street_name_lower`, `street_type`, `province_code`, `community`, `occurrence_count`) VALUES
('Main', 'main', 'Street', 'NB', NULL, 100),
('Church', 'church', 'Street', 'NB', NULL, 50),
('Mountain', 'mountain', 'Road', 'NB', NULL, 40),
('Indian Mountain', 'indian mountain', 'Road', 'NB', NULL, 10),
('Pleasant', 'pleasant', 'Street', 'NB', NULL, 30),
('Water', 'water', 'Street', 'NB', NULL, 25),
('King', 'king', 'Street', 'NB', NULL, 45),
('Queen', 'queen', 'Street', 'NB', NULL, 42),
('Park', 'park', 'Avenue', 'NB', NULL, 35),
('Westmorland', 'westmorland', 'Street', 'NB', 'Fredericton', 20);