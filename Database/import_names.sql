-- =============================================
-- Import Names from CSV file
-- For use in HeidiSQL or MySQL command line
-- =============================================

USE bor_db;

-- Method 1: Load first names from file
-- Replace path with your actual file location
LOAD DATA LOCAL INFILE 'C:/path/to/firstnames.csv'
INTO TABLE names
FIELDS TERMINATED BY ' '  -- Change to ',' for comma-delimited
LINES TERMINATED BY '\n'  -- Use '\r\n' for Windows files
(@name)
SET 
    name_lower = LOWER(TRIM(@name)),
    name_type = 'first',
    name_count = 1,
    last_seen = NOW()
ON DUPLICATE KEY UPDATE 
    name_count = name_count + 1,
    last_seen = NOW();

-- Method 2: Load last names from file
LOAD DATA LOCAL INFILE 'C:/path/to/lastnames.csv'
INTO TABLE names
FIELDS TERMINATED BY ' '
LINES TERMINATED BY '\n'
(@name)
SET 
    name_lower = LOWER(TRIM(@name)),
    name_type = 'last',
    name_count = 1,
    last_seen = NOW()
ON DUPLICATE KEY UPDATE 
    name_count = name_count + 1,
    last_seen = NOW();

-- Method 3: Load names with type specified in file
-- File format: name type (e.g., "john first" or "smith last")
LOAD DATA LOCAL INFILE 'C:/path/to/names_with_type.csv'
INTO TABLE names
FIELDS TERMINATED BY ' '
LINES TERMINATED BY '\n'
(@name, @type)
SET 
    name_lower = LOWER(TRIM(@name)),
    name_type = LOWER(TRIM(@type)),
    name_count = 1,
    last_seen = NOW()
ON DUPLICATE KEY UPDATE 
    name_count = name_count + 1,
    last_seen = NOW();

-- Verify import
SELECT 
    'Names Import Summary' AS Report,
    COUNT(*) AS total_names,
    SUM(CASE WHEN name_type = 'first' THEN 1 ELSE 0 END) AS first_names,
    SUM(CASE WHEN name_type = 'last' THEN 1 ELSE 0 END) AS last_names,
    SUM(CASE WHEN name_type = 'both' THEN 1 ELSE 0 END) AS both_types
FROM names;

-- Show sample of imported names
SELECT name_type, name_lower, name_count 
FROM names 
ORDER BY name_count DESC 
LIMIT 20;