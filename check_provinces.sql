-- Check what province values are actually in the database
SELECT DISTINCT province_uid_left, province_uid_right 
FROM road_network 
WHERE province_uid_left IS NOT NULL OR province_uid_right IS NOT NULL
LIMIT 20;

-- Count records by province
SELECT 
    COALESCE(province_uid_left, province_uid_right) as province,
    COUNT(*) as count
FROM road_network
GROUP BY COALESCE(province_uid_left, province_uid_right)
ORDER BY count DESC;