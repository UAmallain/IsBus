-- Create street type abbreviation mapping table
-- Maps common street type abbreviations to their full names

CREATE TABLE IF NOT EXISTS street_type_mapping (
    id INT AUTO_INCREMENT PRIMARY KEY,
    abbreviation VARCHAR(20) NOT NULL,
    full_name VARCHAR(50) NOT NULL,
    french_name VARCHAR(50),
    category VARCHAR(30),
    is_primary BOOLEAN DEFAULT TRUE,
    display_order INT DEFAULT 100,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    UNIQUE KEY idx_abbreviation (abbreviation),
    INDEX idx_full_name (full_name),
    INDEX idx_category (category)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Insert common street type abbreviations
-- Based on Canadian postal standards and common usage
INSERT INTO street_type_mapping (abbreviation, full_name, french_name, category, is_primary, display_order) VALUES
-- Primary road types
('st', 'Street', 'Rue', 'road', TRUE, 1),
('ave', 'Avenue', 'Avenue', 'road', TRUE, 2),
('rd', 'Road', 'Chemin', 'road', TRUE, 3),
('dr', 'Drive', 'Promenade', 'road', TRUE, 4),
('blvd', 'Boulevard', 'Boulevard', 'road', TRUE, 5),
('way', 'Way', 'Voie', 'road', TRUE, 6),
('pl', 'Place', 'Place', 'road', TRUE, 7),
('cres', 'Crescent', 'Croissant', 'road', TRUE, 8),
('ct', 'Court', 'Cour', 'road', TRUE, 9),
('ln', 'Lane', 'Ruelle', 'road', TRUE, 10),
('terr', 'Terrace', 'Terrasse', 'road', TRUE, 11),
('cir', 'Circle', 'Cercle', 'road', TRUE, 12),
('sq', 'Square', 'Carré', 'road', TRUE, 13),
('pk', 'Park', 'Parc', 'road', TRUE, 14),
('pkwy', 'Parkway', 'Promenade', 'road', TRUE, 15),

-- Highway types
('hwy', 'Highway', 'Autoroute', 'highway', TRUE, 20),
('fwy', 'Freeway', 'Autoroute', 'highway', TRUE, 21),
('expy', 'Expressway', 'Voie rapide', 'highway', TRUE, 22),
('tpke', 'Turnpike', 'Autoroute à péage', 'highway', FALSE, 23),
('rte', 'Route', 'Route', 'highway', TRUE, 24),

-- Secondary road types
('tr', 'Trail', 'Sentier', 'road', TRUE, 30),
('path', 'Path', 'Sentier', 'road', TRUE, 31),
('walk', 'Walk', 'Promenade', 'road', TRUE, 32),
('row', 'Row', 'Rangée', 'road', TRUE, 33),
('gate', 'Gate', 'Porte', 'road', TRUE, 34),
('grn', 'Green', 'Vert', 'road', TRUE, 35),
('mall', 'Mall', 'Mail', 'road', TRUE, 36),
('alley', 'Alley', 'Allée', 'road', TRUE, 37),
('loop', 'Loop', 'Boucle', 'road', TRUE, 38),
('ramp', 'Ramp', 'Bretelle', 'road', TRUE, 39),

-- Geographic features
('bay', 'Bay', 'Baie', 'geographic', TRUE, 40),
('beach', 'Beach', 'Plage', 'geographic', TRUE, 41),
('bend', 'Bend', 'Courbe', 'geographic', TRUE, 42),
('cape', 'Cape', 'Cap', 'geographic', TRUE, 43),
('cliff', 'Cliff', 'Falaise', 'geographic', TRUE, 44),
('cove', 'Cove', 'Anse', 'geographic', TRUE, 45),
('dale', 'Dale', 'Vallée', 'geographic', TRUE, 46),
('dell', 'Dell', 'Vallon', 'geographic', TRUE, 47),
('glen', 'Glen', 'Vallon', 'geographic', TRUE, 48),
('grove', 'Grove', 'Bosquet', 'geographic', TRUE, 49),
('hill', 'Hill', 'Colline', 'geographic', TRUE, 50),
('hts', 'Heights', 'Hauteurs', 'geographic', TRUE, 51),
('island', 'Island', 'Île', 'geographic', TRUE, 52),
('isle', 'Isle', 'Île', 'geographic', FALSE, 53),
('lake', 'Lake', 'Lac', 'geographic', TRUE, 54),
('mdw', 'Meadow', 'Prairie', 'geographic', TRUE, 55),
('mtn', 'Mountain', 'Montagne', 'geographic', TRUE, 56),
('orch', 'Orchard', 'Verger', 'geographic', TRUE, 57),
('pt', 'Point', 'Pointe', 'geographic', TRUE, 58),
('ridge', 'Ridge', 'Crête', 'geographic', TRUE, 59),
('shore', 'Shore', 'Rive', 'geographic', TRUE, 60),
('vale', 'Vale', 'Vallée', 'geographic', TRUE, 61),
('valley', 'Valley', 'Vallée', 'geographic', TRUE, 62),
('view', 'View', 'Vue', 'geographic', TRUE, 63),
('wood', 'Wood', 'Bois', 'geographic', TRUE, 64),
('woods', 'Woods', 'Bois', 'geographic', FALSE, 65),

-- Development types
('gdns', 'Gardens', 'Jardins', 'development', TRUE, 70),
('est', 'Estate', 'Domaine', 'development', TRUE, 71),
('mnr', 'Manor', 'Manoir', 'development', TRUE, 72),
('villas', 'Villas', 'Villas', 'development', TRUE, 73),
('villg', 'Village', 'Village', 'development', TRUE, 74),
('cmn', 'Common', 'Commune', 'development', TRUE, 75),
('ctr', 'Centre', 'Centre', 'development', TRUE, 76),
('plz', 'Plaza', 'Plaza', 'development', TRUE, 77),

-- Additional abbreviations
('av', 'Avenue', 'Avenue', 'road', FALSE, 80),
('boul', 'Boulevard', 'Boulevard', 'road', FALSE, 81),
('ch', 'Chemin', 'Chemin', 'road', FALSE, 82),
('crcl', 'Circle', 'Cercle', 'road', FALSE, 83),
('cresc', 'Crescent', 'Croissant', 'road', FALSE, 84),
('crt', 'Court', 'Cour', 'road', FALSE, 85),
('drv', 'Drive', 'Promenade', 'road', FALSE, 86),
('gdn', 'Garden', 'Jardin', 'development', FALSE, 87),
('hrbr', 'Harbor', 'Port', 'geographic', FALSE, 88),
('ht', 'Height', 'Hauteur', 'geographic', FALSE, 89),
('jct', 'Junction', 'Jonction', 'road', FALSE, 90),
('ldg', 'Landing', 'Débarcadère', 'geographic', FALSE, 91),
('mt', 'Mount', 'Mont', 'geographic', FALSE, 92),
('pky', 'Parkway', 'Promenade', 'road', FALSE, 93),
('psge', 'Passage', 'Passage', 'road', FALSE, 94),
('rdg', 'Ridge', 'Crête', 'geographic', FALSE, 95),
('run', 'Run', 'Voie', 'road', FALSE, 96),
('str', 'Street', 'Rue', 'road', FALSE, 97),
('ter', 'Terrace', 'Terrasse', 'road', FALSE, 98),
('trl', 'Trail', 'Sentier', 'road', FALSE, 99),
('wy', 'Way', 'Voie', 'road', FALSE, 100),

-- Canadian-specific
('conc', 'Concession', 'Concession', 'road', TRUE, 101),
('line', 'Line', 'Ligne', 'road', TRUE, 102),
('rg', 'Range', 'Rang', 'road', TRUE, 103),
('rang', 'Rang', 'Rang', 'road', FALSE, 104),
('sdrd', 'Sideroad', 'Route secondaire', 'road', TRUE, 105),

-- Quebec-specific French abbreviations
('rue', 'Rue', 'Street', 'road', FALSE, 110),
('boul', 'Boulevard', 'Boulevard', 'road', FALSE, 111),
('av', 'Avenue', 'Avenue', 'road', FALSE, 112),
('ch', 'Chemin', 'Road', 'road', FALSE, 113),
('mont', 'Montée', 'Hill', 'road', FALSE, 114),
('imp', 'Impasse', 'Dead End', 'road', FALSE, 115),

-- Corner cases and variations
('street', 'Street', 'Rue', 'road', FALSE, 120),
('avenue', 'Avenue', 'Avenue', 'road', FALSE, 121),
('road', 'Road', 'Chemin', 'road', FALSE, 122),
('drive', 'Drive', 'Promenade', 'road', FALSE, 123),
('boulevard', 'Boulevard', 'Boulevard', 'road', FALSE, 124),
('place', 'Place', 'Place', 'road', FALSE, 125),
('court', 'Court', 'Cour', 'road', FALSE, 126),
('lane', 'Lane', 'Ruelle', 'road', FALSE, 127),
('crescent', 'Crescent', 'Croissant', 'road', FALSE, 128),
('circle', 'Circle', 'Cercle', 'road', FALSE, 129),
('highway', 'Highway', 'Autoroute', 'highway', FALSE, 130),
('route', 'Route', 'Route', 'highway', FALSE, 131),
('trail', 'Trail', 'Sentier', 'road', FALSE, 132),
('terrace', 'Terrace', 'Terrasse', 'road', FALSE, 133),
('park', 'Park', 'Parc', 'road', FALSE, 134),
('parkway', 'Parkway', 'Promenade', 'road', FALSE, 135),
('square', 'Square', 'Carré', 'road', FALSE, 136)
ON DUPLICATE KEY UPDATE 
    full_name = VALUES(full_name),
    french_name = VALUES(french_name),
    category = VALUES(category),
    is_primary = VALUES(is_primary),
    display_order = VALUES(display_order);

-- Create a function to normalize street types
DELIMITER $$

CREATE FUNCTION IF NOT EXISTS NormalizeStreetType(input_type VARCHAR(50))
RETURNS VARCHAR(50)
DETERMINISTIC
READS SQL DATA
BEGIN
    DECLARE normalized_type VARCHAR(50);
    DECLARE input_lower VARCHAR(50);
    
    SET input_lower = LOWER(TRIM(input_type));
    
    -- First try exact match on abbreviation
    SELECT full_name INTO normalized_type
    FROM street_type_mapping
    WHERE LOWER(abbreviation) = input_lower
    ORDER BY is_primary DESC
    LIMIT 1;
    
    -- If not found, try exact match on full name
    IF normalized_type IS NULL THEN
        SELECT full_name INTO normalized_type
        FROM street_type_mapping
        WHERE LOWER(full_name) = input_lower
        LIMIT 1;
    END IF;
    
    -- If still not found, return the original input
    IF normalized_type IS NULL THEN
        SET normalized_type = input_type;
    END IF;
    
    RETURN normalized_type;
END$$

DELIMITER ;

-- Create a function to get abbreviation from full name
DELIMITER $$

CREATE FUNCTION IF NOT EXISTS GetStreetAbbreviation(input_name VARCHAR(50))
RETURNS VARCHAR(20)
DETERMINISTIC
READS SQL DATA
BEGIN
    DECLARE abbr VARCHAR(20);
    DECLARE input_lower VARCHAR(50);
    
    SET input_lower = LOWER(TRIM(input_name));
    
    -- Get the primary abbreviation for this full name
    SELECT abbreviation INTO abbr
    FROM street_type_mapping
    WHERE LOWER(full_name) = input_lower
    ORDER BY is_primary DESC, display_order ASC
    LIMIT 1;
    
    -- If not found, check if input is already an abbreviation
    IF abbr IS NULL THEN
        SELECT abbreviation INTO abbr
        FROM street_type_mapping
        WHERE LOWER(abbreviation) = input_lower
        LIMIT 1;
    END IF;
    
    RETURN abbr;
END$$

DELIMITER ;

-- Create a view for commonly used street types with statistics from road_network
CREATE OR REPLACE VIEW street_type_statistics AS
SELECT 
    stm.abbreviation,
    stm.full_name,
    stm.category,
    stm.is_primary,
    COUNT(DISTINCT rn.id) as usage_count,
    COUNT(DISTINCT rn.name) as unique_street_count
FROM street_type_mapping stm
LEFT JOIN road_network rn ON LOWER(rn.type) = LOWER(stm.abbreviation)
GROUP BY stm.id, stm.abbreviation, stm.full_name, stm.category, stm.is_primary
ORDER BY usage_count DESC;

-- Create stored procedure to search streets with normalized types
DELIMITER $$

CREATE PROCEDURE IF NOT EXISTS SearchStreetsNormalized(
    IN search_term VARCHAR(100),
    IN province_code CHAR(2),
    IN normalize_types BOOLEAN,
    IN limit_results INT
)
BEGIN
    SET @search_pattern = CONCAT('%', LOWER(search_term), '%');
    
    IF normalize_types THEN
        IF province_code IS NULL OR province_code = '' THEN
            SELECT DISTINCT 
                name AS street_name,
                type AS street_type_original,
                NormalizeStreetType(type) AS street_type_normalized,
                COUNT(*) as occurrence_count,
                GROUP_CONCAT(DISTINCT COALESCE(csd_name_left, csd_name_right) SEPARATOR ', ') as communities
            FROM road_network
            WHERE LOWER(name) LIKE @search_pattern
            GROUP BY name, type
            ORDER BY occurrence_count DESC
            LIMIT limit_results;
        ELSE
            SELECT DISTINCT 
                name AS street_name,
                type AS street_type_original,
                NormalizeStreetType(type) AS street_type_normalized,
                COUNT(*) as occurrence_count,
                GROUP_CONCAT(DISTINCT COALESCE(csd_name_left, csd_name_right) SEPARATOR ', ') as communities
            FROM road_network
            WHERE LOWER(name) LIKE @search_pattern
                AND (province_uid_left = province_code OR province_uid_right = province_code)
            GROUP BY name, type
            ORDER BY occurrence_count DESC
            LIMIT limit_results;
        END IF;
    ELSE
        -- Use existing search without normalization
        CALL SearchStreetNames(search_term, province_code, limit_results);
    END IF;
END$$

DELIMITER ;