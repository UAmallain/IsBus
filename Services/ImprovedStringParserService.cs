using IsBus.Models;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace IsBus.Services;

/// <summary>
/// Improved string parser that better handles multi-word street names and unit numbers
/// </summary>
public class ImprovedStringParserService : IStringParserService
{
    private readonly IClassificationService _classificationService;
    private readonly ICommunityService _communityService;
    private readonly IStreetTypeService _streetTypeService;
    private readonly IStreetNameService _streetNameService;
    private readonly ILogger<ImprovedStringParserService> _logger;
    
    // Phone number patterns
    private readonly Regex _phonePattern = new Regex(
        @"(\d{3}[\s-]?\d{3}[\s-]?\d{4}|\d{3}[\s-]?\d{4})$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    
    // Area code + local number pattern
    private readonly Regex _areaCodePhonePattern = new Regex(
        @"(\d{3})\s+(\d{3}[\s-]?\d{4})$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    
    // Business-related words that wouldn't be part of a street name
    private readonly HashSet<string> _businessWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "sunrooms", "company", "inc", "ltd", "corp", "corporation", 
        "services", "service", "store", "shop", "restaurant", "cafe",
        "motors", "automotive", "garage", "salon", "spa", "clinic",
        "center", "centre", "market", "mart", "plaza", "mall",
        "enterprises", "industries", "manufacturing", "construction",
        "plumbing", "electric", "electrical", "roofing", "painting",
        "consulting", "solutions", "systems", "technology", "tech",
        "atlantic", "pacific", "canadian", "national", "international",
        "global", "premier", "professional", "quality", "advanced"
    };
    
    public ImprovedStringParserService(
        IClassificationService classificationService,
        ICommunityService communityService,
        IStreetTypeService streetTypeService,
        IStreetNameService streetNameService,
        ILogger<ImprovedStringParserService> logger)
    {
        _classificationService = classificationService;
        _communityService = communityService;
        _streetTypeService = streetTypeService;
        _streetNameService = streetNameService;
        _logger = logger;
    }
    
    public async Task<ParseResult> ParseAsync(string input, string? province = null, string? areaCode = null)
    {
        var result = new ParseResult { Input = input };
        
        if (string.IsNullOrWhiteSpace(input))
        {
            result.Success = false;
            result.ErrorMessage = "Input cannot be empty";
            return result;
        }
        
        // Normalize input
        input = Regex.Replace(input.Trim(), @"\s+", " ");
        
        // Step 1: Extract phone number
        var phoneExtraction = ExtractPhoneNumber(input);
        if (!phoneExtraction.Success)
        {
            result.Success = false;
            result.ErrorMessage = phoneExtraction.ErrorMessage;
            return result;
        }
        
        result.Phone = phoneExtraction.Phone;
        var remainingText = phoneExtraction.RemainingText.Trim();
        
        // Step 2: Find address using improved algorithm
        var addressInfo = await FindAddressImproved(remainingText, province);
        
        if (addressInfo.AddressStartIndex >= 0)
        {
            result.Name = remainingText.Substring(0, addressInfo.AddressStartIndex).Trim();
            result.Address = remainingText.Substring(addressInfo.AddressStartIndex).Trim();
            result.Confidence.AddressConfidence = addressInfo.Confidence;
        }
        else
        {
            // No address found
            result.Name = remainingText;
            result.Confidence.AddressConfidence = 0;
        }
        
        // Step 3: Classify the name
        if (!string.IsNullOrWhiteSpace(result.Name))
        {
            var classification = await _classificationService.ClassifyAsync(result.Name);
            result.IsBusinessName = classification.IsBusiness;
            result.IsResidentialName = classification.IsResidential;
            result.Confidence.NameConfidence = classification.Confidence;
        }
        
        result.Confidence.PhoneConfidence = 100;
        result.Success = true;
        
        _logger.LogInformation($"Parsed: '{input}' -> Name: '{result.Name}', Address: '{result.Address}', Phone: '{result.Phone}'");
        
        return result;
    }
    
    private async Task<AddressInfo> FindAddressImproved(string text, string? province)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var addressInfo = new AddressInfo { AddressStartIndex = -1, Confidence = 0 };
        
        // Look for street types in the text
        for (int i = 0; i < words.Length; i++)
        {
            var word = words[i].Trim('.', ',');
            
            if (_streetTypeService.IsStreetType(word))
            {
                // Found a street type at position i
                // Now determine where the address starts
                
                // Check for civic/unit numbers before the street name
                int addressStart = i;
                int firstNumberIndex = -1;
                
                // Look backwards for numbers
                for (int j = i - 1; j >= 0; j--)
                {
                    if (Regex.IsMatch(words[j], @"^\d+$"))
                    {
                        firstNumberIndex = j;
                        
                        // Check if there's another number before this one (unit number pattern)
                        if (j > 0 && Regex.IsMatch(words[j - 1], @"^\d+$"))
                        {
                            firstNumberIndex = j - 1;
                        }
                        break;
                    }
                }
                
                if (firstNumberIndex >= 0)
                {
                    // We have numbers, address starts at first number
                    addressStart = firstNumberIndex;
                    addressInfo.Confidence = 90;
                }
                else
                {
                    // No numbers, need to find where street name starts
                    // Try all possible street name lengths and check database
                    int bestMatch = await FindBestStreetNameMatch(words, i, province);
                    if (bestMatch >= 0)
                    {
                        addressStart = bestMatch;
                        addressInfo.Confidence = 85;
                    }
                    else
                    {
                        // No database match found
                        // Default: take 1-2 words before street type, but avoid business words
                        if (i > 0)
                        {
                            // Check if word before street type is a business word
                            if (_businessWords.Contains(words[i - 1].ToLower()))
                            {
                                // Street type comes right after a business word
                                // So address is just the street type and what follows
                                addressStart = i;
                            }
                            else
                            {
                                // Take 1-2 words before street type as the street name
                                addressStart = Math.Max(0, i - Math.Min(2, i));
                            }
                        }
                        else
                        {
                            // Street type is the first word
                            addressStart = 0;
                        }
                        addressInfo.Confidence = 60;
                    }
                }
                
                // Calculate character position
                int charPos = 0;
                for (int k = 0; k < addressStart; k++)
                {
                    charPos += words[k].Length + 1;
                }
                
                addressInfo.AddressStartIndex = charPos;
                return addressInfo;
            }
        }
        
        // No street type found, look for patterns with numbers
        for (int i = 0; i < words.Length; i++)
        {
            if (Regex.IsMatch(words[i], @"^\d+$"))
            {
                // Check if this is preceded by a business word
                if (i > 0 && _businessWords.Contains(words[i - 1]))
                {
                    continue; // Skip this number, it's part of business name
                }
                
                // Found a number that could start an address
                int charPos = 0;
                for (int j = 0; j < i; j++)
                {
                    charPos += words[j].Length + 1;
                }
                
                addressInfo.AddressStartIndex = charPos;
                addressInfo.Confidence = 70;
                return addressInfo;
            }
        }
        
        return addressInfo;
    }
    
    private async Task<int> FindBestStreetNameMatch(string[] words, int streetTypeIndex, string? province)
    {
        int bestMatchIndex = -1;
        int bestMatchLength = 0;
        
        // Try different street name lengths (prefer longer matches)
        // Start with longer combinations first
        for (int length = Math.Min(5, streetTypeIndex); length >= 1; length--)
        {
            int startIdx = streetTypeIndex - length;
            if (startIdx < 0) continue;
            
            // Check if the first word of potential street name is a business word
            // Special case: single common words like "Company", "ABC" shouldn't be street names by themselves
            if (length == 1)
            {
                var singleWord = words[startIdx].ToLower();
                // Skip common business words and very short words when they're alone
                if (_businessWords.Contains(singleWord) || 
                    singleWord.Length <= 3 || 
                    singleWord == "company" || 
                    singleWord == "abc" ||
                    singleWord == "services" ||
                    singleWord == "quality")
                {
                    _logger.LogDebug($"Skipping single word '{words[startIdx]}' as street name");
                    continue;
                }
            }
            else if (_businessWords.Contains(words[startIdx].ToLower()))
            {
                // For multi-word street names, skip if starts with business word
                _logger.LogDebug($"Skipping street name starting with business word: '{words[startIdx]}'");
                continue;
            }
            
            // Build the potential street name
            var streetName = string.Join(" ", words.Skip(startIdx).Take(length));
            
            // Check if this street name exists in database
            bool exists = await _streetNameService.IsKnownStreetNameAsync(streetName, province);
            
            if (exists)
            {
                // For single word matches, only accept if it's a real street-like word
                if (length == 1)
                {
                    var word = words[startIdx].ToLower();
                    // Accept single words like "mountain", "valley", "main", etc.
                    // but not "company", "abc", etc.
                    var validStreetWords = new HashSet<string> { 
                        "mountain", "valley", "main", "king", "queen", "park", 
                        "lake", "river", "forest", "hill", "ridge", "beach",
                        "market", "church", "school", "station", "bridge"
                    };
                    
                    if (!validStreetWords.Contains(word) && !word.EndsWith("view") && !word.EndsWith("wood"))
                    {
                        _logger.LogDebug($"Skipping '{word}' - not a typical street name");
                        continue;
                    }
                }
                
                // Found a match - prefer longer matches
                if (length > bestMatchLength)
                {
                    bestMatchLength = length;
                    bestMatchIndex = startIdx;
                    
                    // Log for debugging
                    _logger.LogDebug($"Found street name match: '{streetName}' (length: {length})");
                }
            }
        }
        
        // Return -1 if no good match found
        return bestMatchIndex;
    }
    
    private PhoneExtractionResult ExtractPhoneNumber(string input)
    {
        var result = new PhoneExtractionResult();
        
        // Try area code pattern first
        var areaCodeMatch = _areaCodePhonePattern.Match(input);
        if (areaCodeMatch.Success)
        {
            result.Phone = areaCodeMatch.Value.Trim();
            result.RemainingText = input.Substring(0, areaCodeMatch.Index).Trim();
            result.Success = true;
            return result;
        }
        
        // Try standard phone pattern
        var phoneMatch = _phonePattern.Match(input);
        if (phoneMatch.Success)
        {
            var phone = phoneMatch.Value.Trim();
            var remaining = input.Substring(0, phoneMatch.Index).Trim();
            
            // Check for area code before phone
            var words = remaining.Split(' ');
            if (words.Length > 0 && Regex.IsMatch(words[^1], @"^\d{3}$"))
            {
                result.Phone = $"{words[^1]} {phone}";
                result.RemainingText = string.Join(" ", words.Take(words.Length - 1));
            }
            else
            {
                result.Phone = phone;
                result.RemainingText = remaining;
            }
            
            result.Success = true;
            return result;
        }
        
        result.Success = false;
        result.ErrorMessage = "No valid phone number found";
        return result;
    }
    
    public async Task<BatchParseResult> ParseBatchAsync(List<string> inputs, string? province = null, string? areaCode = null)
    {
        var result = new BatchParseResult();
        
        foreach (var input in inputs)
        {
            var parseResult = await ParseAsync(input, province, areaCode);
            result.Results.Add(parseResult);
            
            if (parseResult.Success)
                result.SuccessCount++;
            else
                result.FailureCount++;
        }
        
        result.TotalProcessed = inputs.Count;
        return result;
    }
    
    private class PhoneExtractionResult
    {
        public bool Success { get; set; }
        public string Phone { get; set; } = string.Empty;
        public string RemainingText { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }
    }
    
    private class AddressInfo
    {
        public int AddressStartIndex { get; set; }
        public int Confidence { get; set; }
    }
}