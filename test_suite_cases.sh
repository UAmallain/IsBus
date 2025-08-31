#!/bin/bash

# Test cases for suite number detection

echo "Testing Suite Number Detection"
echo "=============================="

# Test 1: Suite number connected to phone
echo -e "\n1. Testing: Suite 209857-5732"
curl -s -X POST http://localhost:5000/api/parse \
  -H "Content-Type: application/json" \
  -d '{"input": "Abed Sangani Masoud Dr 100 Arden Suite 209857-5732", "enableDebug": false}' | \
  python3 -c "import sys, json; d=json.load(sys.stdin); print(f\"  Phone: {d.get('phone','')}, Address: {d.get('address','')[:50]}...\")"

# Test 2: Suite number with space before phone  
echo -e "\n2. Testing: Suite 209 857-5732"
curl -s -X POST http://localhost:5000/api/parse \
  -H "Content-Type: application/json" \
  -d '{"input": "Abed Sangani Masoud Dr 100 Arden Suite 209 857-5732", "enableDebug": false}' | \
  python3 -c "import sys, json; d=json.load(sys.stdin); print(f\"  Phone: {d.get('phone','')}, Address: {d.get('address','')[:50]}...\")"

# Test 3: Apt number connected to phone
echo -e "\n3. Testing: Apt 4506234-5678"
curl -s -X POST http://localhost:5000/api/parse \
  -H "Content-Type: application/json" \
  -d '{"input": "Smith John 123 Main St Apt 4506234-5678", "enableDebug": false}' | \
  python3 -c "import sys, json; d=json.load(sys.stdin); print(f\"  Phone: {d.get('phone','')}, Address: {d.get('address','')[:50]}...\")"

# Test 4: Apt number with space before phone
echo -e "\n4. Testing: Apt 450 623-4567"
curl -s -X POST http://localhost:5000/api/parse \
  -H "Content-Type: application/json" \
  -d '{"input": "Smith John 123 Main St Apt 450 623-4567", "enableDebug": false}' | \
  python3 -c "import sys, json; d=json.load(sys.stdin); print(f\"  Phone: {d.get('phone','')}, Address: {d.get('address','')[:50]}...\")"

# Test 5: Unit number
echo -e "\n5. Testing: Unit 127 894-3212"
curl -s -X POST http://localhost:5000/api/parse \
  -H "Content-Type: application/json" \
  -d '{"input": "Johnson Mary 456 Oak Ave Unit 127 894-3212", "enableDebug": false}' | \
  python3 -c "import sys, json; d=json.load(sys.stdin); print(f\"  Phone: {d.get('phone','')}, Address: {d.get('address','')[:50]}...\")"

# Test 6: No suite (control)
echo -e "\n6. Testing: No suite - just phone"
curl -s -X POST http://localhost:5000/api/parse \
  -H "Content-Type: application/json" \
  -d '{"input": "Wilson James 246 Birch Way 234-5678", "enableDebug": false}' | \
  python3 -c "import sys, json; d=json.load(sys.stdin); print(f\"  Phone: {d.get('phone','')}, Address: {d.get('address','')[:50]}...\")"

echo -e "\n=============================="
echo "Test Complete"