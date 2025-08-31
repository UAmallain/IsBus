-- Create reference tables for phonebook parser
-- These tables replace hard-coded lists in the application

USE bor_db;

-- 1. Street Types Table
CREATE TABLE IF NOT EXISTS street_types (
    id INT AUTO_INCREMENT PRIMARY KEY,
    type_name VARCHAR(50) NOT NULL,
    type_abbr VARCHAR(20),
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    UNIQUE KEY uk_type_name (type_name),
    INDEX idx_type_abbr (type_abbr)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Populate street types
INSERT IGNORE INTO street_types (type_name, type_abbr) VALUES
('Street', 'St'),
('Avenue', 'Ave'),
('Avenue', 'Av'),
('Drive', 'Dr'),
('Road', 'Rd'),
('Boulevard', 'Blvd'),
('Lane', 'Ln'),
('Court', 'Ct'),
('Place', 'Pl'),
('Way', 'Way'),
('Parkway', 'Pkwy'),
('Terrace', 'Terr'),
('Circle', 'Cir'),
('Square', 'Sq'),
('Crescent', 'Cres'),
('Highway', 'Hwy'),
('Trail', 'Tr');

-- 2. Business Endings Table
CREATE TABLE IF NOT EXISTS business_endings (
    id INT AUTO_INCREMENT PRIMARY KEY,
    ending VARCHAR(50) NOT NULL,
    ending_lower VARCHAR(50) NOT NULL,
    full_form VARCHAR(100),
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    UNIQUE KEY uk_ending (ending),
    INDEX idx_ending_lower (ending_lower)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Populate business endings
INSERT IGNORE INTO business_endings (ending, ending_lower, full_form) VALUES
('Ltd', 'ltd', 'Limited'),
('Limited', 'limited', 'Limited'),
('Inc', 'inc', 'Incorporated'),
('Incorporated', 'incorporated', 'Incorporated'),
('Corp', 'corp', 'Corporation'),
('Corporation', 'corporation', 'Corporation'),
('LLC', 'llc', 'Limited Liability Company'),
('LLP', 'llp', 'Limited Liability Partnership'),
('Sons', 'sons', NULL),
('Bros', 'bros', 'Brothers'),
('Brothers', 'brothers', 'Brothers'),
('Co', 'co', 'Company'),
('Company', 'company', 'Company'),
('Sisters', 'sisters', NULL),
('and', 'and', NULL);

-- 3. Province Codes Table
CREATE TABLE IF NOT EXISTS province_codes (
    id INT AUTO_INCREMENT PRIMARY KEY,
    code VARCHAR(2) NOT NULL,
    name VARCHAR(100) NOT NULL,
    country VARCHAR(2) DEFAULT 'CA',
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    UNIQUE KEY uk_code (code),
    INDEX idx_country (country)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Populate province codes
INSERT IGNORE INTO province_codes (code, name) VALUES
('AB', 'Alberta'),
('BC', 'British Columbia'),
('MB', 'Manitoba'),
('NB', 'New Brunswick'),
('NL', 'Newfoundland and Labrador'),
('NS', 'Nova Scotia'),
('NT', 'Northwest Territories'),
('NU', 'Nunavut'),
('ON', 'Ontario'),
('PE', 'Prince Edward Island'),
('QC', 'Quebec'),
('SK', 'Saskatchewan'),
('YT', 'Yukon');

-- 4. Skip Words Table (words to ignore in parsing)
CREATE TABLE IF NOT EXISTS skip_words (
    id INT AUTO_INCREMENT PRIMARY KEY,
    word VARCHAR(50) NOT NULL,
    word_lower VARCHAR(50) NOT NULL,
    context VARCHAR(50) DEFAULT 'general',
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    UNIQUE KEY uk_word_context (word, context),
    INDEX idx_word_lower (word_lower),
    INDEX idx_context (context)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Populate skip words
INSERT IGNORE INTO skip_words (word, word_lower, context) VALUES
('the', 'the', 'general'),
('of', 'of', 'general'),
('de', 'de', 'general'),
('la', 'la', 'general'),
('le', 'le', 'general'),
('du', 'du', 'general'),
('des', 'des', 'general'),
('et', 'et', 'general'),
('&', '&', 'general'),
('and', 'and', 'business_name');

-- 5. Road Indicators Table
CREATE TABLE IF NOT EXISTS road_indicators (
    id INT AUTO_INCREMENT PRIMARY KEY,
    indicator VARCHAR(50) NOT NULL,
    indicator_lower VARCHAR(50) NOT NULL,
    indicator_type VARCHAR(50) DEFAULT 'suffix',
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    UNIQUE KEY uk_indicator (indicator),
    INDEX idx_indicator_lower (indicator_lower),
    INDEX idx_indicator_type (indicator_type)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Populate road indicators
INSERT IGNORE INTO road_indicators (indicator, indicator_lower, indicator_type) VALUES
('road', 'road', 'suffix'),
('rd', 'rd', 'suffix'),
('street', 'street', 'suffix'),
('st', 'st', 'suffix'),
('avenue', 'avenue', 'suffix'),
('ave', 'ave', 'suffix'),
('av', 'av', 'suffix'),
('drive', 'drive', 'suffix'),
('dr', 'dr', 'suffix'),
('lane', 'lane', 'suffix'),
('ln', 'ln', 'suffix'),
('way', 'way', 'suffix'),
('highway', 'highway', 'prefix'),
('hwy', 'hwy', 'prefix'),
('route', 'route', 'prefix'),
('rte', 'rte', 'prefix');

-- 6. Suite Indicators Table
CREATE TABLE IF NOT EXISTS suite_indicators (
    id INT AUTO_INCREMENT PRIMARY KEY,
    indicator VARCHAR(50) NOT NULL,
    indicator_lower VARCHAR(50) NOT NULL,
    requires_number BOOLEAN DEFAULT TRUE,
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    UNIQUE KEY uk_indicator (indicator),
    INDEX idx_indicator_lower (indicator_lower)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Populate suite indicators
INSERT IGNORE INTO suite_indicators (indicator, indicator_lower, requires_number) VALUES
('suite', 'suite', TRUE),
('unit', 'unit', TRUE),
('apt', 'apt', TRUE),
('apartment', 'apartment', TRUE),
('room', 'room', TRUE),
('rm', 'rm', TRUE),
('floor', 'floor', TRUE),
('fl', 'fl', TRUE),
('#', '#', TRUE);

-- 7. Business Context Words Table (for detecting business names)
CREATE TABLE IF NOT EXISTS business_context_words (
    id INT AUTO_INCREMENT PRIMARY KEY,
    word VARCHAR(50) NOT NULL,
    word_lower VARCHAR(50) NOT NULL,
    context_type VARCHAR(50) DEFAULT 'general',
    strength INT DEFAULT 50,
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    UNIQUE KEY uk_word (word),
    INDEX idx_word_lower (word_lower),
    INDEX idx_context_type (context_type),
    INDEX idx_strength (strength)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Populate business context words
INSERT IGNORE INTO business_context_words (word, word_lower, context_type, strength) VALUES
('services', 'services', 'general', 80),
('service', 'service', 'general', 80),
('company', 'company', 'general', 90),
('shop', 'shop', 'general', 85),
('store', 'store', 'general', 85),
('center', 'center', 'general', 75),
('centre', 'centre', 'general', 75),
('clinic', 'clinic', 'general', 85),
('office', 'office', 'general', 70),
('group', 'group', 'general', 70),
('enterprise', 'enterprise', 'general', 90),
('enterprises', 'enterprises', 'general', 90),
('business', 'business', 'general', 90),
('restaurant', 'restaurant', 'general', 95),
('cafe', 'cafe', 'general', 95),
('hotel', 'hotel', 'general', 95),
('motel', 'motel', 'general', 95),
('inn', 'inn', 'general', 90),
('garage', 'garage', 'general', 85),
('auto', 'auto', 'general', 80),
('automotive', 'automotive', 'general', 85),
('salon', 'salon', 'general', 90),
('spa', 'spa', 'general', 90),
('studio', 'studio', 'general', 80),
('mart', 'mart', 'general', 85),
('market', 'market', 'general', 85),
('pharmacy', 'pharmacy', 'general', 95),
('dental', 'dental', 'general', 85),
('medical', 'medical', 'general', 85);

-- Add indexes for better performance
CREATE INDEX idx_street_types_active ON street_types(is_active);
CREATE INDEX idx_business_endings_active ON business_endings(is_active);
CREATE INDEX idx_province_codes_active ON province_codes(is_active);
CREATE INDEX idx_skip_words_active ON skip_words(is_active);
CREATE INDEX idx_road_indicators_active ON road_indicators(is_active);
CREATE INDEX idx_suite_indicators_active ON suite_indicators(is_active);
CREATE INDEX idx_business_context_words_active ON business_context_words(is_active);

-- Verify tables were created
SELECT 'Reference tables created successfully' as status;
SELECT TABLE_NAME, TABLE_ROWS 
FROM information_schema.tables 
WHERE table_schema = 'bor_db' 
AND table_name IN (
    'street_types', 
    'business_endings', 
    'province_codes', 
    'skip_words', 
    'road_indicators', 
    'suite_indicators',
    'business_context_words'
);