#!/usr/bin/env python3
"""
Migration script to transfer data from lrnf000r25p_e.gpkg (GeoPackage) to MySQL bor_db database.
This script extracts road network data including street names, types, and community information.
"""

import sqlite3
import pymysql
import json
from datetime import datetime

# Database configurations
GPKG_FILE = 'lrnf000r25p_e.gpkg'
MYSQL_CONFIG = {
    'host': 'localhost',
    'user': 'root',
    'password': 'D0ntfw!thm01MA',
    'database': 'bor_db',
    'charset': 'utf8mb4'
}

def get_gpkg_data():
    """Extract data from the GeoPackage file."""
    print(f"Connecting to GeoPackage: {GPKG_FILE}")
    conn = sqlite3.connect(GPKG_FILE)
    cursor = conn.cursor()
    
    # Get total count
    cursor.execute("SELECT COUNT(*) FROM lrnf000r25p_e")
    total_records = cursor.fetchone()[0]
    print(f"Found {total_records} records in GeoPackage")
    
    # Extract all relevant data (excluding geometry for now)
    query = """
    SELECT 
        NGD_UID,
        NAME,
        TYPE,
        DIR,
        AFL_VAL,
        ATL_VAL,
        AFR_VAL,
        ATR_VAL,
        CSDUID_L,
        CSDNAME_L,
        CSDTYPE_L,
        CSDUID_R,
        CSDNAME_R,
        CSDTYPE_R,
        PRUID_L,
        PRNAME_L,
        PRUID_R,
        PRNAME_R,
        CLASS,
        RANK
    FROM lrnf000r25p_e
    """
    
    cursor.execute(query)
    data = cursor.fetchall()
    
    conn.close()
    return data

def create_mysql_tables(mysql_conn):
    """Create necessary tables in MySQL if they don't exist."""
    cursor = mysql_conn.cursor()
    
    # Create road_network table
    create_road_network = """
    CREATE TABLE IF NOT EXISTS road_network (
        id INT AUTO_INCREMENT PRIMARY KEY,
        ngd_uid VARCHAR(10) UNIQUE,
        name VARCHAR(100),
        type VARCHAR(10),
        direction VARCHAR(2),
        address_from_left VARCHAR(20),
        address_to_left VARCHAR(20),
        address_from_right VARCHAR(20),
        address_to_right VARCHAR(20),
        csd_uid_left VARCHAR(10),
        csd_name_left VARCHAR(100),
        csd_type_left VARCHAR(5),
        csd_uid_right VARCHAR(10),
        csd_name_right VARCHAR(100),
        csd_type_right VARCHAR(5),
        province_uid_left VARCHAR(2),
        province_name_left VARCHAR(100),
        province_uid_right VARCHAR(2),
        province_name_right VARCHAR(100),
        road_class VARCHAR(2),
        road_rank VARCHAR(1),
        created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
        INDEX idx_name (name),
        INDEX idx_type (type),
        INDEX idx_csd_left (csd_name_left),
        INDEX idx_csd_right (csd_name_right)
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
    """
    
    cursor.execute(create_road_network)
    
    # Create street_types extraction table if needed
    create_street_types = """
    CREATE TABLE IF NOT EXISTS extracted_street_types (
        id INT AUTO_INCREMENT PRIMARY KEY,
        type_code VARCHAR(10) UNIQUE,
        type_name VARCHAR(50),
        occurrences INT DEFAULT 0,
        created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
        INDEX idx_type_code (type_code)
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
    """
    
    cursor.execute(create_street_types)
    
    # Create unique street names table
    create_unique_streets = """
    CREATE TABLE IF NOT EXISTS unique_street_names (
        id INT AUTO_INCREMENT PRIMARY KEY,
        street_name VARCHAR(100) UNIQUE,
        occurrences INT DEFAULT 0,
        first_seen_community VARCHAR(100),
        created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
        INDEX idx_street_name (street_name)
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
    """
    
    cursor.execute(create_unique_streets)
    
    mysql_conn.commit()
    print("MySQL tables created/verified")

def migrate_data(data, mysql_conn):
    """Migrate data from GeoPackage to MySQL."""
    cursor = mysql_conn.cursor()
    
    # Prepare insert query for road_network
    insert_query = """
    INSERT INTO road_network (
        ngd_uid, name, type, direction,
        address_from_left, address_to_left, address_from_right, address_to_right,
        csd_uid_left, csd_name_left, csd_type_left,
        csd_uid_right, csd_name_right, csd_type_right,
        province_uid_left, province_name_left,
        province_uid_right, province_name_right,
        road_class, road_rank
    ) VALUES (
        %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s
    )
    ON DUPLICATE KEY UPDATE
        name = VALUES(name),
        type = VALUES(type),
        direction = VALUES(direction)
    """
    
    # Track unique values
    street_types = {}
    street_names = {}
    
    # Insert data in batches
    batch_size = 1000
    total_inserted = 0
    
    for i in range(0, len(data), batch_size):
        batch = data[i:i+batch_size]
        
        # Process batch
        for row in batch:
            # Track street types and names
            if row[2]:  # TYPE
                street_types[row[2]] = street_types.get(row[2], 0) + 1
            if row[1]:  # NAME
                if row[1] not in street_names:
                    street_names[row[1]] = {
                        'count': 1,
                        'first_community': row[10] or row[13]  # CSDNAME_L or CSDNAME_R
                    }
                else:
                    street_names[row[1]]['count'] += 1
        
        # Insert batch into database
        try:
            cursor.executemany(insert_query, batch)
            mysql_conn.commit()
            total_inserted += len(batch)
            print(f"Inserted {total_inserted}/{len(data)} records...")
        except Exception as e:
            print(f"Error inserting batch: {e}")
            mysql_conn.rollback()
    
    # Insert unique street types
    print("\nInserting unique street types...")
    for type_code, count in street_types.items():
        cursor.execute("""
            INSERT INTO extracted_street_types (type_code, occurrences)
            VALUES (%s, %s)
            ON DUPLICATE KEY UPDATE occurrences = VALUES(occurrences)
        """, (type_code, count))
    
    # Insert unique street names
    print("Inserting unique street names...")
    for name, info in street_names.items():
        cursor.execute("""
            INSERT INTO unique_street_names (street_name, occurrences, first_seen_community)
            VALUES (%s, %s, %s)
            ON DUPLICATE KEY UPDATE occurrences = VALUES(occurrences)
        """, (name, info['count'], info['first_community']))
    
    mysql_conn.commit()
    
    print(f"\nMigration complete!")
    print(f"Total records migrated: {total_inserted}")
    print(f"Unique street types: {len(street_types)}")
    print(f"Unique street names: {len(street_names)}")

def verify_migration(mysql_conn):
    """Verify the migration was successful."""
    cursor = mysql_conn.cursor()
    
    # Check counts
    cursor.execute("SELECT COUNT(*) FROM road_network")
    road_count = cursor.fetchone()[0]
    
    cursor.execute("SELECT COUNT(*) FROM extracted_street_types")
    type_count = cursor.fetchone()[0]
    
    cursor.execute("SELECT COUNT(*) FROM unique_street_names")
    name_count = cursor.fetchone()[0]
    
    print("\n=== Migration Verification ===")
    print(f"Road network records: {road_count}")
    print(f"Street types: {type_count}")
    print(f"Unique street names: {name_count}")
    
    # Show sample data
    print("\nSample road network data:")
    cursor.execute("SELECT ngd_uid, name, type, csd_name_left FROM road_network LIMIT 5")
    for row in cursor.fetchall():
        print(f"  {row}")
    
    print("\nTop 10 street types by occurrence:")
    cursor.execute("SELECT type_code, occurrences FROM extracted_street_types ORDER BY occurrences DESC LIMIT 10")
    for row in cursor.fetchall():
        print(f"  {row[0]}: {row[1]} occurrences")

def main():
    """Main migration function."""
    print("Starting GeoPackage to MySQL migration...")
    print(f"Timestamp: {datetime.now()}")
    
    try:
        # Extract data from GeoPackage
        gpkg_data = get_gpkg_data()
        
        # Connect to MySQL
        print("\nConnecting to MySQL database...")
        mysql_conn = pymysql.connect(**MYSQL_CONFIG)
        
        # Create tables
        create_mysql_tables(mysql_conn)
        
        # Migrate data
        print("\nStarting data migration...")
        migrate_data(gpkg_data, mysql_conn)
        
        # Verify migration
        verify_migration(mysql_conn)
        
        mysql_conn.close()
        print("\nMigration completed successfully!")
        
    except Exception as e:
        print(f"\nError during migration: {e}")
        raise

if __name__ == "__main__":
    main()