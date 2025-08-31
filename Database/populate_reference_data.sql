-- Populate reference data using existing tables
USE bor_db;

-- 1. Populate business_indicators table with business endings and suffixes
INSERT IGNORE INTO business_indicators (indicator_text, indicator_type, weight) VALUES
-- Primary business suffixes (highest weight)
('Ltd', 'primary_suffix', 100),
('Limited', 'primary_suffix', 100),
('Inc', 'primary_suffix', 100),
('Incorporated', 'primary_suffix', 100),
('Corp', 'primary_suffix', 100),
('Corporation', 'primary_suffix', 100),
('LLC', 'primary_suffix', 95),
('LLP', 'primary_suffix', 95),
('Co', 'primary_suffix', 90),
('Company', 'primary_suffix', 90),

-- Secondary business indicators
('Sons', 'secondary_indicator', 80),
('Bros', 'secondary_indicator', 80),
('Brothers', 'secondary_indicator', 80),
('Sisters', 'secondary_indicator', 75),
('Services', 'secondary_indicator', 85),
('Service', 'secondary_indicator', 85),
('Shop', 'secondary_indicator', 85),
('Store', 'secondary_indicator', 85),
('Center', 'secondary_indicator', 75),
('Centre', 'secondary_indicator', 75),
('Clinic', 'secondary_indicator', 85),
('Office', 'secondary_indicator', 70),
('Group', 'secondary_indicator', 70),
('Enterprise', 'secondary_indicator', 90),
('Enterprises', 'secondary_indicator', 90),
('Business', 'secondary_indicator', 90),
('Restaurant', 'secondary_indicator', 95),
('Cafe', 'secondary_indicator', 95),
('Hotel', 'secondary_indicator', 95),
('Motel', 'secondary_indicator', 95),
('Inn', 'secondary_indicator', 90),
('Garage', 'secondary_indicator', 85),
('Auto', 'secondary_indicator', 80),
('Automotive', 'secondary_indicator', 85),
('Salon', 'secondary_indicator', 90),
('Spa', 'secondary_indicator', 90),
('Studio', 'secondary_indicator', 80),
('Mart', 'secondary_indicator', 85),
('Market', 'secondary_indicator', 85),
('Pharmacy', 'secondary_indicator', 95),
('Dental', 'secondary_indicator', 85),
('Medical', 'secondary_indicator', 85),

-- Stop words (words to skip in analysis)
('the', 'stop_word', 0),
('of', 'stop_word', 0),
('de', 'stop_word', 0),
('la', 'stop_word', 0),
('le', 'stop_word', 0),
('du', 'stop_word', 0),
('des', 'stop_word', 0),
('et', 'stop_word', 0),
('&', 'stop_word', 0),
('and', 'stop_word', 10); -- 'and' has some weight as it can indicate business

-- 2. Ensure province_mapping has all Canadian provinces
INSERT IGNORE INTO province_mapping (province_code, province_name, province_name_french, region) VALUES
('AB', 'Alberta', 'Alberta', 'Western'),
('BC', 'British Columbia', 'Colombie-Britannique', 'Western'),
('MB', 'Manitoba', 'Manitoba', 'Western'),
('NB', 'New Brunswick', 'Nouveau-Brunswick', 'Atlantic'),
('NL', 'Newfoundland and Labrador', 'Terre-Neuve-et-Labrador', 'Atlantic'),
('NS', 'Nova Scotia', 'Nouvelle-Écosse', 'Atlantic'),
('NT', 'Northwest Territories', 'Territoires du Nord-Ouest', 'Northern'),
('NU', 'Nunavut', 'Nunavut', 'Northern'),
('ON', 'Ontario', 'Ontario', 'Central'),
('PE', 'Prince Edward Island', 'Île-du-Prince-Édouard', 'Atlantic'),
('QC', 'Quebec', 'Québec', 'Central'),
('SK', 'Saskatchewan', 'Saskatchewan', 'Western'),
('YT', 'Yukon', 'Yukon', 'Northern');

-- 3. Ensure street_type_mapping has common abbreviations
-- (Most should already be there from road_network data)
INSERT IGNORE INTO street_type_mapping (abbreviation, full_name, category, is_primary) VALUES
('St', 'Street', 'Road', 1),
('Ave', 'Avenue', 'Road', 1),
('Av', 'Avenue', 'Road', 1),
('Dr', 'Drive', 'Road', 1),
('Rd', 'Road', 'Road', 1),
('Blvd', 'Boulevard', 'Road', 1),
('Ln', 'Lane', 'Road', 1),
('Ct', 'Court', 'Road', 1),
('Pl', 'Place', 'Road', 1),
('Way', 'Way', 'Road', 1),
('Pkwy', 'Parkway', 'Road', 1),
('Terr', 'Terrace', 'Road', 1),
('Cir', 'Circle', 'Road', 1),
('Sq', 'Square', 'Road', 1),
('Cres', 'Crescent', 'Road', 1),
('Hwy', 'Highway', 'Road', 1),
('Tr', 'Trail', 'Road', 1);

-- 4. Create a view for suite/unit indicators (stored as business indicators)
INSERT IGNORE INTO business_indicators (indicator_text, indicator_type, weight) VALUES
('Suite', 'secondary_indicator', 30),
('Unit', 'secondary_indicator', 30),
('Apt', 'secondary_indicator', 30),
('Apartment', 'secondary_indicator', 30),
('Room', 'secondary_indicator', 25),
('Rm', 'secondary_indicator', 25),
('Floor', 'secondary_indicator', 25),
('Fl', 'secondary_indicator', 25),
('#', 'secondary_indicator', 20);

-- Verify data was inserted
SELECT indicator_type, COUNT(*) as count 
FROM business_indicators 
GROUP BY indicator_type;

SELECT COUNT(*) as province_count FROM province_mapping;

SELECT COUNT(*) as street_type_count FROM street_type_mapping;