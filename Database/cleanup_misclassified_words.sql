-- Cleanup misclassified words in the database
USE bor_db;

-- 1. Fix cardinal directions - these should not be business indicators
-- They often appear in addresses (e.g., "Salisbury West", "Beaubassin East")
UPDATE word_data 
SET word_count = LEAST(word_count, 100)  -- Cap at 100 to reduce strength
WHERE word_lower IN ('north', 'south', 'east', 'west', 'northwest', 'northeast', 'southwest', 'southeast')
AND word_type = 'business';

-- 2. Fix common first names misclassified as business
-- These should have much lower business counts
UPDATE word_data 
SET word_count = LEAST(word_count, 10)  -- Cap at 10 (weak indicator at most)
WHERE word_type = 'business'
AND word_lower IN (
    'jesus', 'devon', 'vernon', 'genesis', 'max', 'real', 'andre', 
    'brandon', 'luc', 'jim', 'randy', 'ms',
    'abdulsatar', 'alia', 'amberman', 'andrade', 'alward', 'aldred'
);

-- 3. Fix community names that are being treated as business indicators
-- These should not be business indicators at all
UPDATE word_data 
SET word_count = 0
WHERE word_type = 'business'
AND word_lower IN (
    'riverview', 'salisbury', 'fredericton', 'moncton', 'dieppe', 
    'shediac', 'buctouche', 'steeves', 'beaubassin', 'barachois',
    'grand', 'petit', 'mountain', 'hill', 'lake', 'river', 'creek',
    'grande', 'petite', 'saint', 'sainte', 'cap', 'port', 'bay',
    'pointe', 'anse', 'havre', 'village', 'ville'
);

-- 4. Reset "alley" - it's a last name, not the business word
UPDATE word_data 
SET word_count = 0
WHERE word_type = 'business'
AND word_lower = 'alley';

-- 5. Reset "arc" - likely a name, not a business
UPDATE word_data 
SET word_count = 0
WHERE word_type = 'business'
AND word_lower = 'arc';

-- 6. Show the changes
SELECT 'Fixed cardinal directions:' as Category;
SELECT word_lower, word_type, word_count 
FROM word_data 
WHERE word_lower IN ('north', 'south', 'east', 'west')
AND word_type = 'business';

SELECT 'Fixed first names:' as Category;
SELECT word_lower, word_type, word_count 
FROM word_data 
WHERE word_lower IN ('jesus', 'devon', 'vernon', 'genesis', 'max', 'brandon')
AND word_type = 'business';

SELECT 'Fixed community names:' as Category;
SELECT word_lower, word_type, word_count 
FROM word_data 
WHERE word_lower IN ('riverview', 'salisbury', 'grand')
AND word_type = 'business';