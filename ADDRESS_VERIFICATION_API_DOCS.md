# Address Verification API Documentation

## Overview

The Address Verification API provides endpoints to validate addresses, cities, and locations against road network data. This service helps ensure data quality by verifying that addresses and locations are real and properly formatted.

## Base URL

```
/api/AddressVerification
```

## Endpoints

### 1. Verify Address

Validates a single address against road network data.

**Endpoint:** `POST /api/AddressVerification/verify-address`

**Request Body:**
```json
{
  "address": "123 Main Street",
  "provinceCode": "ON"  // Optional
}
```

**Response:**
```json
{
  "isValid": true,
  "streetName": "Main",
  "streetType": "Street",
  "cityName": "Toronto",
  "province": "Ontario",
  "provinceCode": "ON",
  "civicNumber": "123",
  "isInRange": true,
  "formattedAddress": "123 Main Street, Toronto, ON",
  "confidence": 0.95,
  "matchQuality": "Exact",
  "validationMessages": []
}
```

**Fields:**
- `isValid`: Whether the address was successfully validated
- `streetName`: Extracted street name
- `streetType`: Type of street (Street, Avenue, Road, etc.)
- `cityName`: Validated city name
- `province`: Full province name
- `provinceCode`: Two-letter province code
- `civicNumber`: House/building number
- `isInRange`: Whether the civic number is within valid range for the street
- `formattedAddress`: Properly formatted version of the address
- `confidence`: Confidence score (0.0 to 1.0)
- `matchQuality`: Quality of the match (Exact, High, Medium, Low)
- `validationMessages`: Array of validation messages/warnings

### 2. Verify City

Checks if a city name exists in the road network database.

**Endpoint:** `POST /api/AddressVerification/verify-city`

**Request Body:**
```json
{
  "cityName": "Toronto",
  "provinceCode": "ON"  // Optional
}
```

**Response:**
```json
{
  "isCity": true,
  "cityName": "Toronto",
  "province": "Ontario",
  "provinceCode": "ON",
  "matchCount": 1542,
  "confidence": 1.0
}
```

**Fields:**
- `isCity`: Whether the city was found in the database
- `cityName`: Normalized city name
- `province`: Full province name
- `provinceCode`: Two-letter province code
- `matchCount`: Number of streets found in this city
- `confidence`: Confidence score (0.0 to 1.0)

### 3. Verify Multiple Locations

Extracts and verifies multiple potential locations from a text block.

**Endpoint:** `POST /api/AddressVerification/verify-locations`

**Request Body:**
```json
{
  "text": "We have offices in Toronto, Vancouver, and Montreal.",
  "provinceCode": null  // Optional, filters to specific province
}
```

**Response:**
```json
{
  "locationsFound": 3,
  "locations": [
    {
      "isCity": true,
      "cityName": "Toronto",
      "province": "Ontario",
      "provinceCode": "ON",
      "matchCount": 1542,
      "confidence": 1.0
    },
    {
      "isCity": true,
      "cityName": "Vancouver",
      "province": "British Columbia",
      "provinceCode": "BC",
      "matchCount": 892,
      "confidence": 1.0
    },
    {
      "isCity": true,
      "cityName": "Montreal",
      "province": "Quebec",
      "provinceCode": "QC",
      "matchCount": 1103,
      "confidence": 1.0
    }
  ]
}
```

### 4. Get Cities in Province

Retrieves all cities/towns in a specific province.

**Endpoint:** `GET /api/AddressVerification/cities/{provinceCode}`

**Parameters:**
- `provinceCode` (path): Two-letter province code (e.g., ON, BC, QC)

**Example:** `GET /api/AddressVerification/cities/ON`

**Response:**
```json
{
  "provinceCode": "ON",
  "cityCount": 458,
  "cities": [
    "Ajax",
    "Aurora",
    "Barrie",
    "Brampton",
    "Burlington",
    "Cambridge",
    "Guelph",
    "Hamilton",
    "Kingston",
    "Kitchener",
    "London",
    "Markham",
    "Mississauga",
    "Oakville",
    "Ottawa",
    "Richmond Hill",
    "Toronto",
    "Vaughan",
    "Waterloo",
    "Windsor"
    // ... more cities
  ]
}
```

### 5. Get Streets in City

Retrieves all street names in a specific city.

**Endpoint:** `GET /api/AddressVerification/streets`

**Query Parameters:**
- `cityName` (required): Name of the city
- `provinceCode` (optional): Two-letter province code for disambiguation

**Example:** `GET /api/AddressVerification/streets?cityName=Toronto&provinceCode=ON`

**Response:**
```json
{
  "cityName": "Toronto",
  "provinceCode": "ON",
  "streetCount": 1542,
  "streets": [
    {
      "streetName": "Adelaide",
      "streetType": "Street",
      "direction": "East"
    },
    {
      "streetName": "Bay",
      "streetType": "Street",
      "direction": null
    },
    {
      "streetName": "Bloor",
      "streetType": "Street",
      "direction": "West"
    },
    {
      "streetName": "College",
      "streetType": "Street",
      "direction": null
    },
    {
      "streetName": "Dundas",
      "streetType": "Street",
      "direction": "West"
    },
    {
      "streetName": "King",
      "streetType": "Street",
      "direction": "West"
    },
    {
      "streetName": "Queen",
      "streetType": "Street",
      "direction": "East"
    },
    {
      "streetName": "University",
      "streetType": "Avenue",
      "direction": null
    },
    {
      "streetName": "Yonge",
      "streetType": "Street",
      "direction": null
    }
    // ... more streets
  ]
}
```

### 6. Batch Verify Addresses

Validates multiple addresses in a single request.

**Endpoint:** `POST /api/AddressVerification/batch-verify`

**Request Body:**
```json
{
  "addresses": [
    "123 Main Street, Toronto",
    "456 Elm Avenue, Ottawa",
    "789 Oak Road, Hamilton"
  ],
  "provinceCode": "ON"  // Optional, applies to all addresses
}
```

**Response:**
```json
{
  "totalProcessed": 3,
  "validCount": 2,
  "invalidCount": 1,
  "results": [
    {
      "input": "123 Main Street, Toronto",
      "isValid": true,
      "formattedAddress": "123 Main Street, Toronto, ON",
      "confidence": 0.95,
      "matchQuality": "Exact",
      "cityName": "Toronto",
      "validationMessages": []
    },
    {
      "input": "456 Elm Avenue, Ottawa",
      "isValid": true,
      "formattedAddress": "456 Elm Avenue, Ottawa, ON",
      "confidence": 0.92,
      "matchQuality": "High",
      "cityName": "Ottawa",
      "validationMessages": []
    },
    {
      "input": "789 Oak Road, Hamilton",
      "isValid": false,
      "formattedAddress": null,
      "confidence": 0.0,
      "matchQuality": "None",
      "cityName": null,
      "validationMessages": ["Street not found in database"]
    }
  ]
}
```

## Error Responses

All endpoints return standard HTTP status codes:

### 400 Bad Request
```json
{
  "error": "Address is required"
}
```

### 500 Internal Server Error
```json
{
  "error": "An error occurred while verifying the address"
}
```

## Usage Examples

### cURL Examples

#### Verify a single address:
```bash
curl -X POST https://api.example.com/api/AddressVerification/verify-address \
  -H "Content-Type: application/json" \
  -d '{
    "address": "123 Bay Street, Toronto",
    "provinceCode": "ON"
  }'
```

#### Check if a city exists:
```bash
curl -X POST https://api.example.com/api/AddressVerification/verify-city \
  -H "Content-Type: application/json" \
  -d '{
    "cityName": "Toronto"
  }'
```

#### Get all cities in Ontario:
```bash
curl -X GET https://api.example.com/api/AddressVerification/cities/ON
```

#### Get streets in a city:
```bash
curl -X GET "https://api.example.com/api/AddressVerification/streets?cityName=Toronto&provinceCode=ON"
```

#### Batch verify multiple addresses:
```bash
curl -X POST https://api.example.com/api/AddressVerification/batch-verify \
  -H "Content-Type: application/json" \
  -d '{
    "addresses": [
      "123 Main St",
      "456 Queen St",
      "789 King St"
    ],
    "provinceCode": "ON"
  }'
```

### JavaScript/Fetch Examples

#### Verify an address:
```javascript
const verifyAddress = async (address, provinceCode) => {
  const response = await fetch('/api/AddressVerification/verify-address', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json'
    },
    body: JSON.stringify({
      address: address,
      provinceCode: provinceCode
    })
  });
  
  const result = await response.json();
  
  if (result.isValid) {
    console.log(`Valid address: ${result.formattedAddress}`);
    console.log(`Confidence: ${result.confidence}`);
  } else {
    console.log('Invalid address');
    console.log('Messages:', result.validationMessages);
  }
  
  return result;
};

// Usage
verifyAddress('123 Bay Street, Toronto', 'ON');
```

#### Get cities in a province:
```javascript
const getCitiesInProvince = async (provinceCode) => {
  const response = await fetch(`/api/AddressVerification/cities/${provinceCode}`);
  const result = await response.json();
  
  console.log(`Found ${result.cityCount} cities in ${provinceCode}`);
  return result.cities;
};

// Usage
const ontarioCities = await getCitiesInProvince('ON');
```

### C#/.NET Examples

```csharp
using System.Net.Http;
using System.Text;
using System.Text.Json;

public class AddressVerificationClient
{
    private readonly HttpClient _httpClient;
    
    public AddressVerificationClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }
    
    public async Task<VerifyAddressResponse> VerifyAddressAsync(string address, string provinceCode = null)
    {
        var request = new VerifyAddressRequest
        {
            Address = address,
            ProvinceCode = provinceCode
        };
        
        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        
        var response = await _httpClient.PostAsync("/api/AddressVerification/verify-address", content);
        response.EnsureSuccessStatusCode();
        
        var responseJson = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<VerifyAddressResponse>(responseJson);
    }
    
    public async Task<List<string>> GetCitiesInProvinceAsync(string provinceCode)
    {
        var response = await _httpClient.GetAsync($"/api/AddressVerification/cities/{provinceCode}");
        response.EnsureSuccessStatusCode();
        
        var responseJson = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<GetCitiesResponse>(responseJson);
        return result.Cities;
    }
}
```

## Best Practices

1. **Province Codes**: Always provide province codes when known to improve accuracy and performance.

2. **Batch Processing**: Use the batch verify endpoint for processing multiple addresses to reduce API calls.

3. **Confidence Scores**: Consider the confidence score when making decisions:
   - 0.9-1.0: High confidence, exact match
   - 0.7-0.89: Good confidence, minor variations
   - 0.5-0.69: Medium confidence, fuzzy match
   - Below 0.5: Low confidence, review required

4. **Error Handling**: Always implement proper error handling for 400 and 500 status codes.

5. **Caching**: Consider caching city lists and street lists as they don't change frequently.

6. **Rate Limiting**: Be aware of any rate limits on the API and implement appropriate throttling.

## Province Codes Reference

| Code | Province/Territory |
|------|-------------------|
| AB | Alberta |
| BC | British Columbia |
| MB | Manitoba |
| NB | New Brunswick |
| NL | Newfoundland and Labrador |
| NS | Nova Scotia |
| NT | Northwest Territories |
| NU | Nunavut |
| ON | Ontario |
| PE | Prince Edward Island |
| QC | Quebec |
| SK | Saskatchewan |
| YT | Yukon |

## Support

For questions or issues with the Address Verification API, please contact the development team or refer to the main API documentation.