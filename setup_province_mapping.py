#!/usr/bin/env python3
"""
Setup script to create and populate the province mapping table in MySQL.
This creates mappings between 2-character province codes and full province names.
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

def create_province_mapping_table(connection):
    """Create the province mapping table and related database objects."""
    
    cursor = connection.cursor()
    
    print("Creating province mapping table...")
    
    # Create the table
    create_table_sql = """
    CREATE TABLE IF NOT EXISTS province_mapping (
        id INT AUTO_INCREMENT PRIMARY KEY,
        province_code CHAR(2) NOT NULL UNIQUE,
        province_name VARCHAR(100) NOT NULL,
        province_name_french VARCHAR(100),
        region VARCHAR(50),
        created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
        INDEX idx_province_code (province_code),
        INDEX idx_province_name (province_name)
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
    """
    cursor.execute(create_table_sql)
    
    print("Inserting province data...")
    
    # Province data
    provinces = [
        ('NL', 'Newfoundland and Labrador', 'Terre-Neuve-et-Labrador', 'Atlantic'),
        ('PE', 'Prince Edward Island', 'Île-du-Prince-Édouard', 'Atlantic'),
        ('NS', 'Nova Scotia', 'Nouvelle-Écosse', 'Atlantic'),
        ('NB', 'New Brunswick', 'Nouveau-Brunswick', 'Atlantic'),
        ('QC', 'Quebec', 'Québec', 'Central'),
        ('ON', 'Ontario', 'Ontario', 'Central'),
        ('MB', 'Manitoba', 'Manitoba', 'Prairie'),
        ('SK', 'Saskatchewan', 'Saskatchewan', 'Prairie'),
        ('AB', 'Alberta', 'Alberta', 'Prairie'),
        ('BC', 'British Columbia', 'Colombie-Britannique', 'Pacific'),
        ('YT', 'Yukon', 'Yukon', 'Northern'),
        ('NT', 'Northwest Territories', 'Territoires du Nord-Ouest', 'Northern'),
        ('NU', 'Nunavut', 'Nunavut', 'Northern')
    ]
    
    # Insert provinces
    insert_sql = """
    INSERT INTO province_mapping (province_code, province_name, province_name_french, region) 
    VALUES (%s, %s, %s, %s)
    ON DUPLICATE KEY UPDATE 
        province_name = VALUES(province_name),
        province_name_french = VALUES(province_name_french),
        region = VALUES(region)
    """
    
    cursor.executemany(insert_sql, provinces)
    
    print(f"✓ Inserted {len(provinces)} provinces")
    
    # Create the view
    print("Creating road_network_with_provinces view...")
    
    create_view_sql = """
    CREATE OR REPLACE VIEW road_network_with_provinces AS
    SELECT 
        rn.*,
        pm_left.province_name AS province_full_name_left,
        pm_right.province_name AS province_full_name_right
    FROM road_network rn
    LEFT JOIN province_mapping pm_left ON rn.province_uid_left = pm_left.province_code
    LEFT JOIN province_mapping pm_right ON rn.province_uid_right = pm_right.province_code
    """
    cursor.execute(create_view_sql)
    
    # Create indexes on road_network if they don't exist
    print("Creating indexes on road_network table...")
    
    indexes = [
        "CREATE INDEX IF NOT EXISTS idx_road_network_name ON road_network (name)",
        "CREATE INDEX IF NOT EXISTS idx_road_network_province_left ON road_network (province_uid_left)",
        "CREATE INDEX IF NOT EXISTS idx_road_network_province_right ON road_network (province_uid_right)",
        "CREATE INDEX IF NOT EXISTS idx_road_network_type ON road_network (type)"
    ]
    
    for index_sql in indexes:
        try:
            cursor.execute(index_sql)
        except Exception as e:
            print(f"  Note: {e}")
    
    # Create stored procedure for street search
    print("Creating SearchStreetNames stored procedure...")
    
    # Drop procedure if exists
    cursor.execute("DROP PROCEDURE IF EXISTS SearchStreetNames")
    
    create_procedure_sql = """
    CREATE PROCEDURE SearchStreetNames(
        IN search_term VARCHAR(100),
        IN province_code CHAR(2),
        IN limit_results INT
    )
    BEGIN
        SET @search_pattern = CONCAT('%', LOWER(search_term), '%');
        
        IF province_code IS NULL OR province_code = '' THEN
            -- Search all provinces
            SELECT DISTINCT 
                name AS street_name,
                type AS street_type,
                COUNT(*) as occurrence_count,
                GROUP_CONCAT(DISTINCT COALESCE(csd_name_left, csd_name_right) SEPARATOR ', ') as communities
            FROM road_network
            WHERE LOWER(name) LIKE @search_pattern
            GROUP BY name, type
            ORDER BY occurrence_count DESC
            LIMIT limit_results;
        ELSE
            -- Search specific province
            SELECT DISTINCT 
                name AS street_name,
                type AS street_type,
                COUNT(*) as occurrence_count,
                GROUP_CONCAT(DISTINCT COALESCE(csd_name_left, csd_name_right) SEPARATOR ', ') as communities
            FROM road_network
            WHERE LOWER(name) LIKE @search_pattern
                AND (province_uid_left = province_code OR province_uid_right = province_code)
            GROUP BY name, type
            ORDER BY occurrence_count DESC
            LIMIT limit_results;
        END IF;
    END
    """
    cursor.execute(create_procedure_sql)
    
    # Create function to get province code from name
    print("Creating GetProvinceCode function...")
    
    cursor.execute("DROP FUNCTION IF EXISTS GetProvinceCode")
    
    create_function_sql = """
    CREATE FUNCTION GetProvinceCode(province_name_input VARCHAR(100))
    RETURNS CHAR(2)
    DETERMINISTIC
    READS SQL DATA
    BEGIN
        DECLARE prov_code CHAR(2);
        
        -- Try exact match first
        SELECT province_code INTO prov_code
        FROM province_mapping
        WHERE LOWER(province_name) = LOWER(province_name_input)
           OR LOWER(province_name_french) = LOWER(province_name_input)
        LIMIT 1;
        
        -- If not found, try partial match
        IF prov_code IS NULL THEN
            SELECT province_code INTO prov_code
            FROM province_mapping
            WHERE LOWER(province_name) LIKE CONCAT('%', LOWER(province_name_input), '%')
               OR LOWER(province_name_french) LIKE CONCAT('%', LOWER(province_name_input), '%')
            LIMIT 1;
        END IF;
        
        RETURN prov_code;
    END
    """
    cursor.execute(create_function_sql)
    
    connection.commit()
    print("✓ All database objects created successfully")

def verify_setup(connection):
    """Verify the province mapping setup."""
    cursor = connection.cursor()
    
    print("\n=== Verification ===")
    
    # Check province count
    cursor.execute("SELECT COUNT(*) FROM province_mapping")
    province_count = cursor.fetchone()[0]
    print(f"✓ Province mappings: {province_count}")
    
    # Show sample provinces
    cursor.execute("SELECT province_code, province_name FROM province_mapping ORDER BY province_name LIMIT 5")
    print("\nSample provinces:")
    for code, name in cursor.fetchall():
        print(f"  {code}: {name}")
    
    # Test the function
    cursor.execute("SELECT GetProvinceCode('Ontario')")
    result = cursor.fetchone()[0]
    print(f"\n✓ Function test: GetProvinceCode('Ontario') = '{result}'")
    
    cursor.execute("SELECT GetProvinceCode('British Columbia')")
    result = cursor.fetchone()[0]
    print(f"✓ Function test: GetProvinceCode('British Columbia') = '{result}'")

def main():
    """Main setup function."""
    print("=" * 50)
    print("Province Mapping Setup Script")
    print("=" * 50)
    print(f"Timestamp: {datetime.now()}")
    print()
    
    try:
        # Connect to MySQL
        print("Connecting to MySQL database...")
        connection = pymysql.connect(**MYSQL_CONFIG)
        print("✓ Connected to MySQL")
        
        # Create province mapping table and related objects
        create_province_mapping_table(connection)
        
        # Verify the setup
        verify_setup(connection)
        
        connection.close()
        
        print("\n" + "=" * 50)
        print("✓ Province mapping setup completed successfully!")
        print("=" * 50)
        print("\nThe street search API now supports province-based filtering.")
        print("You can use either province codes (ON, BC) or names (Ontario, British Columbia).")
        
    except Exception as e:
        print(f"\n✗ Error during setup: {e}")
        sys.exit(1)

if __name__ == "__main__":
    main()
    input("\nPress Enter to exit...")