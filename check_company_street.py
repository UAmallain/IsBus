import pymysql

connection = pymysql.connect(
    host='localhost',
    user='root',
    password='D0ntfw!thm01MA',
    database='bor_db',
    charset='utf8mb4'
)

cursor = connection.cursor()

# Check for 'Company' as a street name
print("Checking for 'Company' in road_network table...")
cursor.execute("SELECT COUNT(*) FROM road_network WHERE LOWER(name) = 'company'")
count = cursor.fetchone()[0]
print(f"  Count of streets named 'Company': {count}")

if count > 0:
    cursor.execute("SELECT ngd_uid, name, type, csd_name_left, csd_name_right, province_uid_left FROM road_network WHERE LOWER(name) = 'company' LIMIT 5")
    print("  Sample records:")
    for row in cursor.fetchall():
        print(f"    {row}")

# Check for 'Mountain' as a street name
print("\nChecking for 'Mountain' in road_network table...")
cursor.execute("SELECT COUNT(*) FROM road_network WHERE LOWER(name) = 'mountain'")
count = cursor.fetchone()[0]
print(f"  Count of streets named 'Mountain': {count}")

if count > 0:
    cursor.execute("SELECT ngd_uid, name, type, csd_name_left, csd_name_right, province_uid_left FROM road_network WHERE LOWER(name) = 'mountain' LIMIT 5")
    print("  Sample records:")
    for row in cursor.fetchall():
        print(f"    {row}")

# Check what the parser is finding
print("\nChecking what streets contain 'Mountain Road'...")
cursor.execute("SELECT COUNT(*) FROM road_network WHERE LOWER(name) LIKE '%mountain%' AND type = 'Road'")
count = cursor.fetchone()[0]
print(f"  Count of streets with 'Mountain' and type 'Road': {count}")

cursor.execute("SELECT name, type, csd_name_left, province_uid_left FROM road_network WHERE LOWER(name) LIKE '%mountain%' AND type = 'Road' LIMIT 10")
print("  Sample records:")
for row in cursor.fetchall():
    print(f"    {row[0]} ({row[1]}) - {row[2]}, {row[3]}")

connection.close()