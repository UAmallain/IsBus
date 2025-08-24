#!/usr/bin/env python3
"""
Combined setup script to create both province and street type mappings.
Run this to set up all mapping tables needed for the street search functionality.
"""

import subprocess
import sys
import os

def run_script(script_name):
    """Run a Python script and return its exit code."""
    print(f"\nRunning {script_name}...")
    print("-" * 50)
    
    try:
        result = subprocess.run(
            [sys.executable, script_name],
            capture_output=False,
            text=True
        )
        return result.returncode
    except Exception as e:
        print(f"Error running {script_name}: {e}")
        return 1

def main():
    """Main function to run all setup scripts."""
    print("=" * 60)
    print("Complete Mapping Setup for Street Search API")
    print("=" * 60)
    print("\nThis script will set up:")
    print("  1. Province mappings (2-char codes to full names)")
    print("  2. Street type mappings (abbreviations to full names)")
    print()
    
    scripts = [
        "setup_province_mapping.py",
        "setup_street_type_mapping.py"
    ]
    
    all_success = True
    
    for script in scripts:
        if not os.path.exists(script):
            print(f"✗ Script not found: {script}")
            all_success = False
            continue
            
        exit_code = run_script(script)
        if exit_code != 0:
            print(f"✗ {script} failed with exit code {exit_code}")
            all_success = False
        else:
            print(f"✓ {script} completed successfully")
    
    print("\n" + "=" * 60)
    if all_success:
        print("✓ All mappings have been set up successfully!")
        print("=" * 60)
        print("\nYour street search API now supports:")
        print("  • Province-based filtering (13 provinces/territories)")
        print("  • Street type normalization (130+ abbreviations)")
        print("  • Canadian-specific street types")
        print("  • French Canadian street types")
        print("\nTest the API with:")
        print("  • GET /api/StreetSearch/search?searchTerm=Main")
        print("  • GET /api/StreetSearch/provinces")
        print("  • GET /api/StreetSearch/type-mappings")
    else:
        print("✗ Some setup scripts failed. Please check the errors above.")
        print("=" * 60)
    
    input("\nPress Enter to exit...")

if __name__ == "__main__":
    main()