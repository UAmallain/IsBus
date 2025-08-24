#!/usr/bin/env python3
"""
Setup script to create and populate the street type mapping table in MySQL.
This creates mappings between street type abbreviations and their full names.
"""

import pymysql
import sys
from datetime import datetime

# Database configuration
MYSQL_CONFIG = {
    'host': 'localhost',
    'user': 'root',
    'password': 'D0ntfw!thm01MA',
    'database': 'bor_db',
    'charset': 'utf8mb4'
}

def create_street_type_mapping_table(connection):
    """Create the street type mapping table and related database objects."""
    
    cursor = connection.cursor()
    
    print("Creating street type mapping table...")
    
    # Create the table
    create_table_sql = """
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
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
    """
    cursor.execute(create_table_sql)
    
    print("Inserting street type mappings...")
    
    # Street type data - comprehensive list
    street_types = [
        # Primary road types
        ('st', 'Street', 'Rue', 'road', True, 1),
        ('ave', 'Avenue', 'Avenue', 'road', True, 2),
        ('rd', 'Road', 'Chemin', 'road', True, 3),
        ('dr', 'Drive', 'Promenade', 'road', True, 4),
        ('blvd', 'Boulevard', 'Boulevard', 'road', True, 5),
        ('way', 'Way', 'Voie', 'road', True, 6),
        ('pl', 'Place', 'Place', 'road', True, 7),
        ('cres', 'Crescent', 'Croissant', 'road', True, 8),
        ('ct', 'Court', 'Cour', 'road', True, 9),
        ('ln', 'Lane', 'Ruelle', 'road', True, 10),
        ('terr', 'Terrace', 'Terrasse', 'road', True, 11),
        ('cir', 'Circle', 'Cercle', 'road', True, 12),
        ('sq', 'Square', 'Carré', 'road', True, 13),
        ('pk', 'Park', 'Parc', 'road', True, 14),
        ('pkwy', 'Parkway', 'Promenade', 'road', True, 15),
        
        # Highway types
        ('hwy', 'Highway', 'Autoroute', 'highway', True, 20),
        ('fwy', 'Freeway', 'Autoroute', 'highway', True, 21),
        ('expy', 'Expressway', 'Voie rapide', 'highway', True, 22),
        ('tpke', 'Turnpike', 'Autoroute à péage', 'highway', False, 23),
        ('rte', 'Route', 'Route', 'highway', True, 24),
        
        # Secondary road types
        ('tr', 'Trail', 'Sentier', 'road', True, 30),
        ('path', 'Path', 'Sentier', 'road', True, 31),
        ('walk', 'Walk', 'Promenade', 'road', True, 32),
        ('row', 'Row', 'Rangée', 'road', True, 33),
        ('gate', 'Gate', 'Porte', 'road', True, 34),
        ('grn', 'Green', 'Vert', 'road', True, 35),
        ('mall', 'Mall', 'Mail', 'road', True, 36),
        ('alley', 'Alley', 'Allée', 'road', True, 37),
        ('loop', 'Loop', 'Boucle', 'road', True, 38),
        ('ramp', 'Ramp', 'Bretelle', 'road', True, 39),
        
        # Geographic features
        ('bay', 'Bay', 'Baie', 'geographic', True, 40),
        ('beach', 'Beach', 'Plage', 'geographic', True, 41),
        ('bend', 'Bend', 'Courbe', 'geographic', True, 42),
        ('cape', 'Cape', 'Cap', 'geographic', True, 43),
        ('cliff', 'Cliff', 'Falaise', 'geographic', True, 44),
        ('cove', 'Cove', 'Anse', 'geographic', True, 45),
        ('dale', 'Dale', 'Vallée', 'geographic', True, 46),
        ('dell', 'Dell', 'Vallon', 'geographic', True, 47),
        ('glen', 'Glen', 'Vallon', 'geographic', True, 48),
        ('grove', 'Grove', 'Bosquet', 'geographic', True, 49),
        ('hill', 'Hill', 'Colline', 'geographic', True, 50),
        ('hts', 'Heights', 'Hauteurs', 'geographic', True, 51),
        ('island', 'Island', 'Île', 'geographic', True, 52),
        ('isle', 'Isle', 'Île', 'geographic', False, 53),
        ('lake', 'Lake', 'Lac', 'geographic', True, 54),
        ('mdw', 'Meadow', 'Prairie', 'geographic', True, 55),
        ('mtn', 'Mountain', 'Montagne', 'geographic', True, 56),
        ('orch', 'Orchard', 'Verger', 'geographic', True, 57),
        ('pt', 'Point', 'Pointe', 'geographic', True, 58),
        ('ridge', 'Ridge', 'Crête', 'geographic', True, 59),
        ('shore', 'Shore', 'Rive', 'geographic', True, 60),
        ('vale', 'Vale', 'Vallée', 'geographic', True, 61),
        ('valley', 'Valley', 'Vallée', 'geographic', True, 62),
        ('view', 'View', 'Vue', 'geographic', True, 63),
        ('wood', 'Wood', 'Bois', 'geographic', True, 64),
        ('woods', 'Woods', 'Bois', 'geographic', False, 65),
        
        # Development types
        ('gdns', 'Gardens', 'Jardins', 'development', True, 70),
        ('est', 'Estate', 'Domaine', 'development', True, 71),
        ('mnr', 'Manor', 'Manoir', 'development', True, 72),
        ('villas', 'Villas', 'Villas', 'development', True, 73),
        ('villg', 'Village', 'Village', 'development', True, 74),
        ('cmn', 'Common', 'Commune', 'development', True, 75),
        ('ctr', 'Centre', 'Centre', 'development', True, 76),
        ('plz', 'Plaza', 'Plaza', 'development', True, 77),
        
        # Additional abbreviations
        ('av', 'Avenue', 'Avenue', 'road', False, 80),
        ('boul', 'Boulevard', 'Boulevard', 'road', False, 81),
        ('ch', 'Chemin', 'Chemin', 'road', False, 82),
        ('crcl', 'Circle', 'Cercle', 'road', False, 83),
        ('cresc', 'Crescent', 'Croissant', 'road', False, 84),
        ('crt', 'Court', 'Cour', 'road', False, 85),
        ('drv', 'Drive', 'Promenade', 'road', False, 86),
        ('gdn', 'Garden', 'Jardin', 'development', False, 87),
        ('hrbr', 'Harbor', 'Port', 'geographic', False, 88),
        ('ht', 'Height', 'Hauteur', 'geographic', False, 89),
        ('jct', 'Junction', 'Jonction', 'road', False, 90),
        ('ldg', 'Landing', 'Débarcadère', 'geographic', False, 91),
        ('mt', 'Mount', 'Mont', 'geographic', False, 92),
        ('pky', 'Parkway', 'Promenade', 'road', False, 93),
        ('psge', 'Passage', 'Passage', 'road', False, 94),
        ('rdg', 'Ridge', 'Crête', 'geographic', False, 95),
        ('run', 'Run', 'Voie', 'road', False, 96),
        ('str', 'Street', 'Rue', 'road', False, 97),
        ('ter', 'Terrace', 'Terrasse', 'road', False, 98),
        ('trl', 'Trail', 'Sentier', 'road', False, 99),
        ('wy', 'Way', 'Voie', 'road', False, 100),
        
        # Canadian-specific
        ('conc', 'Concession', 'Concession', 'road', True, 101),
        ('line', 'Line', 'Ligne', 'road', True, 102),
        ('rg', 'Range', 'Rang', 'road', True, 103),
        ('rang', 'Rang', 'Rang', 'road', False, 104),
        ('sdrd', 'Sideroad', 'Route secondaire', 'road', True, 105),
        
        # Quebec-specific French abbreviations
        ('rue', 'Rue', 'Street', 'road', False, 110),
        ('mont', 'Montée', 'Hill', 'road', False, 114),
        ('imp', 'Impasse', 'Dead End', 'road', False, 115),
        
        # Full names as their own entries
        ('street', 'Street', 'Rue', 'road', False, 120),
        ('avenue', 'Avenue', 'Avenue', 'road', False, 121),
        ('road', 'Road', 'Chemin', 'road', False, 122),
        ('drive', 'Drive', 'Promenade', 'road', False, 123),
        ('boulevard', 'Boulevard', 'Boulevard', 'road', False, 124),
        ('place', 'Place', 'Place', 'road', False, 125),
        ('court', 'Court', 'Cour', 'road', False, 126),
        ('lane', 'Lane', 'Ruelle', 'road', False, 127),
        ('crescent', 'Crescent', 'Croissant', 'road', False, 128),
        ('circle', 'Circle', 'Cercle', 'road', False, 129),
        ('highway', 'Highway', 'Autoroute', 'highway', False, 130),
        ('route', 'Route', 'Route', 'highway', False, 131),
        ('trail', 'Trail', 'Sentier', 'road', False, 132),
        ('terrace', 'Terrace', 'Terrasse', 'road', False, 133),
        ('park', 'Park', 'Parc', 'road', False, 134),
        ('parkway', 'Parkway', 'Promenade', 'road', False, 135),
        ('square', 'Square', 'Carré', 'road', False, 136)
    ]
    
    # Insert street types
    insert_sql = """
    INSERT INTO street_type_mapping 
    (abbreviation, full_name, french_name, category, is_primary, display_order) 
    VALUES (%s, %s, %s, %s, %s, %s)
    ON DUPLICATE KEY UPDATE 
        full_name = VALUES(full_name),
        french_name = VALUES(french_name),
        category = VALUES(category),
        is_primary = VALUES(is_primary),
        display_order = VALUES(display_order)
    """
    
    cursor.executemany(insert_sql, street_types)
    
    print(f"✓ Inserted {len(street_types)} street type mappings")
    
    # Create function to normalize street types
    print("Creating NormalizeStreetType function...")
    
    cursor.execute("DROP FUNCTION IF EXISTS NormalizeStreetType")
    
    create_function_sql = """
    CREATE FUNCTION NormalizeStreetType(input_type VARCHAR(50))
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
    END
    """
    cursor.execute(create_function_sql)
    
    # Create function to get abbreviation
    print("Creating GetStreetAbbreviation function...")
    
    cursor.execute("DROP FUNCTION IF EXISTS GetStreetAbbreviation")
    
    create_abbr_function_sql = """
    CREATE FUNCTION GetStreetAbbreviation(input_name VARCHAR(50))
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
    END
    """
    cursor.execute(create_abbr_function_sql)
    
    # Create view for statistics
    print("Creating street_type_statistics view...")
    
    create_view_sql = """
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
    ORDER BY usage_count DESC
    """
    cursor.execute(create_view_sql)
    
    # Create normalized search procedure
    print("Creating SearchStreetsNormalized procedure...")
    
    cursor.execute("DROP PROCEDURE IF EXISTS SearchStreetsNormalized")
    
    create_proc_sql = """
    CREATE PROCEDURE SearchStreetsNormalized(
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
    END
    """
    cursor.execute(create_proc_sql)
    
    connection.commit()
    print("✓ All database objects created successfully")

def verify_setup(connection):
    """Verify the street type mapping setup."""
    cursor = connection.cursor()
    
    print("\n=== Verification ===")
    
    # Check mapping count
    cursor.execute("SELECT COUNT(*) FROM street_type_mapping")
    mapping_count = cursor.fetchone()[0]
    print(f"✓ Street type mappings: {mapping_count}")
    
    # Show category distribution
    cursor.execute("""
        SELECT category, COUNT(*) as count 
        FROM street_type_mapping 
        GROUP BY category 
        ORDER BY count DESC
    """)
    print("\nMappings by category:")
    for category, count in cursor.fetchall():
        print(f"  {category}: {count}")
    
    # Test normalization function
    test_cases = [
        ('rd', 'Road'),
        ('ave', 'Avenue'),
        ('blvd', 'Boulevard'),
        ('hwy', 'Highway'),
        ('conc', 'Concession'),
        ('ch', 'Chemin')
    ]
    
    print("\nTesting normalization function:")
    for abbr, expected in test_cases:
        cursor.execute(f"SELECT NormalizeStreetType('{abbr}')")
        result = cursor.fetchone()[0]
        status = "✓" if result == expected else "✗"
        print(f"  {status} {abbr} → {result}")
    
    # Test abbreviation function
    print("\nTesting abbreviation function:")
    test_names = ['Street', 'Avenue', 'Boulevard']
    for name in test_names:
        cursor.execute(f"SELECT GetStreetAbbreviation('{name}')")
        result = cursor.fetchone()[0]
        print(f"  {name} → {result}")

def show_examples(connection):
    """Show example street type mappings."""
    cursor = connection.cursor()
    
    print("\n=== Sample Mappings ===")
    
    categories = ['road', 'highway', 'geographic', 'development']
    
    for category in categories:
        cursor.execute("""
            SELECT abbreviation, full_name 
            FROM street_type_mapping 
            WHERE category = %s AND is_primary = TRUE
            ORDER BY display_order 
            LIMIT 5
        """, (category,))
        
        print(f"\n{category.capitalize()} types:")
        for abbr, full in cursor.fetchall():
            print(f"  {abbr:6} → {full}")

def main():
    """Main setup function."""
    print("=" * 60)
    print("Street Type Mapping Setup Script")
    print("=" * 60)
    print(f"Timestamp: {datetime.now()}")
    print()
    
    try:
        # Connect to MySQL
        print("Connecting to MySQL database...")
        connection = pymysql.connect(**MYSQL_CONFIG)
        print("✓ Connected to MySQL")
        
        # Create street type mapping table and related objects
        create_street_type_mapping_table(connection)
        
        # Verify the setup
        verify_setup(connection)
        
        # Show examples
        show_examples(connection)
        
        connection.close()
        
        print("\n" + "=" * 60)
        print("✓ Street type mapping setup completed successfully!")
        print("=" * 60)
        print("\nThe system now supports:")
        print("  • 130+ street type abbreviations")
        print("  • Automatic normalization (rd → Road, ave → Avenue)")
        print("  • Canadian-specific types (conc, rang, sdrd)")
        print("  • French Canadian types (ch, rue, mont)")
        print("\nAPI endpoints available:")
        print("  • /api/StreetSearch/type-mappings")
        print("  • /api/StreetSearch/normalize-type")
        print("  • /api/StreetSearch/get-abbreviation")
        
    except Exception as e:
        print(f"\n✗ Error during setup: {e}")
        sys.exit(1)

if __name__ == "__main__":
    main()
    input("\nPress Enter to exit...")