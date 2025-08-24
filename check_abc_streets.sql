-- Check what "ABC" streets exist in the database
-- This will show us why the parser thinks "ABC" is a street

-- Check for exact match "ABC"
SELECT 
    ngd_uid,
    name,
    type,
    CONCAT(COALESCE(csd_name_left, csd_name_right), ', ', COALESCE(province_uid_left, province_uid_right)) as location,
    COUNT(*) OVER() as total_count
FROM road_network 
WHERE LOWER(name) = 'abc'
LIMIT 20;

-- Also check for streets starting with "ABC"
SELECT 
    ngd_uid,
    name,
    type,
    CONCAT(COALESCE(csd_name_left, csd_name_right), ', ', COALESCE(province_uid_left, province_uid_right)) as location,
    COUNT(*) OVER() as total_count
FROM road_network 
WHERE LOWER(name) LIKE 'abc%'
LIMIT 20;

-- Check for "Company" as a street name
SELECT 
    ngd_uid,
    name,
    type,
    CONCAT(COALESCE(csd_name_left, csd_name_right), ', ', COALESCE(province_uid_left, province_uid_right)) as location,
    COUNT(*) OVER() as total_count
FROM road_network 
WHERE LOWER(name) = 'company'
LIMIT 20;

-- Check for "Mountain" as a street name  
SELECT 
    ngd_uid,
    name,
    type,
    CONCAT(COALESCE(csd_name_left, csd_name_right), ', ', COALESCE(province_uid_left, province_uid_right)) as location,
    COUNT(*) OVER() as total_count
FROM road_network 
WHERE LOWER(name) = 'mountain'
LIMIT 20;