# PhoneBookParserAPI - API Usage Manual

## Base URL
```
http://localhost:5000/api
```

## API Endpoints

### 1. Parser Controller
Parse phone book entries into structured name, address, and phone number components.

#### **POST** `/api/parser/parse`
Parse a single phone book entry.

**Request Body:**
```json
{
  "input": "John Smith 123 Main St 555-1234",
  "province": "ON"  // Optional: NS, NB, PE, NL, ON, etc.
}
```

**Response:**
```json
{
  "success": true,
  "errorMessage": null,
  "input": "John Smith 123 Main St 555-1234",
  "name": "John Smith",
  "address": "123 Main St",
  "phone": "555-1234",
  "isBusinessName": false,
  "isResidentialName": true,
  "confidence": {
    "nameConfidence": 95,
    "addressConfidence": 90,
    "phoneConfidence": 100,
    "notes": "High confidence parsing"
  }
}
```

#### **GET** `/api/parser/parse`
Parse via query parameters.

**Query Parameters:**
- `input` (required): The text to parse
- `province` (optional): Province code

**Example:**
```
GET /api/parser/parse?input=John%20Smith%20123%20Main%20St&province=NS
```

#### **POST** `/api/parser/parse/batch`
Parse multiple entries at once (max 500).

**Request Body:**
```json
{
  "inputs": [
    "John Smith 123 Main St 555-1234",
    "ABC Company 456 Oak Ave 555-5678"
  ],
  "province": "NS"  // Optional: applies to all entries
}
```

**Response:**
```json
{
  "results": [...],
  "totalProcessed": 2,
  "successCount": 2,
  "failureCount": 0
}
```

---

### 2. Classification Controller
Classify text as business or residential.

#### **POST** `/api/classification/classify`
Detailed classification with confidence scores.

**Request Body:**
```json
{
  "input": "ABC Construction Ltd",
  "includeDetails": false  // Optional: get detailed analysis
}
```

**Response:**
```json
{
  "success": true,
  "message": null,
  "input": "ABC Construction Ltd",
  "classification": "business",
  "confidence": 95,
  "isBusiness": true,
  "isResidential": false,
  "reason": "Company suffix detected",
  "businessScore": 95,
  "residentialScore": 5,
  "detailedAnalysis": null
}
```

#### **POST** `/api/classification/classify/batch`
Batch classification (max 500 inputs).

**Request Body:**
```json
{
  "inputs": [
    "John Smith",
    "ABC Company Ltd",
    "Mary Johnson"
  ]
}
```

**Response:**
```json
{
  "success": true,
  "totalProcessed": 3,
  "businessCount": 1,
  "residentialCount": 2,
  "unknownCount": 0,
  "results": [...]
}
```

#### **GET** `/api/classification/is-residential`
Quick check if text is residential.

**Query Parameters:**
- `input` (required): Text to check

**Response:** `true` or `false`

#### **GET** `/api/classification/is-business`
Quick check if text is business.

**Query Parameters:**
- `input` (required): Text to check

**Response:** `true` or `false`

---

### 3. Business Name Controller
Dedicated business name detection with indicators.

#### **POST** `/api/business-name/check`
Check if input is a business name.

**Request Body:**
```json
{
  "input": "ABC Construction Ltd"
}
```

**Response:**
```json
{
  "input": "ABC Construction Ltd",
  "isBusinessName": true,
  "confidence": 0.95,
  "matchedIndicators": ["Construction", "Ltd"],
  "wordsProcessed": 3
}
```

#### **GET** `/api/business-name/health`
Health check endpoint.

**Response:**
```json
{
  "status": "healthy",
  "timestamp": "2025-01-01T00:00:00Z"
}
```

---

### 4. Street Search Controller
Search and validate street names from road network database.

#### **GET** `/api/streetsearch/search`
Search for streets by name.

**Query Parameters:**
- `searchTerm` (required): Street name to search (min 2 chars)
- `province` (optional): Province code (e.g., 'ON') or name (e.g., 'Ontario')
- `maxResults` (optional): Max results to return (default: 100)

**Example:**
```
GET /api/streetsearch/search?searchTerm=Main&province=NS&maxResults=10
```

**Response:**
```json
{
  "searchTerm": "Main",
  "province": "NS",
  "totalResults": 5,
  "results": [
    {
      "streetName": "Main Street",
      "province": "NS",
      "city": "Halifax"
    }
  ]
}
```

#### **GET** `/api/streetsearch/exists`
Check if a street name exists.

**Query Parameters:**
- `name` (required): Street name
- `province` (optional): Province filter

**Response:**
```json
{
  "streetName": "Main Street",
  "province": "NS",
  "exists": true
}
```

#### **GET** `/api/streetsearch/types`
Get all unique street types in database.

**Response:**
```json
{
  "totalTypes": 25,
  "types": ["Street", "Avenue", "Road", "Boulevard", ...]
}
```

#### **GET** `/api/streetsearch/stats`
Get street database statistics.

**Query Parameters:**
- `province` (optional): Filter by province

**Response:**
```json
{
  "province": "NS",
  "statistics": {
    "totalStreets": 1500,
    "uniqueTypes": 20,
    "cities": 45
  }
}
```

#### **GET** `/api/streetsearch/provinces`
Get list of supported provinces.

**Response:**
```json
{
  "totalProvinces": 13,
  "provinces": [
    {"code": "AB", "name": "Alberta"},
    {"code": "BC", "name": "British Columbia"},
    ...
  ]
}
```

#### **GET** `/api/streetsearch/type-mappings`
Get street type abbreviation mappings.

**Response:**
```json
{
  "totalMappings": 50,
  "categories": ["Primary", "Secondary", "Special"],
  "mappings": [
    {"abbreviation": "ave", "fullName": "Avenue"},
    {"abbreviation": "blvd", "fullName": "Boulevard"},
    ...
  ]
}
```

#### **GET** `/api/streetsearch/normalize-type`
Normalize a street type abbreviation.

**Query Parameters:**
- `streetType` (required): Type to normalize (e.g., 'rd', 'ave')

**Response:**
```json
{
  "input": "rd",
  "normalized": "Road",
  "abbreviation": "rd",
  "isKnownType": true
}
```

#### **GET** `/api/streetsearch/get-abbreviation`
Get abbreviation for a street type.

**Query Parameters:**
- `fullName` (required): Full street type name

**Response:**
```json
{
  "fullName": "Avenue",
  "abbreviation": "ave",
  "isKnownType": true
}
```

---

## Error Responses

All endpoints return consistent error responses:

**400 Bad Request:**
```json
{
  "success": false,
  "message": "Input cannot be empty"
}
```

**500 Internal Server Error:**
```json
{
  "error": "An error occurred while processing your request",
  "timestamp": "2025-01-01T00:00:00Z"
}
```

---

## Usage Examples

### Example 1: Parse a phone book entry
```bash
curl -X POST http://localhost:5000/api/parser/parse \
  -H "Content-Type: application/json" \
  -d '{"input": "John Smith 123 Main St Halifax NS 902-555-1234"}'
```

### Example 2: Classify multiple entries
```bash
curl -X POST http://localhost:5000/api/classification/classify/batch \
  -H "Content-Type: application/json" \
  -d '{"inputs": ["ABC Ltd", "John Smith", "XYZ Corp"]}'
```

### Example 3: Search for streets
```bash
curl "http://localhost:5000/api/streetsearch/search?searchTerm=Water&province=NS"
```

### Example 4: Quick business check
```bash
curl "http://localhost:5000/api/classification/is-business?input=ABC%20Construction%20Ltd"
```

---

## Notes

- Maximum batch size: 500 items for all batch endpoints
- Province codes: NS, NB, PE, NL, ON, QC, MB, SK, AB, BC, YT, NT, NU
- All text inputs have a maximum length of 500 characters
- Street search requires minimum 2 characters
- Confidence scores range from 0-100 (or 0.0-1.0 for business name endpoint)