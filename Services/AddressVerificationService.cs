using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using IsBus.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace IsBus.Services;

public class AddressVerificationService : IAddressVerificationService
{
    private readonly PhonebookContext _context;
    private readonly IMemoryCache _cache;
    private readonly ILogger<AddressVerificationService> _logger;
    
    // Cache settings
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromHours(24);
    
    // Multi-word location indicators
    private readonly HashSet<string> _locationIndicators = new(StringComparer.OrdinalIgnoreCase)
    {
        "lake", "creek", "river", "bay", "valley", "hill", "mountain",
        "beach", "island", "park", "resort", "springs", "falls", "ridge",
        "point", "harbor", "harbour", "landing", "junction", "crossing"
    };
    
    public AddressVerificationService(
        PhonebookContext context,
        IMemoryCache cache,
        ILogger<AddressVerificationService> logger)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
    }
    
    public async Task<AddressVerificationResult> VerifyAddressAsync(string address, string? provinceCode = null)
    {
        var result = new AddressVerificationResult { IsValid = false };
        
        if (string.IsNullOrWhiteSpace(address))
        {
            result.ValidationMessages.Add("Address is empty");
            return result;
        }
        
        try
        {
            // Parse the address components
            var parsedAddress = ParseAddress(address);
            
            if (parsedAddress.CivicNumber.HasValue)
            {
                result.CivicNumber = parsedAddress.CivicNumber;
            }
            
            if (string.IsNullOrEmpty(parsedAddress.StreetName))
            {
                result.ValidationMessages.Add("No street name found in address");
                return result;
            }
            
            // Search for the street in road_network
            var query = _context.RoadNetworks
                .Where(r => r.Name != null && r.Name.ToLower() == parsedAddress.StreetName.ToLower());
            
            // Filter by province if provided
            if (!string.IsNullOrEmpty(provinceCode))
            {
                query = query.Where(r => 
                    r.ProvinceUidLeft == provinceCode.ToUpper() || 
                    r.ProvinceUidRight == provinceCode.ToUpper());
            }
            
            var roadNetworks = await query.ToListAsync();
            
            if (!roadNetworks.Any())
            {
                result.ValidationMessages.Add($"Street '{parsedAddress.StreetName}' not found");
                result.MatchQuality = "none";
                return result;
            }
            
            // Check if civic number is in range (if provided)
            if (parsedAddress.CivicNumber.HasValue && roadNetworks.Any())
            {
                var inRange = CheckCivicNumberInRange(parsedAddress.CivicNumber.Value, roadNetworks);
                result.IsInRange = inRange;
                
                if (!inRange)
                {
                    result.ValidationMessages.Add($"Civic number {parsedAddress.CivicNumber} may be out of range for this street");
                }
            }
            
            // Get the best match
            var bestMatch = roadNetworks.First();
            
            result.IsValid = true;
            result.StreetName = bestMatch.Name;
            result.StreetType = bestMatch.Type;
            
            // Get city name (prefer left side, then right side)
            result.CityName = !string.IsNullOrEmpty(bestMatch.CsdNameLeft) ? bestMatch.CsdNameLeft : bestMatch.CsdNameRight;
            
            // Get province info
            if (!string.IsNullOrEmpty(bestMatch.ProvinceUidLeft))
            {
                result.ProvinceCode = bestMatch.ProvinceUidLeft;
                result.Province = bestMatch.ProvinceNameLeft;
            }
            else if (!string.IsNullOrEmpty(bestMatch.ProvinceUidRight))
            {
                result.ProvinceCode = bestMatch.ProvinceUidRight;
                result.Province = bestMatch.ProvinceNameRight;
            }
            
            // Format the address
            result.FormattedAddress = FormatAddress(result);
            
            // Calculate confidence
            result.Confidence = CalculateConfidence(parsedAddress, bestMatch, result.IsInRange);
            result.MatchQuality = result.Confidence >= 0.8 ? "exact" : result.Confidence >= 0.5 ? "partial" : "none";
            
            _logger.LogDebug($"Address verified: {address} -> {result.FormattedAddress} (Confidence: {result.Confidence:P})");
            
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error verifying address: {address}");
            result.ValidationMessages.Add($"Error: {ex.Message}");
        }
        
        return result;
    }
    
    public async Task<CityVerificationResult> VerifyCityAsync(string cityName, string? provinceCode = null)
    {
        var result = new CityVerificationResult { IsCity = false };
        
        if (string.IsNullOrWhiteSpace(cityName))
        {
            return result;
        }
        
        // Check cache first
        var cacheKey = $"city_{cityName.ToLower()}_{provinceCode?.ToLower() ?? "all"}";
        if (_cache.TryGetValue<CityVerificationResult>(cacheKey, out var cachedResult))
        {
            _logger.LogDebug($"City verification cache hit: {cityName}");
            return cachedResult!;
        }
        
        try
        {
            var cityNameLower = cityName.ToLower().Trim();
            
            // Query for the city in road_network
            var query = _context.RoadNetworks
                .Where(r => 
                    (r.CsdNameLeft != null && r.CsdNameLeft.ToLower() == cityNameLower) ||
                    (r.CsdNameRight != null && r.CsdNameRight.ToLower() == cityNameLower));
            
            // Filter by province if provided
            if (!string.IsNullOrEmpty(provinceCode))
            {
                var provinceUpper = provinceCode.ToUpper();
                query = query.Where(r => 
                    r.ProvinceUidLeft == provinceUpper || 
                    r.ProvinceUidRight == provinceUpper);
            }
            
            var matches = await query
                .Select(r => new { 
                    CityLeft = r.CsdNameLeft,
                    CityRight = r.CsdNameRight,
                    ProvinceLeft = r.ProvinceNameLeft,
                    ProvinceCodeLeft = r.ProvinceUidLeft,
                    ProvinceRight = r.ProvinceNameRight,
                    ProvinceCodeRight = r.ProvinceUidRight
                })
                .Distinct()
                .ToListAsync();
            
            if (matches.Any())
            {
                var firstMatch = matches.First();
                
                result.IsCity = true;
                result.CityName = !string.IsNullOrEmpty(firstMatch.CityLeft) && 
                                 firstMatch.CityLeft.ToLower() == cityNameLower 
                                 ? firstMatch.CityLeft 
                                 : firstMatch.CityRight;
                
                // Get province info
                if (!string.IsNullOrEmpty(firstMatch.ProvinceCodeLeft))
                {
                    result.ProvinceCode = firstMatch.ProvinceCodeLeft;
                    result.Province = firstMatch.ProvinceLeft;
                }
                else if (!string.IsNullOrEmpty(firstMatch.ProvinceCodeRight))
                {
                    result.ProvinceCode = firstMatch.ProvinceCodeRight;
                    result.Province = firstMatch.ProvinceRight;
                }
                
                result.MatchCount = matches.Count;
                result.Confidence = 1.0; // Exact match
                
                _logger.LogDebug($"City verified: {cityName} -> {result.CityName} in {result.Province}");
            }
            else
            {
                // Try partial matching for multi-word cities
                var words = cityName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (words.Length > 1)
                {
                    // Try matching with partial name
                    var partialQuery = _context.RoadNetworks
                        .Where(r => 
                            (r.CsdNameLeft != null && r.CsdNameLeft.ToLower().Contains(cityNameLower)) ||
                            (r.CsdNameRight != null && r.CsdNameRight.ToLower().Contains(cityNameLower)));
                    
                    if (!string.IsNullOrEmpty(provinceCode))
                    {
                        var provinceUpper = provinceCode.ToUpper();
                        partialQuery = partialQuery.Where(r => 
                            r.ProvinceUidLeft == provinceUpper || 
                            r.ProvinceUidRight == provinceUpper);
                    }
                    
                    var partialMatches = await partialQuery
                        .Select(r => new { 
                            CityLeft = r.CsdNameLeft,
                            CityRight = r.CsdNameRight,
                            ProvinceLeft = r.ProvinceNameLeft,
                            ProvinceCodeLeft = r.ProvinceUidLeft,
                            ProvinceRight = r.ProvinceNameRight,
                            ProvinceCodeRight = r.ProvinceUidRight
                        })
                        .Take(5)
                        .ToListAsync();
                    
                    if (partialMatches.Any())
                    {
                        var firstMatch = partialMatches.First();
                        result.IsCity = true;
                        result.CityName = !string.IsNullOrEmpty(firstMatch.CityLeft) 
                                         ? firstMatch.CityLeft 
                                         : firstMatch.CityRight;
                        result.MatchCount = partialMatches.Count;
                        result.Confidence = 0.7; // Partial match
                        
                        _logger.LogDebug($"City partially matched: {cityName} -> {result.CityName}");
                    }
                }
            }
            
            // Cache the result
            _cache.Set(cacheKey, result, _cacheExpiration);
            
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error verifying city: {cityName}");
        }
        
        return result;
    }
    
    public async Task<List<CityVerificationResult>> VerifyMultipleLocationsAsync(string text, string? provinceCode = null)
    {
        var results = new List<CityVerificationResult>();
        
        if (string.IsNullOrWhiteSpace(text))
        {
            return results;
        }
        
        // Extract potential location names
        var potentialLocations = ExtractPotentialLocations(text);
        
        foreach (var location in potentialLocations)
        {
            var verificationResult = await VerifyCityAsync(location, provinceCode);
            if (verificationResult.IsCity || verificationResult.Confidence > 0.5)
            {
                results.Add(verificationResult);
            }
        }
        
        return results;
    }
    
    public async Task<List<string>> GetCitiesInProvinceAsync(string provinceCode)
    {
        var cacheKey = $"cities_in_{provinceCode.ToLower()}";
        if (_cache.TryGetValue<List<string>>(cacheKey, out var cachedCities))
        {
            return cachedCities!;
        }
        
        var provinceUpper = provinceCode.ToUpper();
        
        var cities = await _context.RoadNetworks
            .Where(r => r.ProvinceUidLeft == provinceUpper || r.ProvinceUidRight == provinceUpper)
            .Select(r => r.CsdNameLeft ?? r.CsdNameRight)
            .Where(c => c != null)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();
        
        var result = cities.Where(c => !string.IsNullOrEmpty(c)).Cast<string>().ToList();
        
        _cache.Set(cacheKey, result, _cacheExpiration);
        
        return result;
    }
    
    public async Task<List<string>> GetStreetsInCityAsync(string cityName, string? provinceCode = null)
    {
        var cacheKey = $"streets_in_{cityName.ToLower()}_{provinceCode?.ToLower() ?? "all"}";
        if (_cache.TryGetValue<List<string>>(cacheKey, out var cachedStreets))
        {
            return cachedStreets!;
        }
        
        var cityNameLower = cityName.ToLower();
        
        var query = _context.RoadNetworks
            .Where(r => 
                (r.CsdNameLeft != null && r.CsdNameLeft.ToLower() == cityNameLower) ||
                (r.CsdNameRight != null && r.CsdNameRight.ToLower() == cityNameLower));
        
        if (!string.IsNullOrEmpty(provinceCode))
        {
            var provinceUpper = provinceCode.ToUpper();
            query = query.Where(r => 
                r.ProvinceUidLeft == provinceUpper || 
                r.ProvinceUidRight == provinceUpper);
        }
        
        var streets = await query
            .Select(r => r.Name)
            .Where(n => n != null)
            .Distinct()
            .OrderBy(n => n)
            .ToListAsync();
        
        var result = streets.Where(s => !string.IsNullOrEmpty(s)).Cast<string>().ToList();
        
        _cache.Set(cacheKey, result, _cacheExpiration);
        
        return result;
    }
    
    private ParsedAddress ParseAddress(string address)
    {
        var parsed = new ParsedAddress();
        
        // Extract civic number (if present)
        var civicMatch = Regex.Match(address, @"^\d+");
        if (civicMatch.Success)
        {
            parsed.CivicNumber = int.Parse(civicMatch.Value);
            address = address.Substring(civicMatch.Length).Trim();
        }
        
        // Extract street name (everything before common street types)
        var streetTypePattern = @"\b(street|st|avenue|ave|road|rd|drive|dr|boulevard|blvd|lane|ln|way|court|ct|place|pl|crescent|cres)\b";
        var streetTypeMatch = Regex.Match(address, streetTypePattern, RegexOptions.IgnoreCase);
        
        if (streetTypeMatch.Success)
        {
            parsed.StreetName = address.Substring(0, streetTypeMatch.Index).Trim();
            parsed.StreetType = streetTypeMatch.Value;
        }
        else
        {
            // No street type found, use the whole remaining string as street name
            parsed.StreetName = address;
        }
        
        return parsed;
    }
    
    private bool CheckCivicNumberInRange(int civicNumber, List<RoadNetwork> roadNetworks)
    {
        foreach (var road in roadNetworks)
        {
            // Check left side
            if (TryParseRange(road.AddressFromLeft, road.AddressToLeft, out var leftFrom, out var leftTo))
            {
                if (civicNumber >= leftFrom && civicNumber <= leftTo)
                {
                    // Check if civic number matches odd/even pattern
                    if ((civicNumber % 2) == (leftFrom % 2))
                    {
                        return true;
                    }
                }
            }
            
            // Check right side
            if (TryParseRange(road.AddressFromRight, road.AddressToRight, out var rightFrom, out var rightTo))
            {
                if (civicNumber >= rightFrom && civicNumber <= rightTo)
                {
                    // Check if civic number matches odd/even pattern
                    if ((civicNumber % 2) == (rightFrom % 2))
                    {
                        return true;
                    }
                }
            }
        }
        
        return false;
    }
    
    private bool TryParseRange(string? from, string? to, out int fromNum, out int toNum)
    {
        fromNum = 0;
        toNum = 0;
        
        if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to))
            return false;
        
        // Extract numbers from strings (they might contain letters)
        var fromMatch = Regex.Match(from, @"\d+");
        var toMatch = Regex.Match(to, @"\d+");
        
        if (fromMatch.Success && toMatch.Success)
        {
            fromNum = int.Parse(fromMatch.Value);
            toNum = int.Parse(toMatch.Value);
            return true;
        }
        
        return false;
    }
    
    private string FormatAddress(AddressVerificationResult result)
    {
        var parts = new List<string>();
        
        if (result.CivicNumber.HasValue)
        {
            parts.Add(result.CivicNumber.Value.ToString());
        }
        
        if (!string.IsNullOrEmpty(result.StreetName))
        {
            parts.Add(result.StreetName);
        }
        
        if (!string.IsNullOrEmpty(result.StreetType))
        {
            parts.Add(result.StreetType);
        }
        
        if (!string.IsNullOrEmpty(result.CityName))
        {
            parts.Add(result.CityName);
        }
        
        if (!string.IsNullOrEmpty(result.ProvinceCode))
        {
            parts.Add(result.ProvinceCode);
        }
        
        return string.Join(" ", parts);
    }
    
    private double CalculateConfidence(ParsedAddress parsed, RoadNetwork match, bool inRange)
    {
        double confidence = 0.5; // Base confidence for finding a street
        
        // Exact street name match
        if (match.Name?.Equals(parsed.StreetName, StringComparison.OrdinalIgnoreCase) == true)
        {
            confidence += 0.3;
        }
        
        // Civic number in range
        if (parsed.CivicNumber.HasValue && inRange)
        {
            confidence += 0.2;
        }
        
        return Math.Min(confidence, 1.0);
    }
    
    private List<string> ExtractPotentialLocations(string text)
    {
        var locations = new List<string>();
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        // Single words that might be locations (capitalized)
        foreach (var word in words)
        {
            if (word.Length > 2 && char.IsUpper(word[0]) && !Regex.IsMatch(word, @"^\d+$"))
            {
                locations.Add(word);
            }
        }
        
        // Two-word combinations
        for (int i = 0; i < words.Length - 1; i++)
        {
            var twoWord = $"{words[i]} {words[i + 1]}";
            
            // Check if second word is a location indicator
            if (_locationIndicators.Contains(words[i + 1].ToLower()))
            {
                locations.Add(twoWord);
            }
            // Or if both words are capitalized
            else if (words[i].Length > 1 && words[i + 1].Length > 1 && 
                     char.IsUpper(words[i][0]) && char.IsUpper(words[i + 1][0]))
            {
                locations.Add(twoWord);
            }
        }
        
        // Three-word combinations for specific patterns (e.g., "Ville de Montreal")
        for (int i = 0; i < words.Length - 2; i++)
        {
            var middleWord = words[i + 1].ToLower();
            if (middleWord == "de" || middleWord == "la" || middleWord == "du" || 
                middleWord == "des" || middleWord == "of" || middleWord == "the")
            {
                var threeWord = $"{words[i]} {words[i + 1]} {words[i + 2]}";
                locations.Add(threeWord);
            }
        }
        
        // Remove duplicates while preserving order
        return locations.Distinct().ToList();
    }
    
    private class ParsedAddress
    {
        public int? CivicNumber { get; set; }
        public string? StreetName { get; set; }
        public string? StreetType { get; set; }
    }
}