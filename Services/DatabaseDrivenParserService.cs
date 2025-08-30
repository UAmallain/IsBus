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
    private readonly IBusinessWordService _businessWordService;
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
        IBusinessWordService businessWordService,
        PhonebookContext context,
        ILogger<DatabaseDrivenParserService> logger)
    {
        _classificationService = classificationService;
        _communityService = communityService;
        _streetTypeService = streetTypeService;
        _streetNameService = streetNameService;
        _businessWordService = businessWordService;
        _context = context;
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
        
        // Special case: Remove "Composez sans frais / Call no charge 1" if present anywhere in the input
        // Use regex to handle variations in spacing and punctuation
        var tollFreePattern = @"Composez\s+sans\s+frais\s*/\s*Call\s+no\s+charge\s*\.?\s*1";
        var tollFreeMatch = Regex.Match(input, tollFreePattern, RegexOptions.IgnoreCase);
        if (tollFreeMatch.Success)
        {
            // Remove the toll-free text from wherever it appears
            input = input.Remove(tollFreeMatch.Index, tollFreeMatch.Length).Trim();
            _logger.LogDebug($"Removed toll-free text, remaining: '{input}'");
        }
        
        // Normalize input - remove underscores and collapse multiple spaces
        input = input.Replace('_', ' '); // Replace underscores with spaces
        input = Regex.Replace(input.Trim(), @"\s+", " "); // Collapse multiple spaces to single space
        
        // Step 1: Extract phone number
        var phoneExtraction = ExtractPhoneNumber(input, areaCode);
        if (!phoneExtraction.Success)
        {
            result.Success = false;
            result.ErrorMessage = phoneExtraction.ErrorMessage;
            return result;
        }
        
        result.Phone = phoneExtraction.Phone;
        var remainingText = phoneExtraction.RemainingText.Trim();
        
        // Don't do preliminary classification on the full text as it includes the address
        // We'll classify after extracting the address
        bool isLikelyBusiness = false;
        
        // Special cases that are always businesses
        bool forceAsBusiness = false;
        var wordsForCheck = remainingText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        // Check for residential name patterns with initials
        // This should NOT be forced as business even if it starts with "A"
        bool looksLikeResidentialWithInitials = false;
        string residentialInitialPattern = "";
        
        if (wordsForCheck.Length == 3)
        {
            // Check if first and last are single letters (initials)
            bool firstIsInitial = wordsForCheck[0].Length <= 2 && wordsForCheck[0].All(c => char.IsLetter(c) || c == '.');
            bool lastIsInitial = wordsForCheck[2].Length <= 2 && wordsForCheck[2].All(c => char.IsLetter(c) || c == '.');
            bool middleIsName = wordsForCheck[1].Length > 2 && char.IsLetter(wordsForCheck[1][0]);
            
            // Also check for "Initial Initial Surname" pattern
            bool secondIsInitial = wordsForCheck[1].Length <= 2 && wordsForCheck[1].All(c => char.IsLetter(c) || c == '.');
            bool lastIsName = wordsForCheck[2].Length > 2 && char.IsLetter(wordsForCheck[2][0]);
            
            if (firstIsInitial && lastIsInitial && middleIsName)
            {
                looksLikeResidentialWithInitials = true;
                residentialInitialPattern = "initial-surname-initial";
                _logger.LogDebug($"Detected residential pattern 'initial surname initial': {remainingText}");
            }
            else if (firstIsInitial && secondIsInitial && lastIsName)
            {
                looksLikeResidentialWithInitials = true;
                residentialInitialPattern = "initial-initial-surname";
                _logger.LogDebug($"Detected residential pattern 'initial initial surname': {remainingText}");
            }
        }
        
        // Use BusinessWordService to analyze the phrase for business indicators
        var businessAnalysis = await _businessWordService.AnalyzePhraseAsync(remainingText);
        
        // Check if we have strong business indicators
        bool hasStrongBusinessWords = businessAnalysis.isBusiness && 
                                      businessAnalysis.maxStrength >= BusinessIndicatorStrength.Strong;
        
        if (hasStrongBusinessWords)
        {
            forceAsBusiness = true;
            isLikelyBusiness = true;
            looksLikeResidentialWithInitials = false; // Override any residential pattern
            _logger.LogDebug($"Business analysis: {businessAnalysis.reason}");
        }
        
        // Also check for corporate suffixes which are absolute indicators
        foreach (var word in wordsForCheck)
        {
            if (await _businessWordService.IsCorporateSuffixAsync(word))
            {
                forceAsBusiness = true;
                isLikelyBusiness = true;
                looksLikeResidentialWithInitials = false;
                _logger.LogDebug($"Found corporate suffix '{word}', forcing as business");
                break;
            }
        }
        
        // "A 1" or "A-1" patterns are always businesses (but not if it's a residential pattern)
        // Also handle variations like "A1", "A #1", etc.
        if (!forceAsBusiness && !looksLikeResidentialWithInitials && 
            wordsForCheck.Length >= 2 && 
            wordsForCheck[0].Equals("A", StringComparison.OrdinalIgnoreCase))
        {
            // Check if second word is "1" or contains "1"
            if (wordsForCheck[1] == "1" || 
                wordsForCheck[1] == "#1" || 
                wordsForCheck[1] == "-1" ||
                wordsForCheck[1].StartsWith("1"))
            {
                forceAsBusiness = true;
                isLikelyBusiness = true; // Force as business
            }
        }
        // Also check for "A-1" as a single word
        else if (!forceAsBusiness && !looksLikeResidentialWithInitials && 
                 wordsForCheck.Length > 0 && 
                 (wordsForCheck[0].Equals("A-1", StringComparison.OrdinalIgnoreCase) ||
                  wordsForCheck[0].Equals("A1", StringComparison.OrdinalIgnoreCase)))
        {
            forceAsBusiness = true;
            isLikelyBusiness = true; // Force as business
        }
        
        _logger.LogInformation($"Text: '{remainingText}' - forceAsBusiness: {forceAsBusiness}, isLikelyBusiness: {isLikelyBusiness}, looksLikeResidentialWithInitials: {looksLikeResidentialWithInitials}");
        
        // For business entries, handle address detection carefully (skip if it's clearly residential)
        if (isLikelyBusiness && !looksLikeResidentialWithInitials)
        {
            var words = remainingText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            int addressStartIndex = -1;
            
            // Look for clear business terminators that often precede addresses
            // Check for corporate suffixes that indicate addresses often come after these
            int lastTerminatorIndex = -1;
            for (int i = 0; i < words.Length; i++)
            {
                var cleanWord = words[i].Trim('.', ',');
                if (await _businessWordService.IsCorporateSuffixAsync(cleanWord))
                {
                    lastTerminatorIndex = i;
                }
            }
            
            // Look for patterns that clearly indicate an address
            for (int i = 0; i < words.Length; i++)
            {
                var word = words[i];
                
                // Check if this is a number (potential civic address)
                if (Regex.IsMatch(word, @"^\d+$"))
                {
                    _logger.LogInformation($"Found number '{word}' at position {i}");
                    
                    // Special case: "A 1" pattern at the beginning - this is part of business name
                    // Check if this is position 1 and previous word is "A"
                    if (i == 1 && word == "1" && words[0].Equals("A", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogInformation($"Skipping number '1' at position 1 because it follows 'A' (A 1 pattern)");
                        // This is "A 1" pattern - skip looking for address at this number
                        // But we need to find the REAL address number later
                        continue;
                    }
                    
                    // Skip if number is in parentheses (part of business name like "(1987)")
                    if (i > 0 && (words[i - 1] == "(" || words[i - 1].EndsWith("(")))
                        continue;
                    
                    // Skip if this comes before the last business terminator
                    if (lastTerminatorIndex > i)
                        continue;
                    
                    // Look ahead to see what follows this number
                    bool looksLikeAddress = false;
                    
                    // First check immediate next word for street type
                    if (i < words.Length - 1)
                    {
                        var nextWord = words[i + 1].Trim('.', ',');
                        _logger.LogInformation($"Checking if next word '{nextWord}' is a street type...");
                        
                        // If next word is a street type, definitely an address
                        if (_streetTypeService.IsStreetType(nextWord))
                        {
                            _logger.LogInformation($"Yes, '{nextWord}' is a street type!");
                            looksLikeAddress = true;
                        }
                        // If it's another number (unit + civic)
                        else if (Regex.IsMatch(nextWord, @"^\d+$"))
                        {
                            looksLikeAddress = true;
                        }
                        // Check if this could be a known street name (even without type)
                        else if (!looksLikeAddress)
                        {
                            // Build potential street name from next words
                            var potentialStreetNames = new List<string>();
                            
                            // Try single word
                            potentialStreetNames.Add(nextWord);
                            
                            // Try multi-word combinations (up to 4 words for streets like "Filles De Jesus")
                            for (int j = i + 2; j < Math.Min(i + 5, words.Length); j++)
                            {
                                var multiWordStreet = string.Join(" ", 
                                    words.Skip(i + 1).Take(j - i).Select(w => w.Trim('.', ',')));
                                potentialStreetNames.Add(multiWordStreet);
                            }
                            
                            // Check each potential street name
                            foreach (var streetName in potentialStreetNames)
                            {
                                if (await _streetNameService.IsKnownStreetNameAsync(streetName))
                                {
                                    _logger.LogInformation($"Found known street name: '{streetName}'");
                                    looksLikeAddress = true;
                                    break;
                                }
                            }
                        }
                        
                        // If still not found, check the original logic
                        if (!looksLikeAddress)
                        {
                            // Check the next few words for a street type
                            for (int j = i + 2; j < Math.Min(i + 4, words.Length); j++)
                            {
                                var checkWord = words[j].Trim('.', ',');
                                if (_streetTypeService.IsStreetType(checkWord))
                                {
                                    looksLikeAddress = true;
                                    break;
                                }
                            }
                            
                            // If still not found, check if the next word could be a street name 
                            // This is a weaker indicator, but use it if:
                            // 1. The word is capitalized and longer than 2 chars
                            // 2. We're after a business terminator OR
                            // 3. We're after common business words like "Stores", "Insurance", etc
                            if (!looksLikeAddress && char.IsUpper(nextWord[0]) && nextWord.Length > 2)
                            {
                                // Check if we're after a business terminator
                                if (lastTerminatorIndex >= 0 && i > lastTerminatorIndex)
                                {
                                    _logger.LogInformation($"Number after business terminator, assuming '{nextWord}' is street name");
                                    looksLikeAddress = true;
                                }
                                // Or check if previous word suggests this is an address
                                else if (i > 0)
                                {
                                    var prevWord = words[i - 1].ToLower();
                                    var businessContextWords = new HashSet<string> { 
                                        "stores", "insurance", "services", "solutions", "management",
                                        "moncton", "dieppe", "riverview", "fredericton", "saint" 
                                    };
                                    if (businessContextWords.Contains(prevWord))
                                    {
                                        _logger.LogInformation($"Number after '{prevWord}', assuming '{nextWord}' is street name");
                                        looksLikeAddress = true;
                                    }
                                }
                            }
                        }
                    }
                    
                    // If this number appears to start an address, use it
                    if (looksLikeAddress)
                    {
                        _logger.LogInformation($"Number at position {i} looks like address start!");
                        addressStartIndex = i;
                        break;
                    }
                    else
                    {
                        _logger.LogInformation($"Number at position {i} doesn't look like address");
                    }
                }
                // Check for unit indicators
                else if (Regex.IsMatch(word, @"^(Unit|Apt|Suite|Room|Rm)$", RegexOptions.IgnoreCase))
                {
                    addressStartIndex = i;
                    break;
                }
            }
            
            // If we found an address, split the text
            _logger.LogInformation($"Final addressStartIndex: {addressStartIndex}");
            if (addressStartIndex >= 0)
            {
                // Calculate character position for the split
                int charPos = 0;
                for (int j = 0; j < addressStartIndex; j++)
                {
                    charPos += words[j].Length + 1;
                }
                
                // Handle edge case where charPos might be 0 or beyond string length
                if (charPos > 0 && charPos < remainingText.Length)
                {
                    result.Name = remainingText.Substring(0, charPos).Trim();
                    result.Address = remainingText.Substring(charPos).Trim();
                }
                else
                {
                    result.Name = remainingText;
                    result.Address = "";
                }
                
                result.IsBusinessName = true;
                result.IsResidentialName = false;
                result.Confidence.NameConfidence = 85; // Default confidence for forced business
                result.Confidence.AddressConfidence = 85;
                result.Confidence.PhoneConfidence = 100;
                result.Success = true;
                
                return result;
            }
            else
            {
                // Before checking for communities, check if the last word is a business terminator
                // Business terminators should not be treated as addresses
                var lastWordToCheck = words.Length > 0 ? words[^1].Trim('.', ',') : "";
                
                var businessEndings = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "Ltd", "Limited", "Inc", "Incorporated", "Corp", "Corporation",
                    "LLC", "LLP", "Sons", "Bros", "Brothers", "Co", "Company"
                };
                
                // Only check for communities if the last word is NOT a business terminator
                if (!businessEndings.Contains(lastWordToCheck))
                {
                    // No address found - check if the end contains a community (handles multi-word communities)
                    var communityResult = await _communityService.FindCommunityAtEndAsync(remainingText, province);
                    
                    if (communityResult.Found && communityResult.StartIndex > 0)
                    {
                        // Split off the community as the address
                        result.Name = remainingText.Substring(0, communityResult.StartIndex).Trim();
                        result.Address = communityResult.CommunityName ?? "";
                        result.Confidence.AddressConfidence = 75; // Community only
                    }
                    else
                    {
                        // No address found - keep entire text as business name
                        result.Name = remainingText;
                        result.Address = "";
                        result.Confidence.AddressConfidence = 0;
                    }
                }
                else
                {
                    // Business terminator found - keep entire text as business name
                    result.Name = remainingText;
                    result.Address = "";
                    result.Confidence.AddressConfidence = 0;
                }
                
                result.IsBusinessName = true;
                result.IsResidentialName = false;
                result.Confidence.NameConfidence = 85; // Default confidence for forced business
                result.Confidence.PhoneConfidence = 100;
                result.Success = true;
                
                return result;
            }
        }
        
        // Special handling for residential patterns with initials
        if (looksLikeResidentialWithInitials && wordsForCheck.Length == 3)
        {
            // Treat all 3 words as the name
            result.Name = remainingText;
            result.Address = "";
            result.Confidence.AddressConfidence = 0;
            
            // It's a residential name
            result.IsBusinessName = false;
            result.IsResidentialName = true;
            result.Confidence.NameConfidence = 85; // High confidence for this specific pattern
            
            // Split the name properly based on the pattern
            if (residentialInitialPattern == "initial-surname-initial")
            {
                // For "A Mwinkeu C", we want: LastName = "Mwinkeu", FirstName = "A C"
                result.LastName = wordsForCheck[1]; // Middle word is the surname
                result.FirstName = $"{wordsForCheck[0]} {wordsForCheck[2]}"; // First and last are initials
            }
            else if (residentialInitialPattern == "initial-initial-surname")
            {
                // For "J M Smith", we want: LastName = "Smith", FirstName = "J M"
                result.LastName = wordsForCheck[2]; // Last word is the surname
                result.FirstName = $"{wordsForCheck[0]} {wordsForCheck[1]}"; // First two are initials
            }
            
            result.Confidence.PhoneConfidence = 100;
            result.Success = true;
            
            _logger.LogInformation($"Parsed as residential with {residentialInitialPattern} pattern: '{input}' -> Name: '{result.Name}', LastName: '{result.LastName}', FirstName: '{result.FirstName}'");
            
            return result;
        }
        
        // Check if this looks like a phonebook entry (personal name format)
        // Pattern: [LastName] [FirstName/Initial] [Address]
        var phonebookParse = await ParsePhonebookFormatAsync(remainingText, province);
        if (phonebookParse.IsPhonebook)
        {
            result.Name = phonebookParse.Name;
            result.Address = phonebookParse.Address;
            result.Confidence.AddressConfidence = phonebookParse.AddressConfidence;
            
            // Classify the name
            if (!string.IsNullOrWhiteSpace(result.Name))
            {
                // Use BusinessWordService to check if the name contains strong business indicators
                var nameBusinessAnalysis = await _businessWordService.AnalyzePhraseAsync(result.Name);
                
                if (nameBusinessAnalysis.isBusiness && 
                    nameBusinessAnalysis.maxStrength >= BusinessIndicatorStrength.Strong)
                {
                    // Force as business
                    result.IsBusinessName = true;
                    result.IsResidentialName = false;
                    result.Confidence.NameConfidence = 95; // High confidence due to strong business words
                }
                else
                {
                    var classification = await _classificationService.ClassifyAsync(result.Name);
                    result.IsBusinessName = classification.IsBusiness;
                    result.IsResidentialName = classification.IsResidential;
                    result.Confidence.NameConfidence = classification.Confidence;
                    
                    // Split residential names into LastName and FirstName
                    if (result.IsResidentialName)
                    {
                        SplitResidentialName(result);
                    }
                }
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
            // Check for business words using database-driven service
            var nameBusinessAnalysis = await _businessWordService.AnalyzePhraseAsync(result.Name);
            
            if (nameBusinessAnalysis.isBusiness)
            {
                // Force as business based on database analysis
                result.IsBusinessName = true;
                result.IsResidentialName = false;
                result.Confidence.NameConfidence = nameBusinessAnalysis.maxStrength switch
                {
                    BusinessIndicatorStrength.Absolute => 99,
                    BusinessIndicatorStrength.Strong => 95,
                    BusinessIndicatorStrength.Medium => 85,
                    _ => 75
                };
                _logger.LogDebug($"Name classified as business: {nameBusinessAnalysis.reason}");
            }
            else
            {
                var classification = await _classificationService.ClassifyAsync(result.Name);
                result.IsBusinessName = classification.IsBusiness;
                result.IsResidentialName = classification.IsResidential;
                result.Confidence.NameConfidence = classification.Confidence;
                
                // Split residential names into LastName and FirstName
                if (result.IsResidentialName)
                {
                    SplitResidentialName(result);
                }
            }
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
    
    private async Task<PhonebookParseResult> ParsePhonebookFormatAsync(string text, string? province = null)
    {
        var result = new PhonebookParseResult { IsPhonebook = false };
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        if (words.Length < 1)
        {
            return result;
        }
        
        // First, check backwards from the end for community names
        // BUT ONLY if there are no clear address indicators (numbers, street types)
        // This helps identify patterns like "Name Initial Community Phone"
        int communityIndex = -1;
        string? communityName = null;
        
        // First scan: check if there are any numbers or street indicators in the middle
        bool hasAddressIndicators = false;
        for (int i = 1; i < words.Length - 1; i++) // Skip first and last word
        {
            if (Regex.IsMatch(words[i], @"^\d+$") || 
                Regex.IsMatch(words[i], @"^(Unit|Apt|Suite|Room|Rm)$", RegexOptions.IgnoreCase))
            {
                hasAddressIndicators = true;
                break;
            }
        }
        
        // Only check for community if there are NO other address indicators
        if (!hasAddressIndicators && words.Length >= 3) // Need at least 3 words for name + community
        {
            // Check for multi-word communities at the end
            // Try 3 words, then 2, then 1
            for (int wordsToCheck = Math.Min(3, words.Length - 2); wordsToCheck >= 1; wordsToCheck--)
            {
                // Skip if any of these words look like phone numbers
                bool hasPhoneNumber = false;
                for (int j = words.Length - wordsToCheck; j < words.Length; j++)
                {
                    if (Regex.IsMatch(words[j], @"^\d{3}-?\d{4}$") || Regex.IsMatch(words[j], @"^\d{3}$"))
                    {
                        hasPhoneNumber = true;
                        break;
                    }
                }
                
                if (hasPhoneNumber)
                    continue;
                
                var potentialCommunity = string.Join(" ", words.Skip(words.Length - wordsToCheck).Take(wordsToCheck));
                if (await _communityService.IsCommunityNameAsync(potentialCommunity, province))
                {
                    // Make sure we have at least a proper name before it (2+ parts)
                    int nameWordCount = words.Length - wordsToCheck;
                    
                    // Need at least 2 name parts (last name + first name/initial)
                    if (nameWordCount >= 2)
                    {
                        communityIndex = words.Length - wordsToCheck;
                        communityName = potentialCommunity;
                        _logger.LogDebug($"Found community '{potentialCommunity}' starting at position {communityIndex} with {nameWordCount} name words before it");
                        break;
                    }
                }
            }
        }
        
        // Look for clear address indicators to determine where the name ends
        int addressStartIndex = -1;
        
        // If we found a community, use that as the address start
        if (communityIndex != -1)
        {
            addressStartIndex = communityIndex;
        }
        
        // Common address start patterns:
        // 1. Starts with a number (civic address): "123 Main St"
        // 2. Starts with "Unit", "Apt", "Suite": "Unit 7 1777 Pembina"
        // 3. Contains street types after potential name words
        
        // Only look for other indicators if we haven't found a community
        if (addressStartIndex == -1)
        {
            for (int i = 1; i < words.Length; i++)
        {
            var word = words[i];
            var prevWord = i > 0 ? words[i - 1] : "";
            
            // Check if this word indicates the start of an address
            bool isNumber = Regex.IsMatch(word, @"^\d+$");
            bool isUnit = Regex.IsMatch(word, @"^(Unit|Apt|Suite|Room|Rm)$", RegexOptions.IgnoreCase);
            bool isStreetType = _streetTypeService.IsStreetType(word);
            
            // Check if previous word was a connector (& or "et" for names like "M & L" or "Louis et Marie")
            bool prevWasConnector = prevWord == "&" || prevWord.Equals("et", StringComparison.OrdinalIgnoreCase);
            
            // Special handling for parenthetical numbers like (1987)
            if (isNumber && i > 0 && prevWord == "(")
            {
                // This is a number in parentheses, likely part of the business name
                _logger.LogDebug($"Number {word} in parentheses, treating as part of name");
                continue;
            }
            
            if (isNumber || isUnit)
            {
                // Definite address start
                addressStartIndex = i;
                break;
            }
            else if (isStreetType && i > 1)  // Street type after at least 2 words (potential name)
            {
                // Special case: Check if this is "Dr" used as an honorific (Doctor)
                // Clues: 1) Comes after a residential name (2 words)
                //        2) Followed by a number (street address)
                //        3) Followed by another street name
                if (word.Equals("Dr", StringComparison.OrdinalIgnoreCase) && i >= 2)
                {
                    // Check if the next word is a number (indicating a street address follows)
                    bool nextIsNumber = (i + 1 < words.Length) && Regex.IsMatch(words[i + 1], @"^\d+$");
                    
                    // Check if we have a pattern like "FirstName LastName Dr 123 Street St"
                    if (nextIsNumber && i + 2 < words.Length)
                    {
                        // This looks like Dr is an honorific, not a street type
                        // Continue looking for the real address start
                        _logger.LogDebug($"Detected 'Dr' as honorific at position {i}, not street type");
                        continue;
                    }
                }
                
                // This might be a street type in the address
                // But check if the previous word could be part of a name
                if (!prevWasConnector)
                {
                    addressStartIndex = i - 1; // The word before the street type starts the address
                    break;
                }
            }
        }
        }  // End of if (addressStartIndex == -1)
        
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
                    
                    // If this is the last word (no more words after), it's likely still part of the name
                    // (e.g., "Adekunle Olatunbosun K 859-4399")
                    if (i == words.Length - 1 || (i < words.Length - 1 && Regex.IsMatch(words[i + 1], @"^\d{3}-?\d{4}$")))
                    {
                        _logger.LogDebug($"Single letter '{word}' at end or before phone, treating as part of name");
                    }
                }
                // Is this a connector (& or "et")?
                else if (word == "&" || word.Equals("et", StringComparison.OrdinalIgnoreCase))
                {
                    hasAmpersand = true;
                    lastNamePartIndex = i;
                    consecutiveNameWords++;
                }
                // Is this "Dr" as an honorific?
                else if (word.Equals("Dr", StringComparison.OrdinalIgnoreCase) && i >= 2)
                {
                    // Check if the next word is a number (indicating a street address follows)
                    bool nextIsNumber = (i + 1 < words.Length) && Regex.IsMatch(words[i + 1], @"^\d+$");
                    
                    if (nextIsNumber)
                    {
                        // This is Dr as an honorific, include it in the name
                        lastNamePartIndex = i;
                        consecutiveNameWords++;
                        _logger.LogDebug($"Including 'Dr' as honorific in name at position {i}");
                    }
                }
                // Check if this word is in our name database
                else if (!Regex.IsMatch(word, @"^\d+$"))
                {
                    // Special handling for corporate suffixes
                    if (await _businessWordService.IsCorporateSuffixAsync(wordLower))
                    {
                        // This is a business terminator, include it in the name
                        lastNamePartIndex = i;
                        consecutiveNameWords++;
                        _logger.LogDebug($"Including business terminator '{word}' in name");
                        continue;
                    }
                    
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
                        
                        // If we have a connector (& or "et"), include the next word too
                        if (hasAmpersand && i < words.Length - 1)
                        {
                            lastNamePartIndex = i + 1; // Include the word after the connector
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
        
        // Special case: if the address is a single letter, it's likely an initial, not an address
        if (result.Address.Length == 1 && char.IsLetter(result.Address[0]))
        {
            // Append to name instead
            result.Name = result.Name + " " + result.Address;
            result.Address = "";
        }
        // Special case: if address starts with clear business words like "Sons", include them in the name
        else if (addressParts.Count > 0)
        {
            var firstAddressWord = addressParts[0].ToLower();
            var businessSuffixes = new HashSet<string> { "sons", "bros", "brothers", "sisters", "and" };
            
            if (businessSuffixes.Contains(firstAddressWord))
            {
                // Move this word to the name
                result.Name = result.Name + " " + addressParts[0];
                if (addressParts.Count > 1)
                {
                    result.Address = string.Join(" ", addressParts.Skip(1));
                }
                else
                {
                    result.Address = "";
                }
            }
        }
        
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
    
    private PhoneExtractionResult ExtractPhoneNumber(string input, string? defaultAreaCode = null)
    {
        var result = new PhoneExtractionResult();
        
        // Validate and normalize the default area code if provided
        if (!string.IsNullOrWhiteSpace(defaultAreaCode))
        {
            defaultAreaCode = new string(defaultAreaCode.Where(char.IsDigit).ToArray());
            if (defaultAreaCode.Length != 3)
            {
                defaultAreaCode = null; // Invalid area code, ignore it
            }
        }
        
        // Try area code pattern first, but check if it's actually a road number
        var areaCodeMatch = _areaCodePhonePattern.Match(input);
        if (areaCodeMatch.Success)
        {
            _logger.LogInformation($"Area code pattern matched: '{areaCodeMatch.Value}' at position {areaCodeMatch.Index}");
            
            // Before accepting this as an area code, check if the 3-digit number 
            // is preceded by a road type indicator
            var beforeMatch = input.Substring(0, areaCodeMatch.Index).Trim();
            var words = beforeMatch.Split(' ');
            
            _logger.LogInformation($"Before match: '{beforeMatch}', Words count: {words.Length}");
            
            if (words.Length > 0)
            {
                var lastWord = words[^1].ToLower().TrimEnd('.', ',');
                _logger.LogInformation($"Last word before area code: '{lastWord}'");
                
                // Road types that are followed by numbers (like Highway 205, Route 101)
                var roadIndicators = new HashSet<string> { 
                    "highway", "hwy", "route", "rte", "road", "rd", 
                    "chemin", "ch", "autoroute", "aut"
                };
                
                if (roadIndicators.Contains(lastWord))
                {
                    // This is a road number, not an area code
                    // Just use the phone part without the road number
                    _logger.LogInformation($"Detected road indicator '{lastWord}' before 3-digit number, not treating as area code");
                    
                    // Extract just the phone number (second part of the match)
                    var phoneOnly = areaCodeMatch.Groups[2].Value.Trim();
                    result.Phone = NormalizePhoneNumber(phoneOnly, defaultAreaCode);
                    result.RemainingText = input.Substring(0, areaCodeMatch.Index).Trim() + " " + areaCodeMatch.Groups[1].Value;
                    result.Success = true;
                    return result;
                }
                else
                {
                    // It's an area code
                    _logger.LogInformation($"Accepting as area code: '{areaCodeMatch.Value}'");
                    var areaCode = areaCodeMatch.Groups[1].Value;
                    var localNumber = areaCodeMatch.Groups[2].Value;
                    result.Phone = NormalizePhoneNumber(areaCode + localNumber);
                    result.RemainingText = beforeMatch;
                    result.Success = true;
                    return result;
                }
            }
            else
            {
                // No words before, accept as area code
                var areaCode = areaCodeMatch.Groups[1].Value;
                var localNumber = areaCodeMatch.Groups[2].Value;
                result.Phone = NormalizePhoneNumber(areaCode + localNumber);
                result.RemainingText = beforeMatch;
                result.Success = true;
                return result;
            }
        }
        
        // Try standard phone pattern
        var phoneMatch = _phonePattern.Match(input);
        if (phoneMatch.Success)
        {
            var phone = phoneMatch.Value.Trim();
            var remaining = input.Substring(0, phoneMatch.Index).Trim();
            
            // Check for area code or suite/unit number before phone
            var words = remaining.Split(' ');
            if (words.Length > 0)
            {
                var lastWord = words[^1];
                
                // Check if last word is a 3-digit number that could be area code or suite
                if (Regex.IsMatch(lastWord, @"^\d{3}$"))
                {
                    // Check if word before the number indicates it's a suite/unit
                    if (words.Length > 1)
                    {
                        var prevWord = words[^2].ToLower().TrimEnd('.', ',');
                        // Suite indicators and road types that are followed by numbers
                        // The number stays with the address, not the phone
                        var suiteIndicators = new HashSet<string> { 
                            // Suite/unit indicators
                            "suite", "ste", "unit", "apt", "apartment", "room", "rm", "floor", "fl",
                            // Road types that are followed by numbers (like Highway 205, Route 101)
                            "highway", "hwy", "route", "rte", "road", "rd", "street", "st",
                            "avenue", "ave", "av", "boulevard", "blvd", "drive", "dr",
                            "lane", "ln", "place", "pl", "court", "ct", "circle", "cir",
                            "trail", "tr", "path", "parkway", "pkwy", "way",
                            // French road types
                            "chemin", "ch", "rue", "autoroute", "aut", "voie"
                        };
                        
                        _logger.LogDebug($"Checking if '{prevWord}' is a suite/road indicator (3-digit number: {lastWord})");
                        
                        if (suiteIndicators.Contains(prevWord))
                        {
                            // This is a suite/unit number, not an area code
                            result.Phone = NormalizePhoneNumber(phone, defaultAreaCode);
                            result.RemainingText = remaining;
                        }
                        else
                        {
                            // Likely an area code
                            result.Phone = NormalizePhoneNumber(lastWord + phone);
                            result.RemainingText = string.Join(" ", words.Take(words.Length - 1));
                        }
                    }
                    else
                    {
                        // Only one word before phone, assume it's area code
                        result.Phone = NormalizePhoneNumber(lastWord + phone);
                        result.RemainingText = string.Join(" ", words.Take(words.Length - 1));
                    }
                }
                else
                {
                    result.Phone = NormalizePhoneNumber(phone, defaultAreaCode);
                    result.RemainingText = remaining;
                }
            }
            else
            {
                result.Phone = NormalizePhoneNumber(phone, defaultAreaCode);
                result.RemainingText = remaining;
            }
            
            result.Success = true;
            return result;
        }
        
        result.Success = false;
        result.ErrorMessage = "No valid phone number found";
        return result;
    }
    
    private string NormalizePhoneNumber(string phone, string? defaultAreaCode = null)
    {
        // Remove all non-digit characters
        var digitsOnly = new string(phone.Where(char.IsDigit).ToArray());
        
        // If we have 10 digits, return as is
        if (digitsOnly.Length == 10)
        {
            return digitsOnly;
        }
        
        // If we have 7 digits and a default area code, prepend it
        if (digitsOnly.Length == 7 && !string.IsNullOrWhiteSpace(defaultAreaCode))
        {
            var areaCodeDigits = new string(defaultAreaCode.Where(char.IsDigit).ToArray());
            if (areaCodeDigits.Length == 3)
            {
                return areaCodeDigits + digitsOnly;
            }
        }
        
        // If we have 11 digits starting with 1 (country code), remove the 1
        if (digitsOnly.Length == 11 && digitsOnly[0] == '1')
        {
            return digitsOnly.Substring(1);
        }
        
        // Return what we have - it might not be a valid 10-digit number
        // but we'll preserve it and let the caller handle validation
        return digitsOnly;
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
    
    private void SplitResidentialName(ParseResult result)
    {
        if (string.IsNullOrWhiteSpace(result.Name))
            return;
            
        var name = result.Name.Trim();
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        if (parts.Length == 0)
            return;
            
        if (parts.Length == 1)
        {
            result.LastName = parts[0];
            result.FirstName = null;
            return;
        }
        
        var firstPart = parts[0];
        var remainingParts = string.Join(" ", parts.Skip(1));
        
        // Check if first part is initial(s) and remaining is a regular name
        // Example: "M Allain" -> LastName: Allain, FirstName: M
        if (IsInitialOrMultipleInitials(firstPart) && !IsInitialOrMultipleInitials(remainingParts))
        {
            result.LastName = remainingParts;
            result.FirstName = firstPart;
        }
        else
        {
            // Standard format: "Smith John & Mary" -> LastName: Smith, FirstName: John & Mary
            result.LastName = firstPart;
            result.FirstName = FormatFirstName(remainingParts);
        }
    }
    
    private bool IsInitialOrMultipleInitials(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;
            
        text = text.Trim().Replace(".", "");
        
        // Single or double initial (e.g., "M", "AB")
        if (text.Length <= 2 && text.All(char.IsUpper))
            return true;
            
        // Multiple initials separated by spaces or &
        var parts = text.Split(new[] { ' ', '&' }, StringSplitOptions.RemoveEmptyEntries);
        
        return parts.All(part => 
            part.Replace(".", "").Length <= 2 && 
            part.Replace(".", "").All(char.IsUpper)
        );
    }
    
    private string FormatFirstName(string firstName)
    {
        if (string.IsNullOrWhiteSpace(firstName))
            return string.Empty;
            
        // Ensure proper spacing around ampersands
        firstName = Regex.Replace(firstName.Trim(), @"\s*&\s*", " & ");
        
        return firstName;
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