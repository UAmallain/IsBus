-- Migration Script: Move all static lists to word_data table
-- Using only valid word_type enum values: 'first','last','both','business','indeterminate'
--
-- Special count value ranges to identify word categories:
-- 99999 = Corporate suffixes (absolute business indicators)
-- 95000-98999 = Titles/honorifics (stored as 'indeterminate')
-- 90000-94999 = Strong business words
-- 85000-89999 = Highway/road indicators (stored as 'indeterminate')
-- 80000-84999 = Location keywords (stored as 'indeterminate')
-- 75000-79999 = Special business patterns
-- 5000-74999 = Various strength business words
-- 1000-4999 = Medium/weak business words

-- ====================================
-- 1. CORPORATE SUFFIXES (Absolute business indicators)
-- word_type = 'business', count = 99999
-- ====================================
INSERT INTO word_data (word_lower, word_type, word_count) VALUES
('inc', 'business', 99999),
('incorporated', 'business', 99999),
('corp', 'business', 99999),
('corporation', 'business', 99999),
('ltd', 'business', 99999),
('limited', 'business', 99999),
('llc', 'business', 99999),
('llp', 'business', 99999),
('lp', 'business', 99999),
('plc', 'business', 99999),
('gmbh', 'business', 99999),
('ag', 'business', 99999),
('sa', 'business', 99999),
('nv', 'business', 99999),
('bv', 'business', 99999),
('co', 'business', 99999),
('company', 'business', 99999)
ON DUPLICATE KEY UPDATE word_count = GREATEST(word_count, 99999);

-- ====================================
-- 2. TITLES/HONORIFICS 
-- word_type = 'indeterminate', count = 95000-98999
-- These help identify residential names
-- ====================================
INSERT INTO word_data (word_lower, word_type, word_count) VALUES
('mr', 'indeterminate', 95000),
('mrs', 'indeterminate', 95000),
('ms', 'indeterminate', 95000),
('miss', 'indeterminate', 95000),
('dr', 'indeterminate', 95000),
('doctor', 'indeterminate', 95000),
('prof', 'indeterminate', 95000),
('professor', 'indeterminate', 95000),
('rev', 'indeterminate', 95000),
('reverend', 'indeterminate', 95000),
('hon', 'indeterminate', 95000),
('honourable', 'indeterminate', 95000),
('sir', 'indeterminate', 95000),
('dame', 'indeterminate', 95000),
('lord', 'indeterminate', 95000),
('lady', 'indeterminate', 95000)
ON DUPLICATE KEY UPDATE 
    word_type = CASE 
        WHEN word_type = 'business' AND word_count < 95000 THEN 'indeterminate'
        ELSE word_type 
    END,
    word_count = GREATEST(word_count, 95000);

-- ====================================
-- 3. STRONG BUSINESS WORDS
-- word_type = 'business', count = 90000-94999
-- ====================================
INSERT INTO word_data (word_lower, word_type, word_count) VALUES
('enterprises', 'business', 92000),
('holdings', 'business', 92000),
('ventures', 'business', 91000),
('investments', 'business', 91000),
('capital', 'business', 91000),
('group', 'business', 90000),
('partners', 'business', 90000),
('associates', 'business', 90000),
('solutions', 'business', 90000),
('services', 'business', 90000),
('systems', 'business', 90000),
('technologies', 'business', 90000),
('consulting', 'business', 90000),
('consultants', 'business', 90000)
ON DUPLICATE KEY UPDATE word_count = GREATEST(word_count, VALUES(word_count));

-- ====================================
-- 4. BUSINESS ESTABLISHMENT TYPES
-- word_type = 'business', count based on strength
-- ====================================
INSERT INTO word_data (word_lower, word_type, word_count) VALUES
-- Retail/Commercial (70000-89999)
('store', 'business', 80000),
('stores', 'business', 80000),
('shop', 'business', 80000),
('shops', 'business', 80000),
('mart', 'business', 75000),
('market', 'business', 75000),
('boutique', 'business', 85000),
('outlet', 'business', 80000),
('outlets', 'business', 80000),
('retail', 'business', 85000),
('wholesale', 'business', 85000),
('trading', 'business', 80000),
('traders', 'business', 80000),
('imports', 'business', 80000),
('exports', 'business', 80000),
('distributors', 'business', 85000),
('distribution', 'business', 80000),
('supply', 'business', 75000),
('supplies', 'business', 75000),
('suppliers', 'business', 80000),

-- Food/Restaurant (80000-89999)
('restaurant', 'business', 88000),
('restaurants', 'business', 88000),
('cafe', 'business', 87000),
('cafeteria', 'business', 87000),
('bistro', 'business', 87000),
('grill', 'business', 86000),
('grille', 'business', 86000),
('kitchen', 'business', 82000),
('kitchens', 'business', 82000),
('pizza', 'business', 85000),
('pizzeria', 'business', 87000),
('sushi', 'business', 85000),
('bakery', 'business', 87000),
('deli', 'business', 86000),
('delicatessen', 'business', 87000),
('pub', 'business', 86000),
('bar', 'business', 81000),
('tavern', 'business', 86000),
('lounge', 'business', 82000),
('club', 'business', 75000),
('catering', 'business', 85000),

-- Health/Medical (80000-89999)
('clinic', 'business', 88000),
('clinics', 'business', 88000),
('medical', 'business', 87000),
('dental', 'business', 87000),
('pharmacy', 'business', 88000),
('pharmacies', 'business', 88000),
('health', 'business', 70000),
('healthcare', 'business', 87000),
('wellness', 'business', 75000),
('therapy', 'business', 80000),
('therapeutic', 'business', 80000),
('rehabilitation', 'business', 85000),
('laboratory', 'business', 85000),
('labs', 'business', 85000),
('diagnostics', 'business', 85000),

-- Beauty/Personal Care (80000-89999)
('salon', 'business', 87000),
('salons', 'business', 87000),
('spa', 'business', 86000),
('spas', 'business', 86000),
('barbershop', 'business', 87000),
('barber', 'business', 85000),
('hairstyling', 'business', 85000),
('hairdressing', 'business', 85000),
('beauty', 'business', 75000),
('cosmetics', 'business', 80000),
('aesthetics', 'business', 80000),

-- Fitness/Sports (70000-85000)
('fitness', 'business', 85000),
('gym', 'business', 85000),
('gymnasium', 'business', 85000),
('athletic', 'business', 75000),
('athletics', 'business', 75000),
('sports', 'business', 70000),
('sporting', 'business', 70000),
('recreation', 'business', 70000),
('recreational', 'business', 70000),

-- Hospitality (80000-89999)
('hotel', 'business', 88000),
('hotels', 'business', 88000),
('motel', 'business', 87000),
('motels', 'business', 87000),
('inn', 'business', 82000),
('inns', 'business', 82000),
('lodge', 'business', 82000),
('lodging', 'business', 83000),
('resort', 'business', 87000),
('resorts', 'business', 87000),
('suites', 'business', 83000),
('accommodations', 'business', 82000),

-- Automotive (80000-89999)
('automotive', 'business', 88000),
('motors', 'business', 87000),
('motor', 'business', 75000),
('auto', 'business', 75000),
('automobile', 'business', 80000),
('cars', 'business', 70000),
('vehicles', 'business', 75000),
('dealership', 'business', 88000),
('garage', 'business', 85000),
('garages', 'business', 85000),
('repair', 'business', 75000),
('repairs', 'business', 75000),
('service', 'business', 60000),
('servicing', 'business', 75000),
('towing', 'business', 85000),
('bodyshop', 'business', 87000),
('autobody', 'business', 87000),

-- Construction/Trades (80000-89999)
('construction', 'business', 88000),
('contracting', 'business', 88000),
('contractors', 'business', 88000),
('builders', 'business', 85000),
('building', 'business', 60000),
('development', 'business', 75000),
('developers', 'business', 85000),
('roofing', 'business', 87000),
('plumbing', 'business', 87000),
('plumbers', 'business', 87000),
('electric', 'business', 85000),
('electrical', 'business', 87000),
('electricians', 'business', 87000),
('hvac', 'business', 87000),
('heating', 'business', 85000),
('cooling', 'business', 85000),
('landscaping', 'business', 85000),
('paving', 'business', 85000),
('excavation', 'business', 85000),
('renovation', 'business', 83000),
('renovations', 'business', 83000),

-- Real Estate (80000-89999)
('realty', 'business', 88000),
('realtors', 'business', 88000),
('properties', 'business', 75000),
('property', 'business', 60000),
('estates', 'business', 75000),
('leasing', 'business', 80000),
('rentals', 'business', 80000),
('rental', 'business', 75000),

-- Financial (85000-89999)
('bank', 'business', 89000),
('banking', 'business', 89000),
('financial', 'business', 88000),
('finance', 'business', 87000),
('insurance', 'business', 88000),
('assurance', 'business', 87000),
('investments', 'business', 87000),
('securities', 'business', 87000),
('mortgage', 'business', 86000),
('mortgages', 'business', 86000),
('accounting', 'business', 86000),
('accountants', 'business', 86000),
('bookkeeping', 'business', 85000),
('tax', 'business', 75000),
('taxation', 'business', 80000),

-- Professional Services (80000-89999)
('law', 'business', 88000),
('legal', 'business', 88000),
('lawyers', 'business', 88000),
('attorneys', 'business', 88000),
('notary', 'business', 86000),
('engineering', 'business', 87000),
('engineers', 'business', 87000),
('architects', 'business', 87000),
('architecture', 'business', 87000),
('surveying', 'business', 85000),
('surveyors', 'business', 85000),

-- Manufacturing/Industrial (80000-89999)
('manufacturing', 'business', 88000),
('manufacturers', 'business', 88000),
('factory', 'business', 87000),
('factories', 'business', 87000),
('industrial', 'business', 85000),
('industries', 'business', 86000),
('processing', 'business', 80000),
('processors', 'business', 80000),
('production', 'business', 75000),
('products', 'business', 60000),
('equipment', 'business', 85000),
('machinery', 'business', 85000),
('tools', 'business', 60000),

-- Technology (75000-89999)
('tech', 'business', 75000),
('technology', 'business', 80000),
('software', 'business', 85000),
('hardware', 'business', 80000),
('computers', 'business', 75000),
('computing', 'business', 80000),
('networks', 'business', 75000),
('networking', 'business', 80000),
('telecom', 'business', 87000),
('telecommunications', 'business', 88000),
('wireless', 'business', 80000),
('broadband', 'business', 80000),
('internet', 'business', 75000),
('web', 'business', 65000),
('online', 'business', 65000),
('digital', 'business', 70000),
('cyber', 'business', 75000),

-- Education/Training (75000-88000)
('academy', 'business', 87000),
('institute', 'business', 87000),
('college', 'business', 86000),
('university', 'business', 86000),
('school', 'business', 75000),
('schools', 'business', 75000),
('education', 'business', 75000),
('educational', 'business', 75000),
('training', 'business', 75000),
('learning', 'business', 65000),
('tutoring', 'business', 80000),

-- Logistics/Transportation (80000-89999)
('logistics', 'business', 87000),
('transport', 'business', 85000),
('transportation', 'business', 87000),
('trucking', 'business', 87000),
('freight', 'business', 86000),
('shipping', 'business', 85000),
('delivery', 'business', 75000),
('deliveries', 'business', 75000),
('courier', 'business', 85000),
('couriers', 'business', 85000),
('moving', 'business', 80000),
('movers', 'business', 85000),
('storage', 'business', 75000),
('warehouse', 'business', 85000),
('warehousing', 'business', 86000),

-- Entertainment/Media (75000-88000)
('entertainment', 'business', 85000),
('productions', 'business', 85000),
('studios', 'business', 85000),
('broadcasting', 'business', 87000),
('broadcast', 'business', 80000),
('media', 'business', 75000),
('publishing', 'business', 85000),
('publishers', 'business', 85000),
('printing', 'business', 83000),
('printers', 'business', 83000),
('photography', 'business', 75000),
('photographers', 'business', 80000),
('video', 'business', 65000),
('audio', 'business', 65000),
('music', 'business', 60000),
('records', 'business', 60000),
('gaming', 'business', 75000),
('games', 'business', 60000),

-- Agriculture/Farming (70000-85000)
('farm', 'business', 75000),
('farms', 'business', 75000),
('farming', 'business', 75000),
('agriculture', 'business', 83000),
('agricultural', 'business', 83000),
('ranch', 'business', 75000),
('ranching', 'business', 75000),
('dairy', 'business', 75000),
('livestock', 'business', 75000),
('produce', 'business', 60000),
('nursery', 'business', 75000),
('greenhouse', 'business', 75000),

-- General Business Terms (50000-85000)
('center', 'business', 60000),
('centre', 'business', 60000),
('office', 'business', 60000),
('offices', 'business', 65000),
('headquarters', 'business', 85000),
('branch', 'business', 60000),
('division', 'business', 65000),
('department', 'business', 60000),
('bureau', 'business', 75000),
('foundation', 'business', 75000),
('institute', 'business', 85000),
('association', 'business', 75000),
('society', 'business', 65000),
('union', 'business', 65000),
('federation', 'business', 75000),
('chamber', 'business', 75000),
('commission', 'business', 75000),
('committee', 'business', 65000),
('council', 'business', 65000),
('board', 'business', 60000),
('trust', 'business', 75000),
('fund', 'business', 65000),
('exchange', 'business', 75000),
('market', 'business', 60000),
('trade', 'business', 60000),
('commerce', 'business', 75000),
('commercial', 'business', 75000),
('business', 'business', 60000),
('professional', 'business', 60000),
('premier', 'business', 60000),
('premium', 'business', 60000),
('quality', 'business', 50000),
('express', 'business', 60000),
('rapid', 'business', 50000),
('quick', 'business', 50000),
('fast', 'business', 50000),
('speedy', 'business', 50000),
('discount', 'business', 60000),
('bargain', 'business', 60000),
('value', 'business', 50000),
('budget', 'business', 50000),
('economy', 'business', 50000),
('luxury', 'business', 60000),
('elite', 'business', 60000),
('select', 'business', 50000),
('choice', 'business', 50000),
('best', 'business', 50000),
('top', 'business', 50000),
('first', 'business', 10000),
('plus', 'business', 50000),
('pro', 'business', 50000),
('expert', 'business', 60000),
('specialists', 'business', 75000),
('specialist', 'business', 60000),
('professionals', 'business', 60000),
('world', 'business', 50000),
('universal', 'business', 50000),
('general', 'business', 50000),
('total', 'business', 50000),
('complete', 'business', 50000),
('full', 'business', 10000),
('integrated', 'business', 60000),
('unified', 'business', 60000),
('united', 'business', 50000),
('allied', 'business', 60000),
('combined', 'business', 50000),
('joint', 'business', 50000),
('mutual', 'business', 60000),
('cooperative', 'business', 75000),
('collective', 'business', 60000),

-- Business patterns with numbers (75000-79999)
('24 hour', 'business', 78000),
('24hr', 'business', 78000),
('24/7', 'business', 78000),
('7-eleven', 'business', 79000),
('7 eleven', 'business', 79000),
('a&w', 'business', 79000),
('a & w', 'business', 79000),
('h&r block', 'business', 79000),
('h & r block', 'business', 79000),
('3m', 'business', 79000)
ON DUPLICATE KEY UPDATE word_count = GREATEST(word_count, VALUES(word_count));

-- ====================================
-- 5. HIGHWAY/ROAD INDICATORS
-- word_type = 'indeterminate', count = 85000-89999
-- Used for address parsing
-- ====================================
INSERT INTO word_data (word_lower, word_type, word_count) VALUES
('highway', 'indeterminate', 86000),
('hwy', 'indeterminate', 86000),
('route', 'indeterminate', 86000),
('rte', 'indeterminate', 86000),
('interstate', 'indeterminate', 86000),
('autoroute', 'indeterminate', 86000)
ON DUPLICATE KEY UPDATE 
    word_type = CASE 
        WHEN word_type = 'business' AND word_count < 86000 THEN 'indeterminate'
        ELSE word_type 
    END,
    word_count = GREATEST(word_count, 86000);

-- ====================================
-- 6. LOCATION KEYWORDS
-- word_type = 'indeterminate', count = 80000-84999
-- Help identify address components
-- ====================================
INSERT INTO word_data (word_lower, word_type, word_count) VALUES
('downtown', 'indeterminate', 82000),
('uptown', 'indeterminate', 82000),
('midtown', 'indeterminate', 82000),
('suburb', 'indeterminate', 81000),
('plaza', 'indeterminate', 83000),
('complex', 'indeterminate', 82000),
('tower', 'indeterminate', 82000),
('floor', 'indeterminate', 81000),
('suite', 'indeterminate', 81000),
('unit', 'indeterminate', 81000),
('level', 'indeterminate', 81000)
ON DUPLICATE KEY UPDATE 
    word_type = CASE 
        WHEN word_type = 'business' AND word_count < 85000 THEN 'indeterminate'
        ELSE word_type 
    END,
    word_count = GREATEST(word_count, VALUES(word_count));

-- ====================================
-- 7. RESIDENTIAL INDICATORS
-- word_type = 'indeterminate', count = 70000-74999
-- Help identify residential names
-- ====================================
INSERT INTO word_data (word_lower, word_type, word_count) VALUES
('family', 'indeterminate', 73000),
('residence', 'indeterminate', 73000),
('household', 'indeterminate', 73000),
('estate', 'indeterminate', 71000),
('manor', 'indeterminate', 71000),
('cottage', 'indeterminate', 71000),
('villa', 'indeterminate', 71000),
('bungalow', 'indeterminate', 71000),
('apartment', 'indeterminate', 72000),
('condo', 'indeterminate', 72000),
('condominium', 'indeterminate', 72000),
('townhouse', 'indeterminate', 72000),
('duplex', 'indeterminate', 72000),
('home', 'indeterminate', 70000)
ON DUPLICATE KEY UPDATE 
    word_type = CASE 
        WHEN word_type = 'business' AND word_count < 75000 THEN 'indeterminate'
        ELSE word_type 
    END,
    word_count = GREATEST(word_count, VALUES(word_count));

-- Verify the migration
SELECT word_type, 
       COUNT(*) as total_words,
       MIN(word_count) as min_count,
       MAX(word_count) as max_count,
       CASE 
           WHEN word_type = 'business' THEN
               CASE 
                   WHEN MAX(word_count) = 99999 THEN 'Includes corporate suffixes'
                   WHEN MAX(word_count) >= 90000 THEN 'Includes strong business indicators'
                   WHEN MAX(word_count) >= 50000 THEN 'Includes medium business indicators'
                   ELSE 'Standard business words'
               END
           WHEN word_type = 'indeterminate' THEN
               CASE
                   WHEN MAX(word_count) >= 95000 THEN 'Includes titles/honorifics'
                   WHEN MAX(word_count) >= 85000 THEN 'Includes road/highway indicators'
                   WHEN MAX(word_count) >= 80000 THEN 'Includes location keywords'
                   WHEN MAX(word_count) >= 70000 THEN 'Includes residential indicators'
                   ELSE 'Standard indeterminate words'
               END
           ELSE word_type
       END as category_note
FROM word_data 
WHERE word_count >= 10000
GROUP BY word_type
ORDER BY word_type;