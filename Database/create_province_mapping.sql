-- Create province mapping table for translating 2-char codes to full names
-- This maps the Statistics Canada province codes to full province names

CREATE TABLE IF NOT EXISTS province_mapping (
    id INT AUTO_INCREMENT PRIMARY KEY,
    province_code CHAR(2) NOT NULL UNIQUE,
    province_name VARCHAR(100) NOT NULL,
    province_name_french VARCHAR(100),
    region VARCHAR(50),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    INDEX idx_province_code (province_code),
    INDEX idx_province_name (province_name)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Insert Canadian provinces and territories
INSERT INTO province_mapping (province_code, province_name, province_name_french, region) VALUES
('NL', 'Newfoundland and Labrador', 'Terre-Neuve-et-Labrador', 'Atlantic'),
('PE', 'Prince Edward Island', 'Île-du-Prince-Édouard', 'Atlantic'),
('NS', 'Nova Scotia', 'Nouvelle-Écosse', 'Atlantic'),
('NB', 'New Brunswick', 'Nouveau-Brunswick', 'Atlantic'),
('QC', 'Quebec', 'Québec', 'Central'),
('ON', 'Ontario', 'Ontario', 'Central'),
('MB', 'Manitoba', 'Manitoba', 'Prairie'),
('SK', 'Saskatchewan', 'Saskatchewan', 'Prairie'),
('AB', 'Alberta', 'Alberta', 'Prairie'),
('BC', 'British Columbia', 'Colombie-Britannique', 'Pacific'),
('YT', 'Yukon', 'Yukon', 'Northern'),
('NT', 'Northwest Territories', 'Territoires du Nord-Ouest', 'Northern'),
('NU', 'Nunavut', 'Nunavut', 'Northern')
ON DUPLICATE KEY UPDATE 
    province_name = VALUES(province_name),
    province_name_french = VALUES(province_name_french),
    region = VALUES(region);

-- Create a view that joins road_network with province mapping for easier queries
CREATE OR REPLACE VIEW road_network_with_provinces AS
SELECT 
    rn.*,
    pm_left.province_name AS province_full_name_left,
    pm_right.province_name AS province_full_name_right
FROM road_network rn
LEFT JOIN province_mapping pm_left ON rn.province_uid_left = pm_left.province_code
LEFT JOIN province_mapping pm_right ON rn.province_uid_right = pm_right.province_code;

-- Create indexes on road_network table if they don't exist
-- These will optimize street name searches
CREATE INDEX IF NOT EXISTS idx_road_network_name_lower 
    ON road_network ((LOWER(name)));

CREATE INDEX IF NOT EXISTS idx_road_network_province_left 
    ON road_network (province_uid_left);

CREATE INDEX IF NOT EXISTS idx_road_network_province_right 
    ON road_network (province_uid_right);

-- Create a stored procedure for efficient street name search
DELIMITER $$

CREATE PROCEDURE IF NOT EXISTS SearchStreetNames(
    IN search_term VARCHAR(100),
    IN province_code CHAR(2),
    IN limit_results INT
)
BEGIN
    SET @search_pattern = CONCAT('%', LOWER(search_term), '%');
    
    IF province_code IS NULL OR province_code = '' THEN
        -- Search all provinces
        SELECT DISTINCT 
            name AS street_name,
            type AS street_type,
            COUNT(*) as occurrence_count,
            GROUP_CONCAT(DISTINCT COALESCE(csd_name_left, csd_name_right) SEPARATOR ', ') as communities
        FROM road_network
        WHERE LOWER(name) LIKE @search_pattern
        GROUP BY name, type
        ORDER BY occurrence_count DESC
        LIMIT limit_results;
    ELSE
        -- Search specific province
        SELECT DISTINCT 
            name AS street_name,
            type AS street_type,
            COUNT(*) as occurrence_count,
            GROUP_CONCAT(DISTINCT COALESCE(csd_name_left, csd_name_right) SEPARATOR ', ') as communities
        FROM road_network
        WHERE LOWER(name) LIKE @search_pattern
            AND (province_uid_left = province_code OR province_uid_right = province_code)
        GROUP BY name, type
        ORDER BY occurrence_count DESC
        LIMIT limit_results;
    END IF;
END$$

DELIMITER ;

-- Create a function to get province code from province name
DELIMITER $$

CREATE FUNCTION IF NOT EXISTS GetProvinceCode(province_name_input VARCHAR(100))
RETURNS CHAR(2)
DETERMINISTIC
READS SQL DATA
BEGIN
    DECLARE prov_code CHAR(2);
    
    -- Try exact match first
    SELECT province_code INTO prov_code
    FROM province_mapping
    WHERE LOWER(province_name) = LOWER(province_name_input)
       OR LOWER(province_name_french) = LOWER(province_name_input)
    LIMIT 1;
    
    -- If not found, try partial match
    IF prov_code IS NULL THEN
        SELECT province_code INTO prov_code
        FROM province_mapping
        WHERE LOWER(province_name) LIKE CONCAT('%', LOWER(province_name_input), '%')
           OR LOWER(province_name_french) LIKE CONCAT('%', LOWER(province_name_input), '%')
        LIMIT 1;
    END IF;
    
    RETURN prov_code;
END$$

DELIMITER ;