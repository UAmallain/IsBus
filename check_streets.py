import sqlite3

# Connect to the GeoPackage
conn = sqlite3.connect('lrnf000r25p_e.gpkg')
cursor = conn.cursor()

# First check what tables exist
cursor.execute("SELECT name FROM sqlite_master WHERE type='table'")
tables = cursor.fetchall()
print("Tables in GeoPackage:", [t[0] for t in tables])

# Check for 'Company' as a street name
print("\nChecking for 'Company' in GeoPackage...")
cursor.execute("SELECT COUNT(*) FROM lrnf000r25p_e WHERE LOWER(Name) = 'company'")
count = cursor.fetchone()[0]
print(f"  Count of streets named 'Company': {count}")

if count > 0:
    # First check column names
    cursor.execute("PRAGMA table_info(lrnf000r25p_e)")
    columns = cursor.fetchall()
    print("  Available columns:", [c[1] for c in columns[:10]])  # Show first 10 columns
    
    cursor.execute("SELECT NGD_UID, Name, Type FROM lrnf000r25p_e WHERE LOWER(Name) = 'company' LIMIT 5")
    print("  Sample records:")
    for row in cursor.fetchall():
        print(f"    {row}")

# Check for 'Mountain' as a street name
print("\nChecking for 'Mountain' in GeoPackage...")
cursor.execute("SELECT COUNT(*) FROM lrnf000r25p_e WHERE LOWER(Name) = 'mountain'")
count = cursor.fetchone()[0]
print(f"  Count of streets named 'Mountain': {count}")

if count > 0:
    cursor.execute("SELECT NGD_UID, Name, Type, CSD_Name_Left, Province_UID_Left FROM lrnf000r25p_e WHERE LOWER(Name) = 'mountain' LIMIT 5")
    print("  Sample records:")
    for row in cursor.fetchall():
        print(f"    {row}")

# Check for 'Company Mountain' as a street name
print("\nChecking for 'Company Mountain' in GeoPackage...")
cursor.execute("SELECT COUNT(*) FROM lrnf000r25p_e WHERE LOWER(Name) = 'company mountain'")
count = cursor.fetchone()[0]
print(f"  Count of streets named 'Company Mountain': {count}")

conn.close()