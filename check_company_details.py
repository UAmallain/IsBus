import sqlite3

# Connect to the GeoPackage
conn = sqlite3.connect('lrnf000r25p_e.gpkg')
cursor = conn.cursor()

print("Detailed look at 'Company' streets:")
print("=" * 50)

# Get all Company streets with their full details
cursor.execute("""
    SELECT NGD_UID, Name, Type, AFL_VAL, ATL_VAL, AFR_VAL, ATR_VAL
    FROM lrnf000r25p_e 
    WHERE LOWER(Name) = 'company'
""")

print("\nAll 'Company' streets with address ranges:")
for row in cursor.fetchall():
    uid, name, street_type, afl, atl, afr, atr = row
    print(f"  {uid}: {name} {street_type}")
    print(f"    Left side:  {afl or 'N/A'} - {atl or 'N/A'}")
    print(f"    Right side: {afr or 'N/A'} - {atr or 'N/A'}")

conn.close()