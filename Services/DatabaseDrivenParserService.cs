using IsBus.Models;
using IsBus.Data;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

namespace IsBus.Services;

/// <summary>
/// Parser that relies entirely on the database to find the best street match
/// Looks for the longest matching street name that exists in the database
/// </summary>
public class DatabaseDrivenParserService : IStringParserService
{
    private readonly IClassificationService _classificationService;
    private readonly ICommunityService _communityService;
    private readonly IStreetTypeService _streetTypeService;
    private readonly IStreetNameService _streetNameService;
    private readonly PhonebookContext _context;
    private readonly ILogger<DatabaseDrivenParserService> _logger;
    
    // Phone number patterns
    private readonly Regex _phonePattern = new Regex(
        @"(\d{3}[\s-]?\d{3}[\s-]?\d{4}|\d{3}[\s-]?\d{4})$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    
    private readonly Regex _areaCodePhonePattern = new Regex(
        @"(\d{3})\s+(\d{3}[\s-]?\d{4})$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    
    public DatabaseDrivenParserService(
        IClassificationService classificationService,
        ICommunityService communityService,
        IStreetTypeService streetTypeService,
        IStreetNameService streetNameService,
        PhonebookContext context,
        ILogger<DatabaseDrivenParserService> logger)
    {
        _classificationService = classificationService;
        _communityService = communityService;
        _streetTypeService = streetTypeService;
        _streetNameService = streetNameService;
        _context = context;
        _logger = logger;
    }
    
    public async Task<ParseResult> ParseAsync(string input, string? province = null)
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
        
        // Check if this looks like a phonebook entry (personal name format)
        // Pattern: [LastName] [FirstName/Initial] [Address]
        var phonebookParse = await ParsePhonebookFormatAsync(remainingText);
        if (phonebookParse.IsPhonebook)
        {
            result.Name = phonebookParse.Name;
            result.Address = phonebookParse.Address;
            result.Confidence.AddressConfidence = phonebookParse.AddressConfidence;
            
            // Classify the name
            if (!string.IsNullOrWhiteSpace(result.Name))
            {
                var classification = await _classificationService.ClassifyAsync(result.Name);
                result.IsBusinessName = classification.IsBusiness;
                result.IsResidentialName = classification.IsResidential;
                result.Confidence.NameConfidence = classification.Confidence;
            }
            
            result.Confidence.PhoneConfidence = 100;
            result.Success = true;
            
            _logger.LogInformation($"Parsed as phonebook entry: '{input}' -> Name: '{result.Name}', Address: '{result.Address}', Phone: '{result.Phone}'");
            
            return result;
        }
        
        // Step 2: Find the best street match using database
        var streetMatch = await FindBestStreetMatch(remainingText, province);
        
        if (streetMatch.Found)
        {
            // We found a street in the database
            // The name is everything before the street starts
            if (streetMatch.StartIndex > 0)
            {
                result.Name = remainingText.Substring(0, streetMatch.StartIndex).Trim();
            }
            else
            {
                result.Name = "";
            }
            
            // The address is from the street start to the end
            result.Address = remainingText.Substring(streetMatch.StartIndex).Trim();
            result.Confidence.AddressConfidence = streetMatch.Confidence;
            
            _logger.LogInformation($"Found street '{streetMatch.StreetName}' at position {streetMatch.StartIndex}");
        }
        else
        {
            // No street found - check for community names or fall back to looking for numbers
            var communityMatch = await FindCommunityMatch(remainingText, province);
            if (communityMatch.Found)
            {
                // Found a community name
                if (communityMatch.StartIndex > 0)
                {
                    result.Name = remainingText.Substring(0, communityMatch.StartIndex).Trim();
                }
                else
                {
                    result.Name = "";
                }
                result.Address = remainingText.Substring(communityMatch.StartIndex).Trim();
                result.Confidence.AddressConfidence = 70;
                
                _logger.LogInformation($"Found community '{communityMatch.CommunityName}' at position {communityMatch.StartIndex}");
            }
            else
            {
                // No community found either - fall back to looking for numbers
                var addressStart = FindFirstNumber(remainingText);
                if (addressStart >= 0)
                {
                    result.Name = remainingText.Substring(0, addressStart).Trim();
                    result.Address = remainingText.Substring(addressStart).Trim();
                    result.Confidence.AddressConfidence = 50;
                }
                else
                {
                    // No address indicators found
                    result.Name = remainingText;
                    result.Address = "";
                    result.Confidence.AddressConfidence = 0;
                }
            }
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
    
    private async Task<StreetMatch> FindBestStreetMatch(string text, string? province)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var bestMatch = new StreetMatch { Found = false };
        
        // First, find ALL street type positions in the text
        // But exclude province codes that might look like street types
        var streetTypePositions = new List<int>();
        var provinceAbbreviations = new HashSet<string> { "AB", "BC", "MB", "NB", "NL", "NS", "NT", "NU", "ON", "PE", "QC", "SK", "YT" };
        
        for (int i = 0; i < words.Length; i++)
        {
            var word = words[i].Trim('.', ',');
            
            // Skip if this looks like a province code
            if (provinceAbbreviations.Contains(word.ToUpper()))
            {
                continue;
            }
            
            if (_streetTypeService.IsStreetType(word))
            {
                streetTypePositions.Add(i);
                _logger.LogInformation($"Found street type '{word}' at position {i}");
            }
        }
        
        // Process street types from rightmost to leftmost
        // This ensures we prefer "Road" over "Mountain" in "Mountain Road"
        foreach (int i in streetTypePositions.OrderByDescending(p => p))
        {
            var streetType = words[i].Trim('.', ',');
            _logger.LogInformation($"Processing street type '{streetType}' at position {i}");
            
            if (i == 0)
            {
                // Street type is the first word, no street name before it
                _logger.LogInformation($"Street type '{streetType}' is at the beginning, no street name before it");
                continue;
            }
            
            // Start with one word before the street type and work backwards
            string longestValidStreet = "";
            int longestValidStart = i;
            
            for (int startIdx = i - 1; startIdx >= 0; startIdx--)
            {
                // Build the street name from startIdx to just before the street type
                var streetNameWithoutType = string.Join(" ", words.Skip(startIdx).Take(i - startIdx));
                
                _logger.LogInformation($"Checking if '{streetNameWithoutType}' exists in database for province '{province}'...");
                
                bool exists = await _streetNameService.IsKnownStreetNameAsync(streetNameWithoutType, province);
                
                _logger.LogInformation($"Result for '{streetNameWithoutType}': {exists}");
                
                if (exists)
                {
                    // This combination exists, so update our longest valid street
                    longestValidStreet = streetNameWithoutType;
                    longestValidStart = startIdx;
                    // Continue checking to see if we can find an even longer match
                }
                else
                {
                    // This combination doesn't exist, so stop checking backwards
                    break;
                }
            }
            
            if (!string.IsNullOrEmpty(longestValidStreet))
            {
                // We found a valid street name
                // Check if there's a civic/unit number before it
                int addressStart = longestValidStart;
                
                // Look for numbers before the street name
                if (longestValidStart > 0)
                {
                    // Check for civic number (and possibly unit number)
                    for (int j = longestValidStart - 1; j >= 0; j--)
                    {
                        if (Regex.IsMatch(words[j], @"^\d+$"))
                        {
                            addressStart = j;
                            // Check if there's another number before this (unit number)
                            if (j > 0 && Regex.IsMatch(words[j - 1], @"^\d+$"))
                            {
                                addressStart = j - 1;
                            }
                            break;
                        }
                        else
                        {
                            // No more numbers, stop looking
                            break;
                        }
                    }
                }
                
                // Calculate character position
                int charPos = 0;
                for (int k = 0; k < addressStart; k++)
                {
                    charPos += words[k].Length + 1;
                }
                
                // Update best match if this is better (prefer rightmost street type)
                if (!bestMatch.Found || longestValidStart > bestMatch.StartIndex)
                {
                    bestMatch.Found = true;
                    bestMatch.StreetName = longestValidStreet;
                    bestMatch.StartIndex = charPos;
                    bestMatch.Confidence = 90;
                    bestMatch.Length = longestValidStreet.Split(' ').Length;
                    
                    _logger.LogInformation($"Found longest valid street: '{longestValidStreet}' {streetType} at position {charPos}");
                    
                    // Return immediately - we found the best match for the rightmost street type
                    return bestMatch;
                }
            }
            else
            {
                _logger.LogInformation($"No valid street name found before street type '{streetType}'");
            }
        }
        
        return bestMatch;
    }
    
    private async Task<PhonebookParseResult> ParsePhonebookFormatAsync(string text)
    {
        var result = new PhonebookParseResult { IsPhonebook = false };
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        if (words.Length < 1)
        {
            return result;
        }
        
        // Look for clear address indicators to determine where the name ends
        int addressStartIndex = -1;
        
        // Common address start patterns:
        // 1. Starts with a number (civic address): "123 Main St"
        // 2. Starts with "Unit", "Apt", "Suite": "Unit 7 1777 Pembina"
        // 3. Contains street types after potential name words
        
        for (int i = 1; i < words.Length; i++)
        {
            var word = words[i];
            var prevWord = i > 0 ? words[i - 1] : "";
            
            // Check if this word indicates the start of an address
            bool isNumber = Regex.IsMatch(word, @"^\d+$");
            bool isUnit = Regex.IsMatch(word, @"^(Unit|Apt|Suite|Room|Rm)$", RegexOptions.IgnoreCase);
            bool isStreetType = _streetTypeService.IsStreetType(word);
            
            // Check if previous word was an ampersand (for names like "M & L")
            bool prevWasAmpersand = prevWord == "&";
            
            if (isNumber || isUnit)
            {
                // Definite address start
                addressStartIndex = i;
                break;
            }
            else if (isStreetType && i > 1)  // Street type after at least 2 words (potential name)
            {
                // This might be a street type in the address
                // But check if the previous word could be part of a name
                if (!prevWasAmpersand)
                {
                    addressStartIndex = i - 1; // The word before the street type starts the address
                    break;
                }
            }
        }
        
        // If no clear address indicators found, use database to identify names
        if (addressStartIndex == -1)
        {
            // Special case: Check if this might be just a name with no address
            // (e.g., "Aguila John 774-1957" or "Smith John 555-1234")
            // In phonebook format, if there are only 2-3 non-phone words, they're likely all part of the name
            
            // Count non-phone words
            int nonPhoneWordCount = 0;
            for (int i = 0; i < words.Length; i++)
            {
                if (!Regex.IsMatch(words[i], @"^\d{3}-?\d{4}$"))
                {
                    nonPhoneWordCount++;
                }
            }
            
            // If we have 2-3 words before phone and no clear address indicators, assume it's all name
            if (nonPhoneWordCount <= 3 && nonPhoneWordCount >= 2)
            {
                // Check if the last word or two could be a phone number
                bool hasPhone = false;
                for (int i = words.Length - 1; i >= 0 && i >= words.Length - 2; i--)
                {
                    if (Regex.IsMatch(words[i], @"^\d{3}-?\d{4}$"))
                    {
                        hasPhone = true;
                        break;
                    }
                }
                
                if (hasPhone)
                {
                    // All non-phone words are the name, no address
                    addressStartIndex = nonPhoneWordCount;
                    _logger.LogDebug($"Detected name-only format with {nonPhoneWordCount} name words");
                }
            }
            
            // If still no determination, check for patterns that suggest a name using word_data table
            if (addressStartIndex == -1)
            {
                bool hasInitial = false;
                bool hasAmpersand = false;
                int lastNamePartIndex = 0;
                int consecutiveNameWords = 0;
                bool firstWordIsLastName = false;
                
                // Check if first word is a known last name
                if (words.Length > 0)
                {
                    var firstWordLower = words[0].ToLower().Trim('.', ',');
                    var firstWordData = await _context.Set<WordData>()
                        .Where(w => w.WordLower == firstWordLower && (w.WordType == "last" || w.WordType == "both"))
                        .FirstOrDefaultAsync();
                    
                    if (firstWordData != null)
                    {
                        firstWordIsLastName = true;
                        _logger.LogDebug($"First word '{firstWordLower}' is a known last name");
                    }
                }
                
                for (int i = 0; i < Math.Min(words.Length, 5); i++) // Check first 5 words max for name
            {
                var word = words[i];
                var wordLower = word.ToLower().Trim('.', ',');
                
                // Is this an initial?
                if (word.Length == 1 && char.IsLetter(word[0]))
                {
                    hasInitial = true;
                    lastNamePartIndex = i;
                    consecutiveNameWords++;
                }
                // Is this an ampersand?
                else if (word == "&")
                {
                    hasAmpersand = true;
                    lastNamePartIndex = i;
                    consecutiveNameWords++;
                }
                // Check if this word is in our name database
                else if (!Regex.IsMatch(word, @"^\d+$"))
                {
                    // Query word_data table to check if this is a name
                    var wordData = await _context.Set<WordData>()
                        .Where(w => w.WordLower == wordLower)
                        .ToListAsync();
                    
                    bool isLikelyName = false;
                    if (wordData.Any())
                    {
                        // Check if this word is primarily a name (first, last, or both)
                        var nameEntry = wordData.FirstOrDefault(w => 
                            w.WordType == "first" || 
                            w.WordType == "last" || 
                            w.WordType == "both");
                        
                        var businessEntry = wordData.FirstOrDefault(w => w.WordType == "business");
                        
                        if (nameEntry != null && businessEntry != null)
                        {
                            // Compare counts - if name count is higher, treat as name
                            isLikelyName = nameEntry.WordCount >= businessEntry.WordCount;
                        }
                        else if (nameEntry != null)
                        {
                            isLikelyName = true;
                        }
                        
                        _logger.LogDebug($"Word '{wordLower}' database check: " +
                            $"name={nameEntry?.WordCount ?? 0}, " +
                            $"business={businessEntry?.WordCount ?? 0}, " +
                            $"isLikelyName={isLikelyName}");
                    }
                    else if (word.Length <= 15 && char.IsUpper(word[0]))
                    {
                        // Not in database but looks like a proper name (capitalized, reasonable length)
                        isLikelyName = true;
                    }
                    
                    if (isLikelyName || firstWordIsLastName)
                    {
                        lastNamePartIndex = i;
                        consecutiveNameWords++;
                        
                        // If we have an ampersand, include the next word too
                        if (hasAmpersand && i < words.Length - 1)
                        {
                            lastNamePartIndex = i;
                        }
                    }
                    else if (!firstWordIsLastName)
                    {
                        // This doesn't look like part of a name, stop here
                        // But only stop if the first word isn't a known last name
                        break;
                    }
                    else
                    {
                        // First word is a last name, so be more lenient about including following words
                        lastNamePartIndex = i;
                        consecutiveNameWords++;
                    }
                }
                else
                {
                    // This is a number, not part of a name
                    break;
                }
            }
            
                // If we found name patterns, set the address start after them
                if (firstWordIsLastName)
                {
                    // If first word is a known last name, include all following name-like words
                    // (initials, names, ampersands) until we hit an address indicator
                    addressStartIndex = lastNamePartIndex + 1;
                    if (addressStartIndex < 2 && words.Length >= 2)
                    {
                        // At minimum, include first + second word when first is a last name
                        addressStartIndex = 2;
                    }
                }
                else if (hasInitial || consecutiveNameWords > 0)
                {
                    addressStartIndex = lastNamePartIndex + 1;
                }
                else if (words.Length >= 2)
                {
                    // Default: assume first two words are the name
                    addressStartIndex = 2;
                }
                else
                {
                    // Only one word - it's the name
                    addressStartIndex = 1;
                }
            }
        }
        
        // Build the name (everything before address start)
        var nameParts = new List<string>();
        for (int i = 0; i < addressStartIndex && i < words.Length; i++)
        {
            nameParts.Add(words[i]);
        }
        result.Name = string.Join(" ", nameParts);
        
        // Build the address (everything from address start)
        var addressParts = new List<string>();
        for (int i = addressStartIndex; i < words.Length; i++)
        {
            addressParts.Add(words[i]);
        }
        result.Address = string.Join(" ", addressParts);
        
        // Mark as phonebook entry if we have a name
        if (!string.IsNullOrWhiteSpace(result.Name))
        {
            result.IsPhonebook = true;
            
            // Set confidence based on what we found
            if (addressStartIndex > 0 && addressParts.Count > 0 && Regex.IsMatch(addressParts[0], @"^\d+$"))
            {
                result.AddressConfidence = 90; // High confidence - clear address number
            }
            else if (addressParts.Count > 0 && Regex.IsMatch(addressParts[0], @"^(Unit|Apt|Suite)$", RegexOptions.IgnoreCase))
            {
                result.AddressConfidence = 85; // High confidence - clear unit indicator
            }
            else
            {
                result.AddressConfidence = 70; // Medium confidence - heuristic based
            }
        }
        
        return result;
    }
    
    private async Task<CommunityMatch> FindCommunityMatch(string text, string? province)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var match = new CommunityMatch { Found = false };
        
        // Common words that should never be considered as community names
        var skipWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase) 
        { 
            "and", "the", "of", "for", "to", "at", "in", "on", "by", "with", 
            "from", "or", "but", "not", "all", "can", "her", "was", "one", 
            "our", "out", "his", "has", "had", "were", "been", "have", "their",
            "a", "an", "as", "are", "is", "it", "its", "be", "been", "being",
            "administration", "sales", "services", "management", "consulting",
            "solutions", "systems", "technologies", "enterprises", "industries"
        };
        
        // Check each word or combination to see if it's a known community
        for (int i = 0; i < words.Length; i++)
        {
            // Try single word first
            var word = words[i].Trim('.', ',');
            
            // Skip common words
            if (skipWords.Contains(word))
            {
                _logger.LogInformation($"Skipping common word '{word}'");
                continue;
            }
            
            _logger.LogInformation($"Checking if '{word}' is a known community...");
            
            if (await _communityService.IsCommunityNameAsync(word, province))
            {
                // Calculate character position
                int charPos = 0;
                for (int j = 0; j < i; j++)
                {
                    charPos += words[j].Length + 1;
                }
                
                match.Found = true;
                match.CommunityName = word;
                match.StartIndex = charPos;
                
                _logger.LogInformation($"Found community '{word}' at position {charPos}");
                return match;
            }
            
            // Try two-word combinations (like "Saint John")
            if (i < words.Length - 1)
            {
                var twoWords = $"{words[i]} {words[i + 1]}".Trim('.', ',');
                
                if (await _communityService.IsCommunityNameAsync(twoWords, province))
                {
                    // Calculate character position
                    int charPos = 0;
                    for (int j = 0; j < i; j++)
                    {
                        charPos += words[j].Length + 1;
                    }
                    
                    match.Found = true;
                    match.CommunityName = twoWords;
                    match.StartIndex = charPos;
                    
                    _logger.LogInformation($"Found community '{twoWords}' at position {charPos}");
                    return match;
                }
            }
        }
        
        return match;
    }
    
    private int FindFirstNumber(string text)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        for (int i = 0; i < words.Length; i++)
        {
            if (Regex.IsMatch(words[i], @"^\d+$"))
            {
                // Calculate character position
                int charPos = 0;
                for (int j = 0; j < i; j++)
                {
                    charPos += words[j].Length + 1;
                }
                return charPos;
            }
        }
        
        return -1;
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
    
    public async Task<BatchParseResult> ParseBatchAsync(List<string> inputs, string? province = null)
    {
        var result = new BatchParseResult();
        
        foreach (var input in inputs)
        {
            var parseResult = await ParseAsync(input, province);
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
    
    private class StreetMatch
    {
        public bool Found { get; set; }
        public string StreetName { get; set; } = string.Empty;
        public int StartIndex { get; set; }
        public int Confidence { get; set; }
        public int Length { get; set; }
    }
    
    private class CommunityMatch
    {
        public bool Found { get; set; }
        public string CommunityName { get; set; } = string.Empty;
        public int StartIndex { get; set; }
    }
    
    private class PhonebookParseResult
    {
        public bool IsPhonebook { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public int AddressConfidence { get; set; }
    }
}