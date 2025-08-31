#!/usr/bin/env python3
import json
import requests
import time

# Wait for API to be ready
time.sleep(2)

test_cases = [
    ("Abed Sangani Masoud Dr 100 Arden Suite 209857-5732", "Suite 209, Phone 857-5732"),
    ("Smith John 123 Main St Apt 4506234-5678", "Apt 450, Phone 623-4567"),
    ("Johnson Mary 456 Oak Ave Unit 12789432-1234", "Unit 127, Phone 894-3212"),
    ("Williams Bob 789 Pine Rd Ste 305555-1234", "Ste 30, Phone 555-5123"),
    ("Brown Alice 321 Elm St Floor 2467890-1234", "Floor 24, Phone 678-9012"),
    ("Davis Tom 654 Maple Dr #567892-3456", "#5, Phone 678-9234"),
    ("Miller Susan 987 Cedar Ln Room 89012345-6789", "Room 890, Phone 123-4567"),
    ("Wilson James 246 Birch Way 234-5678", "No suite, Phone 234-5678"),
    ("Taylor Michael 864 Spruce Ave 345-6789", "No suite, Phone 345-6789"),
]

print("Testing Suite Number Detection")
print("=" * 60)

for input_text, expected in test_cases:
    try:
        response = requests.post(
            "http://localhost:5000/api/parse",
            json={"input": input_text, "enableDebug": False},
            timeout=5
        )
        
        if response.status_code == 200:
            result = response.json()
            phone = result.get('phone', '')
            address = result.get('address', '')
            
            # Check if suite number is in address
            suite_found = False
            for indicator in ['Suite', 'Apt', 'Unit', 'Ste', 'Floor', '#', 'Room']:
                if indicator in address:
                    suite_found = True
                    break
            
            print(f"\nInput: {input_text}")
            print(f"Expected: {expected}")
            print(f"Phone: {phone}")
            print(f"Address: {address}")
            print(f"Suite in address: {'Yes' if suite_found else 'No'}")
            
        else:
            print(f"\nError for '{input_text}': Status {response.status_code}")
            
    except requests.exceptions.RequestException as e:
        print(f"\nError for '{input_text}': {e}")

print("\n" + "=" * 60)
print("Suite detection testing complete")