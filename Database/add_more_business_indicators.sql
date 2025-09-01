-- Add MORE missing business indicators that are OBVIOUS business keywords
INSERT INTO business_indicators (indicator_text, indicator_type, weight, is_active) VALUES
-- From the failed examples
('Willow', 'secondary_indicator', 60, 1),  -- Could be part of business name like "A Bend In The Willow"
('Sound', 'primary_suffix', 90, 1),  -- Audio/music business
('Lighting', 'primary_suffix', 95, 1),  -- Lighting business
('Bath', 'primary_suffix', 90, 1),  -- Bath/bathroom business
('Shoppers', 'secondary_indicator', 85, 1),  -- Store name
('Mall', 'primary_suffix', 100, 1),  -- ALWAYS business
('brstr', 'secondary_indicator', 85, 1),  -- Barista
('Paints', 'primary_suffix', 95, 1),  -- Paint store
('Benmarks', 'primary_suffix', 100, 1),  -- Store name
('ofc', 'secondary_indicator', 85, 1),  -- Office abbreviation
('Clinic', 'primary_suffix', 100, 1),  -- Medical clinic - ALWAYS business
('Medical', 'secondary_indicator', 95, 1),  -- Medical anything
('Western', 'secondary_indicator', 50, 1),  -- Could be part of business name
('Optometrist', 'primary_suffix', 100, 1),  -- ALWAYS business
('Chef', 'primary_suffix', 95, 1),  -- Restaurant/food
('Appraisals', 'primary_suffix', 100, 1),  -- ALWAYS business
('Crisis', 'secondary_indicator', 90, 1),  -- Crisis center/line
('Line', 'secondary_indicator', 70, 1)  -- Phone line, crisis line, etc.
ON DUPLICATE KEY UPDATE 
    weight = VALUES(weight),
    is_active = VALUES(is_active);

-- Also update existing entries to ensure proper weights
UPDATE business_indicators SET weight = 100 WHERE indicator_text = 'Clinic' AND weight < 100;
UPDATE business_indicators SET weight = 95 WHERE indicator_text = 'Medical' AND weight < 95;

-- Add "Dr" as it appears in many business contexts (Dr office, etc.)
INSERT INTO business_indicators (indicator_text, indicator_type, weight, is_active) VALUES
('Dr', 'title', 60, 1)
ON DUPLICATE KEY UPDATE weight = 60;