-- =============================================
-- Test Script: Verify Duplicate Handling in Name Imports
-- =============================================

USE bor_db;

-- =============================================
-- Test Setup: Create test data with duplicates
-- =============================================

-- Clear any existing test data
DELETE FROM names WHERE name_lower LIKE 'test%';

-- Insert initial test data
INSERT INTO names (name_lower, name_type, name_count, last_seen, created_at, updated_at)
VALUES 
    ('testjohn', 'first', 100, NOW(), NOW(), NOW()),
    ('testmary', 'first', 50, NOW(), NOW(), NOW()),
    ('testsmith', 'last', 200, NOW(), NOW(), NOW());

-- Show initial state
SELECT 'INITIAL STATE' AS Test_Stage;
SELECT name_lower, name_type, name_count 
FROM names 
WHERE name_lower LIKE 'test%'
ORDER BY name_lower;

-- =============================================
-- Test 1: Import duplicate first name (should increment count)
-- =============================================

-- Simulate importing testjohn again with count 25
INSERT INTO names (name_lower, name_type, name_count, last_seen, created_at, updated_at)
VALUES ('testjohn', 'first', 25, NOW(), NOW(), NOW())
ON DUPLICATE KEY UPDATE
    name_count = name_count + VALUES(name_count),
    last_seen = NOW(),
    updated_at = NOW();

SELECT 'TEST 1: After duplicate first name import' AS Test_Stage;
SELECT name_lower, name_type, name_count, 
       CASE WHEN name_count = 125 THEN 'PASS' ELSE 'FAIL' END AS result
FROM names 
WHERE name_lower = 'testjohn';

-- =============================================
-- Test 2: Import name with different type (first vs last)
-- =============================================

-- Import testjohn as a last name
INSERT INTO names (name_lower, name_type, name_count, last_seen, created_at, updated_at)
VALUES ('testjohn', 'last', 30, NOW(), NOW(), NOW())
ON DUPLICATE KEY UPDATE
    name_count = name_count + VALUES(name_count),
    last_seen = NOW(),
    updated_at = NOW();

SELECT 'TEST 2: After importing as different type' AS Test_Stage;
SELECT name_lower, name_type, name_count
FROM names 
WHERE name_lower = 'testjohn'
ORDER BY name_type;

-- =============================================
-- Test 3: Convert to 'both' type when name exists as first and last
-- =============================================

-- Run the consolidation logic
UPDATE names n1
INNER JOIN names n2 
    ON n1.name_lower = n2.name_lower
    AND n1.name_type = 'first'
    AND n2.name_type = 'last'
    AND n1.name_lower = 'testjohn'
SET n1.name_type = 'both',
    n1.name_count = n1.name_count + n2.name_count;

DELETE n2 FROM names n1
INNER JOIN names n2
    ON n1.name_lower = n2.name_lower
    AND n1.name_type = 'both'
    AND n2.name_type = 'last'
    AND n1.name_lower = 'testjohn';

SELECT 'TEST 3: After consolidation to both type' AS Test_Stage;
SELECT name_lower, name_type, name_count,
       CASE WHEN name_type = 'both' AND name_count = 155 THEN 'PASS' ELSE 'FAIL' END AS result
FROM names 
WHERE name_lower = 'testjohn';

-- =============================================
-- Test 4: Import batch with mixed duplicates
-- =============================================

-- Create temp table for batch test
DROP TEMPORARY TABLE IF EXISTS temp_batch_test;
CREATE TEMPORARY TABLE temp_batch_test (
    name_lower VARCHAR(100),
    name_type VARCHAR(10),
    name_count INT
);

INSERT INTO temp_batch_test VALUES
    ('testmary', 'first', 75),    -- Existing, should increment
    ('testjohn', 'both', 45),     -- Existing as 'both', should increment
    ('testrobert', 'first', 100), -- New name
    ('testsmith', 'last', 50),    -- Existing, should increment
    ('testjones', 'last', 150);   -- New name

-- Execute batch insert
INSERT INTO names (name_lower, name_type, name_count, last_seen, created_at, updated_at)
SELECT name_lower, name_type, name_count, NOW(), NOW(), NOW()
FROM temp_batch_test
ON DUPLICATE KEY UPDATE
    name_count = name_count + VALUES(name_count),
    last_seen = NOW(),
    updated_at = NOW();

SELECT 'TEST 4: After batch import with duplicates' AS Test_Stage;
SELECT name_lower, name_type, name_count,
       CASE 
           WHEN name_lower = 'testmary' AND name_count = 125 THEN 'PASS'
           WHEN name_lower = 'testjohn' AND name_count = 200 THEN 'PASS'
           WHEN name_lower = 'testrobert' AND name_count = 100 THEN 'PASS'
           WHEN name_lower = 'testsmith' AND name_count = 250 THEN 'PASS'
           WHEN name_lower = 'testjones' AND name_count = 150 THEN 'PASS'
           ELSE 'FAIL'
       END AS result
FROM names 
WHERE name_lower LIKE 'test%'
ORDER BY name_lower;

-- =============================================
-- Test 5: Verify last_seen and updated_at are updated
-- =============================================

-- Wait a moment then update testmary
DO SLEEP(1);

SET @old_updated = (SELECT updated_at FROM names WHERE name_lower = 'testmary');

INSERT INTO names (name_lower, name_type, name_count, last_seen, created_at, updated_at)
VALUES ('testmary', 'first', 1, NOW(), NOW(), NOW())
ON DUPLICATE KEY UPDATE
    name_count = name_count + VALUES(name_count),
    last_seen = NOW(),
    updated_at = NOW();

SELECT 'TEST 5: Timestamp update verification' AS Test_Stage;
SELECT name_lower, 
       CASE WHEN updated_at > @old_updated THEN 'PASS' ELSE 'FAIL' END AS timestamp_updated
FROM names 
WHERE name_lower = 'testmary';

-- =============================================
-- Test Summary
-- =============================================

SELECT 'FINAL TEST SUMMARY' AS Test_Stage;
SELECT 
    COUNT(*) AS total_test_records,
    SUM(CASE WHEN name_lower LIKE 'test%' THEN 1 ELSE 0 END) AS test_names,
    SUM(CASE WHEN name_type = 'first' THEN 1 ELSE 0 END) AS first_names,
    SUM(CASE WHEN name_type = 'last' THEN 1 ELSE 0 END) AS last_names,
    SUM(CASE WHEN name_type = 'both' THEN 1 ELSE 0 END) AS both_types
FROM names
WHERE name_lower LIKE 'test%';

-- =============================================
-- Cleanup test data
-- =============================================
DELETE FROM names WHERE name_lower LIKE 'test%';
DROP TEMPORARY TABLE IF EXISTS temp_batch_test;

SELECT 'TEST CLEANUP COMPLETE' AS Status;