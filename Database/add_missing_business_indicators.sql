-- Add missing strong business indicators
INSERT INTO business_indicators (indicator_text, indicator_type, weight, is_active) VALUES
-- Medical/Health
('Optometrist', 'primary_suffix', 100, 1),
('Phys', 'secondary_indicator', 90, 1),  -- Physician
('Dr', 'title', 60, 1),  -- Can be doctor title

-- Retail/Services  
('Mall', 'primary_suffix', 95, 1),
('Shoppers', 'secondary_indicator', 85, 1),
('Bookmart', 'primary_suffix', 100, 1),
('Lighting', 'secondary_indicator', 90, 1),
('Bath', 'secondary_indicator', 85, 1),
('Appraisals', 'primary_suffix', 100, 1),
('Paints', 'primary_suffix', 95, 1),

-- Food Service
('Chef', 'primary_suffix', 95, 1),
('brstr', 'secondary_indicator', 85, 1),  -- Barista

-- Emergency/Support Services
('Crisis', 'secondary_indicator', 90, 1),
('Line', 'secondary_indicator', 70, 1),

-- Professional titles/services
('ofc', 'secondary_indicator', 80, 1)  -- office abbreviation

ON DUPLICATE KEY UPDATE 
    weight = VALUES(weight),
    is_active = VALUES(is_active);

-- Also ensure "Medical" is properly weighted
UPDATE business_indicators 
SET weight = 95 
WHERE indicator_text = 'Medical' 
  AND weight < 95;