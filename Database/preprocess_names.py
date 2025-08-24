#!/usr/bin/env python3
"""
Preprocess names CSV file to clean format for SQL import
Creates a clean CSV ready for LOAD DATA INFILE
"""

import re
import sys
import argparse
from pathlib import Path

class NameCleaner:
    def __init__(self):
        # Titles to remove
        self.titles = {
            'mr', 'mrs', 'ms', 'miss', 'dr', 'prof', 'professor', 
            'rev', 'reverend', 'fr', 'father', 'sr', 'jr', 'sir',
            'lord', 'lady', 'master', 'mister', 'madam', 'dame'
        }
        
        # Business words to exclude
        self.business_words = {
            'ltd', 'inc', 'corp', 'llc', 'company', 'co', 'corporation',
            'limited', 'incorporated', 'enterprises', 'group', 'holdings',
            'mgr', 'manager', 'supervisor', 'director', 'president',
            'trainer', 'admin', 'administrator', 'office', 'center'
        }
    
    def clean_name(self, name):
        """Clean and split names"""
        if not name:
            return []
        
        # Convert to lowercase
        name_lower = name.lower().strip()
        
        # Remove parenthetical content
        name_lower = re.sub(r'\([^)]*\)', '', name_lower).strip()
        
        # Remove dots
        name_lower = re.sub(r'\.', ' ', name_lower).strip()
        
        # Split into parts
        parts = name_lower.split()
        
        # Filter out titles and business words
        clean_parts = []
        for part in parts:
            part_clean = part.strip().strip(',')
            
            if not part_clean:
                continue
            if part_clean in self.titles:
                continue
            if part_clean in self.business_words:
                continue
            if not re.match(r'^[a-z\-\']+$', part_clean):
                continue
            if len(part_clean) == 1:
                continue
                
            clean_parts.append(part_clean)
        
        # Handle hyphenated names
        final_parts = []
        for part in clean_parts:
            if '-' in part and len(part) > 2:
                sub_parts = part.split('-')
                for sub_part in sub_parts:
                    if sub_part and len(sub_part) > 1:
                        final_parts.append(sub_part)
            else:
                final_parts.append(part)
        
        return final_parts
    
    def process_file(self, input_file, output_file, name_type='first'):
        """Process input file and create clean output"""
        
        clean_names = {}
        skipped = []
        
        with open(input_file, 'r', encoding='utf-8', errors='ignore') as f:
            lines = f.readlines()
        
        # Skip header
        start_idx = 0
        for i, line in enumerate(lines):
            if 'fname' in line.lower() or 'lname' in line.lower() or 'count' in line.lower():
                start_idx = i + 1
            elif line.strip().startswith('-'):
                start_idx = i + 1
            else:
                break
        
        # Process lines
        for line in lines[start_idx:]:
            line = line.strip()
            if not line:
                continue
            
            # Parse line
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
            
            # Clean the name
            cleaned = self.clean_name(raw_name)
            
            if not cleaned:
                skipped.append(raw_name)
                continue
            
            # Add each clean part
            for clean_name in cleaned:
                if clean_name in clean_names:
                    clean_names[clean_name] += count
                else:
                    clean_names[clean_name] = count
        
        # Write output file
        with open(output_file, 'w', encoding='utf-8') as f:
            for name, count in sorted(clean_names.items()):
                f.write(f"{name}\t{name_type}\t{count}\n")
        
        print(f"Processed {len(clean_names)} unique names")
        print(f"Skipped {len(skipped)} invalid entries")
        if skipped and len(skipped) <= 10:
            print(f"Examples of skipped: {', '.join(skipped[:10])}")
        
        return output_file

def main():
    parser = argparse.ArgumentParser(description='Clean names CSV for import')
    parser.add_argument('input', help='Input CSV file')
    parser.add_argument('output', help='Output clean CSV file')
    parser.add_argument('--type', choices=['first', 'last'], required=True,
                       help='Type of names')
    
    args = parser.parse_args()
    
    cleaner = NameCleaner()
    cleaner.process_file(args.input, args.output, args.type)
    
    print(f"\nClean file created: {args.output}")
    print("\nTo import to database, run:")
    print(f"mysql -u root -p bor_db -e \"LOAD DATA LOCAL INFILE '{args.output}' INTO TABLE names")
    print(f"  FIELDS TERMINATED BY '\\t'")
    print(f"  LINES TERMINATED BY '\\n'")
    print(f"  (name_lower, name_type, @count)")
    print(f"  SET name_count = @count, last_seen = NOW()") 
    print(f"  ON DUPLICATE KEY UPDATE name_count = name_count + VALUES(name_count);\"")

if __name__ == "__main__":
    main()