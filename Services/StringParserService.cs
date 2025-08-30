using IsBus.Models;
using System.Text.RegularExpressions;

namespace IsBus.Services;

public interface IStringParserService
{
    Task<ParseResult> ParseAsync(string input, string? province = null, string? areaCode = null);
    Task<BatchParseResult> ParseBatchAsync(List<string> inputs, string? province = null, string? areaCode = null);
}

public class StringParserService : IStringParserService
{
    private readonly IClassificationService _classificationService;
    private readonly ICommunityService _communityService;
    private readonly IStreetTypeService _streetTypeService;
    private readonly IStreetNameService _streetNameService;
    private readonly ILogger<StringParserService> _logger;
    
    // Phone number patterns
    private readonly Regex _phonePattern = new Regex(
        @"(\d{3}[\s-]?\d{3}[\s-]?\d{4}|\d{3}[\s-]?\d{4})$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    
    // Area code + local number pattern
    private readonly Regex _areaCodePhonePattern = new Regex(
        @"(\d{3})\s+(\d{3}[\s-]?\d{4})$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    
    // Pattern for detecting numbers that might be part of addresses
    private readonly Regex _numberPattern = new Regex(
        @"\b\d+\b",
        RegexOptions.Compiled);
    
    // Highway/Route indicators
    private readonly HashSet<string> _highwayIndicators = new(StringComparer.OrdinalIgnoreCase)
    {
        "highway", "hwy", "route", "rte", "rt", "road", "rd", 
        "street", "st", "avenue", "ave", "lane", "ln", "drive", "dr",
        "boulevard", "blvd", "parkway", "pkwy", "place", "pl",
        "court", "ct", "circle", "cir", "trail", "tr"
    };
    
    // Location keywords that indicate an address
    private readonly HashSet<string> _locationKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "island", "beach", "bay", "point", "cove", "harbour", "harbor",
        "river", "lake", "mountain", "hill", "valley", "ridge",
        "village", "town", "city", "settlement", "junction", "crossing"
    };
    
    // Business names with numbers that should not be treated as addresses
    private readonly HashSet<string> _businessNamesWithNumbers = new(StringComparer.OrdinalIgnoreCase)
    {
        "super 8", "motel 6", "century 21", "7-eleven", "7 eleven",
        "24 hour", "365", "studio 54", "pier 1", "forever 21"
    };
    
    public StringParserService(
        IClassificationService classificationService,
        ICommunityService communityService,
        IStreetTypeService streetTypeService,
        IStreetNameService streetNameService,
        ILogger<StringParserService> logger)
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
        
        // Normalize input (remove extra spaces)
        input = Regex.Replace(input.Trim(), @"\s+", " ");
        
        // Step 1: Extract phone number from the end
        var phoneExtraction = ExtractPhoneNumber(input);
        if (!phoneExtraction.Success)
        {
            result.Success = false;
            result.ErrorMessage = phoneExtraction.ErrorMessage;
            return result;
        }
        
        result.Phone = phoneExtraction.Phone;
        var remainingText = phoneExtraction.RemainingText.Trim();
        
        // Step 2: Find where the address starts (if any)
        var addressStartIndex = await FindAddressStartAsync(remainingText, province);
        
        // Step 3: Split name and address
        if (addressStartIndex > 0)
        {
            result.Name = remainingText.Substring(0, addressStartIndex).Trim();
            result.Address = remainingText.Substring(addressStartIndex).Trim();
        }
        else
        {
            // Check if the entire remaining text might be an address (location name)
            if (ContainsLocationKeywords(remainingText))
            {
                // It's likely just a location name with no person/business name
                result.Address = remainingText;
            }
            else
            {
                // No address found, entire remaining text is the name
                result.Name = remainingText;
            }
        }
        
        // Step 4: Check if the end of the name is actually a community name
        if (!string.IsNullOrWhiteSpace(result.Name) && string.IsNullOrWhiteSpace(result.Address))
        {
            var nameWords = result.Name.Split(' ');
            if (nameWords.Length > 1)
            {
                // Check last word
                var lastWord = nameWords[nameWords.Length - 1];
                if (await _communityService.IsCommunityNameAsync(lastWord, province))
                {
                    // Move the community name to address
                    result.Address = lastWord;
                    result.Name = string.Join(" ", nameWords.Take(nameWords.Length - 1));
                    result.Confidence.AddressConfidence = 90;
                    result.Confidence.Notes = $"Detected community name: {lastWord}";
                }
                else if (nameWords.Length > 2)
                {
                    // Check last two words (for multi-word community names)
                    var lastTwoWords = $"{nameWords[nameWords.Length - 2]} {nameWords[nameWords.Length - 1]}";
                    if (await _communityService.IsCommunityNameAsync(lastTwoWords, province))
                    {
                        result.Address = lastTwoWords;
                        result.Name = string.Join(" ", nameWords.Take(nameWords.Length - 2));
                        result.Confidence.AddressConfidence = 90;
                        result.Confidence.Notes = $"Detected community name: {lastTwoWords}";
                    }
                }
            }
        }
        
        // Step 5: Classify the name if we have one
        if (!string.IsNullOrWhiteSpace(result.Name))
        {
            var classification = await _classificationService.ClassifyAsync(result.Name);
            result.IsBusinessName = classification.IsBusiness;
            result.IsResidentialName = classification.IsResidential;
            
            result.Confidence.NameConfidence = classification.Confidence;
        }
        
        // Set confidence scores
        result.Confidence.PhoneConfidence = 100; // We're strict about phone format
        if (result.Confidence.AddressConfidence == 0)
        {
            result.Confidence.AddressConfidence = addressStartIndex > 0 ? 80 : 50;
        }
        
        // Record street name if we found one
        if (!string.IsNullOrWhiteSpace(result.Address))
        {
            await RecordStreetNameFromAddress(result.Address, province);
        }
        
        result.Success = true;
        
        _logger.LogInformation($"Parsed: '{input}' -> Name: '{result.Name}', Address: '{result.Address}', Phone: '{result.Phone}'");
        
        return result;
    }
    
    private PhoneExtractionResult ExtractPhoneNumber(string input)
    {
        var result = new PhoneExtractionResult();
        
        // Try area code pattern first (e.g., "431 570-2868")
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
            
            // Check if there's a number right before the phone that might be area code
            // e.g., "John Smith 125 Spruce St 103 555-5555"
            var words = remaining.Split(' ');
            if (words.Length > 0)
            {
                var lastWord = words[words.Length - 1];
                if (Regex.IsMatch(lastWord, @"^\d{3}$"))
                {
                    // Check if this 3-digit number is after a highway indicator
                    bool isHighwayNumber = false;
                    if (words.Length > 1)
                    {
                        var precedingWord = words[words.Length - 2];
                        isHighwayNumber = _highwayIndicators.Any(h => 
                            precedingWord.Equals(h, StringComparison.OrdinalIgnoreCase));
                    }
                    
                    if (!isHighwayNumber)
                    {
                        // It's likely an area code
                        result.Phone = $"{lastWord} {phone}";
                        result.RemainingText = string.Join(" ", words.Take(words.Length - 1));
                        result.Success = true;
                        return result;
                    }
                }
            }
            
            result.Phone = phone;
            result.RemainingText = remaining;
            result.Success = true;
            return result;
        }
        
        result.Success = false;
        result.ErrorMessage = "No valid phone number found at the end of the input";
        return result;
    }
    
    private async Task<int> FindAddressStartAsync(string text, string? province)
    {
        // Check for business names with numbers that shouldn't be treated as addresses
        foreach (var businessName in _businessNamesWithNumbers)
        {
            if (text.Contains(businessName, StringComparison.OrdinalIgnoreCase))
            {
                // This business name contains numbers but isn't an address
                return -1;
            }
        }
        
        // Check for numbers in parentheses, brackets, etc. (these are not addresses)
        var groupedNumberPattern = @"[\(\[\{]\d+[\)\]\}]";
        text = Regex.Replace(text, groupedNumberPattern, "");
        
        var words = text.Split(' ');
        
        // First, check if there's a street type in the text
        if (_streetTypeService.ContainsStreetType(text, out var streetType, out var streetTypePos))
        {
            // Found a street type, look for where the street name starts
            var streetTypeWordIndex = 0;
            int currentPos = 0;
            for (int i = 0; i < words.Length; i++)
            {
                if (currentPos >= streetTypePos)
                {
                    streetTypeWordIndex = i;
                    break;
                }
                currentPos += words[i].Length + 1;
            }
            
            // Intelligently determine the start of the street name
            int startIndex = streetTypeWordIndex;
            
            // Check if there's a civic number (potentially with unit number)
            bool hasCivicNumber = false;
            int civicNumberIndex = -1;
            
            // Look backwards from street type for numbers
            for (int i = streetTypeWordIndex - 1; i >= 0; i--)
            {
                if (Regex.IsMatch(words[i], @"^\d+$"))
                {
                    // Found a number - but check if there's another number before it (unit number)
                    if (i > 0 && Regex.IsMatch(words[i - 1], @"^\d+$"))
                    {
                        // Two consecutive numbers - likely unit number followed by civic number
                        // e.g., "223 1057 Beaverhill Blvd" where 223 is unit, 1057 is civic
                        startIndex = i - 1; // Start from the first number (unit)
                        hasCivicNumber = true;
                        civicNumberIndex = i - 1;
                        break;
                    }
                    else
                    {
                        // Single number - likely just civic number
                        startIndex = i;
                        hasCivicNumber = true;
                        civicNumberIndex = i;
                        break;
                    }
                }
            }
            
            if (!hasCivicNumber && streetTypeWordIndex > 0)
            {
                // No civic number, need to find where street name starts
                // Check for all possible street name lengths and use the LONGEST match
                int longestMatchIndex = -1;
                int longestMatchLength = 0;
                bool foundBusinessBoundary = false;
                int businessBoundaryIndex = -1;
                
                // Look for the longest possible street name (up to 5 words before street type)
                for (int lookBack = Math.Min(5, streetTypeWordIndex); lookBack >= 1; lookBack--)
                {
                    int testIndex = streetTypeWordIndex - lookBack;
                    if (testIndex < 0) continue;
                    
                    // Build potential street name
                    var potentialStreetName = string.Join(" ", 
                        words.Skip(testIndex).Take(streetTypeWordIndex - testIndex));
                    
                    // Check if this is a known street name
                    bool isKnownStreet = await _streetNameService.IsKnownStreetNameAsync(potentialStreetName, province);
                    
                    // Also check with the street type included for better matching
                    var fullStreetName = potentialStreetName + " " + streetType;
                    bool isKnownFullStreet = await _streetNameService.IsKnownStreetNameAsync(fullStreetName, province);
                    
                    if (isKnownStreet || isKnownFullStreet)
                    {
                        // Before accepting this match, check if the word before it is a business word
                        bool skipThisMatch = false;
                        if (testIndex > 0)
                        {
                            var wordBefore = words[testIndex - 1];
                            if (IsLikelyBusinessWord(wordBefore))
                            {
                                // This street name starts right after a business word
                                // Mark as potential boundary but keep looking
                                if (!foundBusinessBoundary)
                                {
                                    foundBusinessBoundary = true;
                                    businessBoundaryIndex = testIndex;
                                }
                                // Don't skip if this is a much longer match
                                if (lookBack <= longestMatchLength + 1)
                                {
                                    skipThisMatch = true;
                                }
                            }
                        }
                        
                        if (!skipThisMatch)
                        {
                            // Found a match - prefer longer matches
                            // For "Indian Mountain Road", this will prefer the 2-word match over 1-word
                            if (lookBack > longestMatchLength)
                            {
                                longestMatchLength = lookBack;
                                longestMatchIndex = testIndex;
                            }
                        }
                    }
                    
                    // Check if the word before this looks like part of a business name
                    if (testIndex > 0 && !foundBusinessBoundary)
                    {
                        var wordBefore = words[testIndex - 1];
                        if (IsLikelyBusinessWord(wordBefore))
                        {
                            foundBusinessBoundary = true;
                            businessBoundaryIndex = testIndex;
                        }
                    }
                }
                
                // Use the longest match if we found one
                if (longestMatchIndex >= 0)
                {
                    startIndex = longestMatchIndex;
                }
                // If we found a business boundary and no street name match, use the boundary
                else if (foundBusinessBoundary)
                {
                    startIndex = businessBoundaryIndex;
                }
                // Default to maximum 2 words before street type if no match found
                else if (startIndex == streetTypeWordIndex && streetTypeWordIndex > 0)
                {
                    startIndex = Math.Max(0, streetTypeWordIndex - 2);
                }
            }
            
            // Calculate the character position
            int position = 0;
            for (int j = 0; j < startIndex; j++)
            {
                position += words[j].Length + 1;
            }
            return position;
        }
        
        // Original logic for finding addresses with numbers
        for (int i = 0; i < words.Length; i++)
        {
            var word = words[i];
            
            // Check if this word is a number
            if (Regex.IsMatch(word, @"^\d+$"))
            {
                // Found a number, this might be the start of an address
                // But first check if there's a second number right after (unit + civic pattern)
                if (i + 1 < words.Length && Regex.IsMatch(words[i + 1], @"^\d+$"))
                {
                    // Two consecutive numbers at this position
                    // This is likely unit number + civic number (e.g., "223 1057")
                    // The address starts at the first number
                }
                
                // Calculate the position in the original string
                int position = 0;
                for (int j = 0; j < i; j++)
                {
                    position += words[j].Length + 1; // +1 for space
                }
                return position;
            }
            
            // Check for location keywords that might indicate address without number
            if (_locationKeywords.Contains(word))
            {
                // Check if this is the last word or two in the name part
                // (location keywords at the end are likely addresses)
                if (i > 0 && i >= words.Length - 2)
                {
                    int position = 0;
                    for (int j = 0; j < i; j++)
                    {
                        position += words[j].Length + 1;
                    }
                    return position;
                }
            }
        }
        
        return -1; // No address found
    }
    
    // Synchronous wrapper for backward compatibility
    private int FindAddressStart(string text)
    {
        return FindAddressStartAsync(text, null).GetAwaiter().GetResult();
    }
    
    private bool ContainsLocationKeywords(string text)
    {
        var words = text.Split(' ');
        return words.Any(w => _locationKeywords.Contains(w));
    }
    
    private bool IsLikelyBusinessWord(string word)
    {
        if (string.IsNullOrWhiteSpace(word))
            return false;
            
        // Common business-related words that wouldn't be part of a street name
        var businessWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
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
        
        return businessWords.Contains(word.ToLower());
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
    
    private async Task RecordStreetNameFromAddress(string address, string? province)
    {
        try
        {
            // Extract street name and type from address
            if (_streetTypeService.ContainsStreetType(address, out var streetType, out var position))
            {
                // Get the words before the street type
                var beforeStreetType = address.Substring(0, position).Trim();
                var words = beforeStreetType.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                
                if (words.Length > 0)
                {
                    // Skip civic number if present
                    int startIndex = 0;
                    if (Regex.IsMatch(words[0], @"^\d+"))
                    {
                        startIndex = 1;
                    }
                    
                    if (startIndex < words.Length)
                    {
                        var streetName = string.Join(" ", words.Skip(startIndex));
                        if (!string.IsNullOrWhiteSpace(streetName))
                        {
                            await _streetNameService.RecordStreetNameAsync(streetName, streetType, province);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording street name from address: {Address}", address);
        }
    }
    
    private class PhoneExtractionResult
    {
        public bool Success { get; set; }
        public string Phone { get; set; } = string.Empty;
        public string RemainingText { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }
    }
}