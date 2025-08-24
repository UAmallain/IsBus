#!/usr/bin/env python3
"""
Import and clean first/last names from CSV files
- Removes titles (Mr., Mrs., Dr., etc.)
- Splits hyphenated names
- Removes business words
- Handles duplicates by incrementing counts
"""

import mysql.connector
import re
import sys
import argparse
from pathlib import Path

class NameImporter:
    def __init__(self, host='localhost', database='bor_db', user='root', password=''):
        self.host = host
        self.database = database
        self.user = user
        self.password = password
        self.connection = None
        self.cursor = None
        
        # Titles to remove
        self.titles = {
            'mr', 'mrs', 'ms', 'miss', 'dr', 'prof', 'professor', 
            'rev', 'reverend', 'fr', 'father', 'sr', 'jr', 'sir',
            'lord', 'lady', 'master', 'mister', 'madam', 'dame'
        }
        
        # Suffixes to remove
        self.suffixes = {
            'jr', 'sr', 'ii', 'iii', 'iv', 'esq', 'phd', 'md', 
            'dds', 'dvm', 'jd', 'cpa', 'mba', 'ma', 'ba', 'bs'
        }
        
        # Business words that shouldn't be names
        self.business_words = {
            'ltd', 'inc', 'corp', 'llc', 'company', 'co', 'corporation',
            'limited', 'incorporated', 'enterprises', 'group', 'holdings',
            'international', 'global', 'services', 'solutions', 'consulting',
            'management', 'partners', 'associates', 'industries', 'systems',
            'technologies', 'software', 'development', 'marketing', 'sales',
            'office', 'center', 'centre', 'clinic', 'hospital', 'store',
            'shop', 'restaurant', 'cafe', 'hotel', 'motel', 'inn',
            'mgr', 'manager', 'supervisor', 'director', 'president',
            'ceo', 'cfo', 'cto', 'vp', 'trainer', 'admin', 'administrator',
            'call', 'fax', 'phone', 'tel', 'mobile', 'cell',
            'canada', 'health', 'school', 'community', 'foundation'
        }
        
        # Common name particles to preserve
        self.name_particles = {'de', 'del', 'della', 'di', 'da', 'van', 'von', 'der', 'den', 'ten', 'ter', 'la', 'le', 'mac', 'mc', 'o'}
        
    def connect(self):
        """Connect to database"""
        try:
            self.connection = mysql.connector.connect(
                host=self.host,
                database=self.database,
                user=self.user,
                password=self.password,
                autocommit=False
            )
            self.cursor = self.connection.cursor()
            print(f"Connected to database: {self.database}")
            return True
        except Exception as e:
            print(f"Error connecting to database: {e}")
            return False
    
    def disconnect(self):
        """Close database connection"""
        if self.cursor:
            self.cursor.close()
        if self.connection and self.connection.is_connected():
            self.connection.close()
            print("Database connection closed")
    
    def clean_name(self, name):
        """
        Clean a name by removing titles, suffixes, and non-name parts
        Returns a list of clean name parts
        """
        if not name:
            return []
        
        # Convert to lowercase for processing
        name_lower = name.lower().strip()
        
        # Remove parenthetical content (like (MGR))
        name_lower = re.sub(r'\([^)]*\)', '', name_lower).strip()
        
        # Remove dots after titles (mr. -> mr)
        name_lower = re.sub(r'\.\s*', ' ', name_lower).strip()
        
        # Split into parts
        parts = name_lower.split()
        
        # Filter out titles, suffixes, and business words
        clean_parts = []
        for part in parts:
            part_clean = part.strip().strip(',').strip('.')
            
            # Skip if empty
            if not part_clean:
                continue
                
            # Skip if it's a title
            if part_clean in self.titles:
                continue
                
            # Skip if it's a suffix
            if part_clean in self.suffixes:
                continue
                
            # Skip if it's a business word
            if part_clean in self.business_words:
                continue
                
            # Skip if it's just numbers or special characters
            if not re.match(r'^[a-z\-\']+$', part_clean):
                continue
                
            # Skip single letters unless they might be initials
            if len(part_clean) == 1 and part_clean not in ['a', 'i']:
                continue
                
            clean_parts.append(part_clean)
        
        # Handle hyphenated names - split them
        final_parts = []
        for part in clean_parts:
            if '-' in part and len(part) > 2:
                # Split hyphenated names
                sub_parts = part.split('-')
                for sub_part in sub_parts:
                    if sub_part and len(sub_part) > 1:
                        final_parts.append(sub_part)
            else:
                final_parts.append(part)
        
        return final_parts
    
    def is_likely_business(self, name):
        """Check if a name is likely a business rather than a person name"""
        name_lower = name.lower()
        
        # Check for obvious business indicators
        for biz_word in self.business_words:
            if biz_word in name_lower:
                return True
        
        # Check for patterns like numbers, excessive length
        if re.search(r'\d{3,}', name):  # 3+ digits
            return True
            
        # Very long "names" are likely businesses
        if len(name) > 50:
            return True
            
        return False
    
    def import_names_from_file(self, filepath, name_type='first'):
        """
        Import names from CSV file
        name_type: 'first' or 'last'
        """
        if not self.connect():
            return False
        
        try:
            # Read file
            with open(filepath, 'r', encoding='utf-8', errors='ignore') as f:
                lines = f.readlines()
            
            # Skip header if present
            if lines and 'fname' in lines[0].lower() or 'lname' in lines[0].lower() or 'count' in lines[0].lower():
                lines = lines[1:]
            
            # Skip separator lines
            lines = [l for l in lines if not l.strip().startswith('-')]
            
            # Process each line
            names_to_insert = {}
            skipped_business = []
            skipped_invalid = []
            
            for line in lines:
                line = line.strip()
                if not line:
                    continue
                
                # Parse line - expecting format: "NAME    COUNT" (space or tab separated)
                parts = re.split(r'\s{2,}|\t+', line)
                if len(parts) >= 2:
                    raw_name = parts[0].strip()
                    try:
                        count = int(parts[-1])
                    except:
                        count = 1
                else:
                    raw_name = line.strip()
                    count = 1
                
                # Skip if likely business
                if self.is_likely_business(raw_name):
                    skipped_business.append(raw_name)
                    continue
                
                # Clean the name
                clean_names = self.clean_name(raw_name)
                
                if not clean_names:
                    skipped_invalid.append(raw_name)
                    continue
                
                # Add each clean name part
                for clean_name in clean_names:
                    if clean_name in names_to_insert:
                        names_to_insert[clean_name] += count
                    else:
                        names_to_insert[clean_name] = count
            
            # Insert into database
            if names_to_insert:
                success_count = self.insert_names_batch(names_to_insert, name_type)
                
                print(f"\nImport Summary:")
                print(f"  Successfully processed: {success_count} unique names")
                print(f"  Skipped (business): {len(skipped_business)}")
                print(f"  Skipped (invalid): {len(skipped_invalid)}")
                
                if skipped_business and len(skipped_business) <= 20:
                    print(f"\nSkipped business terms: {', '.join(skipped_business[:20])}")
                
                return True
            else:
                print("No valid names found to import")
                return False
                
        except Exception as e:
            print(f"Error during import: {e}")
            return False
        finally:
            self.disconnect()
    
    def insert_names_batch(self, names_dict, name_type):
        """Insert names in batch with duplicate handling"""
        try:
            query = """
                INSERT INTO names (name_lower, name_type, name_count, last_seen)
                VALUES (%s, %s, %s, NOW())
                ON DUPLICATE KEY UPDATE
                    name_count = name_count + VALUES(name_count),
                    last_seen = NOW(),
                    updated_at = CURRENT_TIMESTAMP
            """
            
            data = [(name, name_type, count) for name, count in names_dict.items()]
            
            self.cursor.executemany(query, data)
            self.connection.commit()
            
            print(f"Inserted/updated {len(data)} {name_type} names")
            return len(data)
            
        except Exception as e:
            print(f"Error inserting batch: {e}")
            self.connection.rollback()
            return 0
    
    def show_statistics(self):
        """Show statistics after import"""
        if not self.connect():
            return
        
        try:
            # Total names by type
            self.cursor.execute("""
                SELECT name_type, COUNT(*) as count, SUM(name_count) as total
                FROM names
                GROUP BY name_type
            """)
            
            print("\nDatabase Statistics:")
            print("-" * 40)
            for row in self.cursor.fetchall():
                print(f"{row[0].capitalize()} names: {row[1]:,} unique, {row[2]:,} total occurrences")
            
            # Top 10 most common first names
            self.cursor.execute("""
                SELECT name_lower, name_count
                FROM names
                WHERE name_type = 'first'
                ORDER BY name_count DESC
                LIMIT 10
            """)
            
            print("\nTop 10 First Names:")
            for row in self.cursor.fetchall():
                print(f"  {row[0]}: {row[1]:,}")
                
        except Exception as e:
            print(f"Error getting statistics: {e}")
        finally:
            self.disconnect()

def main():
    parser = argparse.ArgumentParser(description='Import names from CSV file')
    parser.add_argument('file', help='Path to CSV file')
    parser.add_argument('--type', choices=['first', 'last'], required=True,
                       help='Type of names in file')
    parser.add_argument('--host', default='localhost', help='Database host')
    parser.add_argument('--database', default='bor_db', help='Database name')
    parser.add_argument('--user', default='root', help='Database user')
    parser.add_argument('--password', default='', help='Database password')
    parser.add_argument('--stats', action='store_true', help='Show statistics after import')
    
    args = parser.parse_args()
    
    importer = NameImporter(
        host=args.host,
        database=args.database,
        user=args.user,
        password=args.password
    )
    
    print(f"Importing {args.type} names from: {args.file}")
    
    if importer.import_names_from_file(args.file, args.type):
        print("Import completed successfully!")
        
        if args.stats:
            importer.show_statistics()
    else:
        print("Import failed!")
        sys.exit(1)

if __name__ == "__main__":
    main()