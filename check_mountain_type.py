import sqlite3

# Connect to the GeoPackage
conn = sqlite3.connect('lrnf000r25p_e.gpkg')
cursor = conn.cursor()

# Check what street types contain "Mountain"
print("Checking for street types containing 'Mountain':")
cursor.execute("""
    SELECT DISTINCT Type 
    FROM lrnf000r25p_e 
    WHERE Type LIKE '%MTN%' OR Type LIKE '%MOUNTAIN%'
    ORDER BY Type
""")
types = cursor.fetchall()
for t in types:
    print(f"  {t[0]}")

# Check if any streets have "MOUNTAIN" as their type
print("\nChecking for streets with 'MOUNTAIN' or 'MTN' as type:")
cursor.execute("""
    SELECT COUNT(*) 
    FROM lrnf000r25p_e 
    WHERE Type = 'MOUNTAIN' OR Type = 'MTN'
""")
count = cursor.fetchone()[0]
print(f"  Count: {count}")

if count > 0:
    cursor.execute("""
        SELECT Name, Type, NGD_UID
        FROM lrnf000r25p_e 
        WHERE Type = 'MOUNTAIN' OR Type = 'MTN'
        LIMIT 5
    """)
    print("  Sample records:")
    for row in cursor.fetchall():
        print(f"    {row}")

conn.close()