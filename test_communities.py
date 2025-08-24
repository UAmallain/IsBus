import pymysql

connection = pymysql.connect(
    host='localhost',
    user='root',
    password='D0ntfw!thm01MA',
    database='bor_db',
    charset='utf8mb4'
)

cursor = connection.cursor()

# Check if "and" exists as a community
print("Checking for 'and' as a community name...")
cursor.execute("SELECT * FROM communities WHERE LOWER(name) = 'and'")
results = cursor.fetchall()
if results:
    print(f"  Found {len(results)} communities named 'and':")
    for row in results:
        print(f"    {row}")
else:
    print("  No community named 'and' found")

# Check if "Moncton" exists as a community
print("\nChecking for 'Moncton' as a community name...")
cursor.execute("SELECT * FROM communities WHERE LOWER(name) = 'moncton'")
results = cursor.fetchall()
if results:
    print(f"  Found {len(results)} communities named 'Moncton':")
    for row in results:
        print(f"    {row}")
else:
    print("  No community named 'Moncton' found")

# Check what communities exist that contain "and"
print("\nChecking for communities containing 'and'...")
cursor.execute("SELECT name, province FROM communities WHERE LOWER(name) LIKE '%and%' LIMIT 10")
results = cursor.fetchall()
if results:
    print(f"  Found communities containing 'and':")
    for row in results:
        print(f"    {row[0]}, {row[1]}")

connection.close()