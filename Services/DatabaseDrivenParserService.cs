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
    private readonly IReferenceDataService _referenceDataService;
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
        IReferenceDataService referenceDataService,
        PhonebookContext context,
        ILogger<DatabaseDrivenParserService> logger)
    {
        _classificationService = classificationService;
        _communityService = communityService;
        _streetTypeService = streetTypeService;
        _streetNameService = streetNameService;
        _businessWordService = businessWordService;
        _referenceDataService = referenceDataService;
        _context = context;
        _logger = logger;
    }
    
    public async Task<ParseResult> ParseAsync(string input, string? province = null, string? areaCode = null)
    {
        var result = new ParseResult { Input = input };
        var originalInput = input; // Keep original for debug logging
        
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
        
        // Special debug logging for problematic records
        var debugRecords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Aguayo Jesus Riverview 204-7610",
            "Albert Luc Grand - Barachois 532-6339",
            "Aldred Devon 86 Jasmine 388-1009",
            "Alia Abdulsatar 140 Gordon 384-1669",
            "Allain Real 546 Main 532-4500",
            "Allen J Salisbury West 372-4111",
            "Alley Jim Steeves Mountain 869-8981",
            "allis randy 854-1889",
            "Alta Musica Riverview 857-4290",
            "Alward Vernon 123 Fredericton Rd 372-5317",
            "Amberman Ms 858-9860",
            "Andrade Genesis 11 Breau 576-7667",
            "Andrews Max 196 King 856-9989",
            "Arc Andre Leblanc 1132 Route 133 Beaubassin East 533-8322",
            "Arsenault Brandon 388-7131"
        };
        
        bool isDebugRecord = debugRecords.Contains(originalInput);
        if (isDebugRecord)
        {
            _logger.LogWarning($"=== DEBUG RECORD START: '{originalInput}' ===");
        }
        
        // OCR Error Detection and Correction
        input = await DetectAndCorrectOCRErrors(input, province);
        
        // Step 1: Extract phone number
        var phoneExtraction = await ExtractPhoneNumber(input, areaCode);
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
        
        // Check for initial patterns in the FIRST 2 or 3 words
        // This preliminary check can be overridden by strong business indicators
        // We only look at the beginning of the text since it may include an address
        if (wordsForCheck.Length >= 2)
        {
            // Check for "Initial Name" or "Name Initial" patterns in first 2 words
            // An initial is a single letter (with or without period)
            bool firstIsInitial = (wordsForCheck[0].Length == 1 && char.IsLetter(wordsForCheck[0][0])) ||
                                 (wordsForCheck[0].Length == 2 && wordsForCheck[0][1] == '.' && char.IsLetter(wordsForCheck[0][0]));
            bool secondIsInitial = (wordsForCheck[1].Length == 1 && char.IsLetter(wordsForCheck[1][0])) ||
                                  (wordsForCheck[1].Length == 2 && wordsForCheck[1][1] == '.' && char.IsLetter(wordsForCheck[1][0]));
            
            // A name is anything longer than an initial
            bool firstIsName = !firstIsInitial && wordsForCheck[0].Length >= 2 && char.IsLetter(wordsForCheck[0][0]);
            bool secondIsName = !secondIsInitial && wordsForCheck[1].Length >= 2 && char.IsLetter(wordsForCheck[1][0]);
            
            // Log at INFO level for debugging these specific cases
            if (wordsForCheck[0].StartsWith("Abdel") || wordsForCheck[0].StartsWith("Aber"))
            {
                _logger.LogInformation($"SPECIAL DEBUG - Initial detection for '{wordsForCheck[0]}' (len={wordsForCheck[0].Length}) and '{wordsForCheck[1]}' (len={wordsForCheck[1].Length}): " +
                               $"firstIsInitial={firstIsInitial}, secondIsInitial={secondIsInitial}, " +
                               $"firstIsName={firstIsName}, secondIsName={secondIsName}, " +
                               $"first[0]='{wordsForCheck[0][0]}', isLetter={char.IsLetter(wordsForCheck[0][0])}");
            }
            
            if (firstIsInitial && secondIsName)
            {
                looksLikeResidentialWithInitials = true;
                residentialInitialPattern = "initial-name";
                _logger.LogDebug($"Detected POTENTIAL residential pattern 'initial name': {remainingText}");
            }
            else if (firstIsName && secondIsInitial)
            {
                looksLikeResidentialWithInitials = true;
                residentialInitialPattern = "name-initial";
                _logger.LogDebug($"Detected POTENTIAL residential pattern 'name initial': {remainingText}");
            }
        }
        
        if (!looksLikeResidentialWithInitials && wordsForCheck.Length >= 3)
        {
            // Check patterns in first 3 words
            bool firstIsInitial = (wordsForCheck[0].Length == 1 && char.IsLetter(wordsForCheck[0][0])) ||
                                 (wordsForCheck[0].Length == 2 && wordsForCheck[0][1] == '.' && char.IsLetter(wordsForCheck[0][0]));
            bool secondIsInitial = (wordsForCheck[1].Length == 1 && char.IsLetter(wordsForCheck[1][0])) ||
                                  (wordsForCheck[1].Length == 2 && wordsForCheck[1][1] == '.' && char.IsLetter(wordsForCheck[1][0]));
            bool thirdIsInitial = (wordsForCheck[2].Length == 1 && char.IsLetter(wordsForCheck[2][0])) ||
                                 (wordsForCheck[2].Length == 2 && wordsForCheck[2][1] == '.' && char.IsLetter(wordsForCheck[2][0]));
            
            bool firstIsName = !firstIsInitial && wordsForCheck[0].Length >= 2 && char.IsLetter(wordsForCheck[0][0]);
            bool secondIsName = !secondIsInitial && wordsForCheck[1].Length >= 2 && char.IsLetter(wordsForCheck[1][0]);
            bool thirdIsName = !thirdIsInitial && wordsForCheck[2].Length >= 2 && char.IsLetter(wordsForCheck[2][0]);
            
            if (firstIsInitial && thirdIsInitial && secondIsName)
            {
                looksLikeResidentialWithInitials = true;
                residentialInitialPattern = "initial-surname-initial";
                _logger.LogDebug($"Detected POTENTIAL residential pattern 'initial surname initial': {remainingText}");
            }
            else if (firstIsInitial && secondIsInitial && thirdIsName)
            {
                looksLikeResidentialWithInitials = true;
                residentialInitialPattern = "initial-initial-surname";
                _logger.LogDebug($"Detected POTENTIAL residential pattern 'initial initial surname': {remainingText}");
            }
            else if (firstIsName && secondIsInitial && thirdIsInitial)
            {
                looksLikeResidentialWithInitials = true;
                residentialInitialPattern = "name-initial-initial";
                _logger.LogDebug($"Detected POTENTIAL residential pattern 'name initial initial': {remainingText}");
            }
        }
        
        // CRITICAL: Extract the address FIRST, then analyze only the remaining name portion
        // This ensures street types (Dr, Av, St) don't get misinterpreted as business indicators
        string namePortionForAnalysis = remainingText;
        string extractedAddress = "";
        var wordsForAnalysis = remainingText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        // Get street types from database
        var streetTypes = await _referenceDataService.GetStreetTypesAsync();
        
        // Find where the address likely starts - look for patterns like:
        // 1. Number followed by street name (e.g., "123 Main St")
        // 2. Unit/Apt indicators (e.g., "A22", "#5")
        // 3. Just a street name with type (e.g., "Mountain Rd")
        int addressStartIndex = -1;
        
        for (int i = 0; i < wordsForAnalysis.Length; i++)
        {
            // Check if this word is a civic number or unit indicator
            if (Regex.IsMatch(wordsForAnalysis[i], @"^[A-Z]?\d+[A-Z]?$|^#\d+$"))
            {
                addressStartIndex = i;
                break;
            }
            
            // Check if this word is a street type (even without a number)
            if (i > 0 && streetTypes.Contains(wordsForAnalysis[i]))
            {
                // Check if the previous word could be a street name
                // (not a common first/last name indicator)
                addressStartIndex = i - 1;
                break;
            }
        }
        
        // Extract the name and address portions
        if (addressStartIndex > 0)
        {
            namePortionForAnalysis = string.Join(" ", wordsForAnalysis.Take(addressStartIndex));
            extractedAddress = string.Join(" ", wordsForAnalysis.Skip(addressStartIndex));
            _logger.LogDebug($"Extracted - Name: '{namePortionForAnalysis}', Address: '{extractedAddress}' from '{remainingText}'");
        }
        else if (addressStartIndex == 0)
        {
            // The entire string appears to be an address (starts with number)
            // This is unusual for a phone book entry, treat with low confidence
            namePortionForAnalysis = "";
            extractedAddress = remainingText;
            _logger.LogDebug($"Unusual pattern - entire string appears to be address: '{remainingText}'");
        }
        else
        {
            // No clear address pattern found - might be name only or phone only
            _logger.LogDebug($"No clear address pattern found in: '{remainingText}'");
        }
        
        // Use BusinessWordService to analyze ONLY the name portion for business indicators
        if (isDebugRecord)
        {
            _logger.LogWarning($"DEBUG: Analyzing name portion for business indicators: '{namePortionForAnalysis}'");
        }
        var businessAnalysis = await _businessWordService.AnalyzePhraseAsync(namePortionForAnalysis);
        if (isDebugRecord)
        {
            _logger.LogWarning($"DEBUG: Business analysis result - isBusiness: {businessAnalysis.isBusiness}, maxStrength: {businessAnalysis.maxStrength}");
        }
        
        // Check if we have strong business indicators
        bool hasStrongBusinessWords = businessAnalysis.isBusiness && 
                                      businessAnalysis.maxStrength >= BusinessIndicatorStrength.Strong;
        
        // Log the business analysis result for debugging
        _logger.LogDebug($"Business analysis for '{remainingText}': isBusiness={businessAnalysis.isBusiness}, maxStrength={businessAnalysis.maxStrength}, reason={businessAnalysis.reason}");
        
        // If we detected a residential pattern with initials, only override it for absolute business indicators
        if (looksLikeResidentialWithInitials)
        {
            if (businessAnalysis.maxStrength == BusinessIndicatorStrength.Absolute)
            {
                forceAsBusiness = true;
                isLikelyBusiness = true;
                looksLikeResidentialWithInitials = false; // Override residential pattern
                _logger.LogDebug($"Absolute business indicator found - overriding residential pattern");
            }
            else
            {
                // Keep the residential pattern - initials with names are typically residential
                forceAsBusiness = false;
                isLikelyBusiness = false;
                _logger.LogDebug($"Keeping residential pattern despite business analysis (strength={businessAnalysis.maxStrength})");
            }
        }
        else if (hasStrongBusinessWords)
        {
            forceAsBusiness = true;
            isLikelyBusiness = true;
            _logger.LogDebug($"Strong business indicators found");
        }
        else if (businessAnalysis.maxStrength == BusinessIndicatorStrength.Absolute)
        {
            forceAsBusiness = true;
            isLikelyBusiness = true;
            _logger.LogDebug($"Absolute business indicator found - forcing as business");
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
        // Also handle variations like "A1", "A #1", "A - 1", etc.
        if (!forceAsBusiness && !looksLikeResidentialWithInitials && 
            wordsForCheck.Length >= 2 && 
            wordsForCheck[0].Equals("A", StringComparison.OrdinalIgnoreCase))
        {
            // Check if second word is "1" or contains "1"
            // Also handle "A - 1" pattern where hyphen is a separate word
            if (wordsForCheck[1] == "1" || 
                wordsForCheck[1] == "#1" || 
                wordsForCheck[1] == "-1" ||
                wordsForCheck[1].StartsWith("1") ||
                (wordsForCheck[1] == "-" && wordsForCheck.Length > 2 && wordsForCheck[2] == "1"))
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
        
        _logger.LogInformation($"Text: '{remainingText}' - forceAsBusiness: {forceAsBusiness}, isLikelyBusiness: {isLikelyBusiness}, looksLikeResidentialWithInitials: {looksLikeResidentialWithInitials}, residentialPattern: '{residentialInitialPattern}'");
        
        // For business entries, handle address detection carefully (skip if it's clearly residential)
        if (isLikelyBusiness && !looksLikeResidentialWithInitials)
        {
            var words = remainingText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            int businessAddressStartIndex = -1;
            
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
                    
                    // Special case: "A 1" or "A - 1" pattern at the beginning - this is part of business name
                    // Check if this is position 1 and previous word is "A"
                    if (i == 1 && word == "1" && words[0].Equals("A", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogInformation($"Skipping number '1' at position 1 because it follows 'A' (A 1 pattern)");
                        // This is "A 1" pattern - skip looking for address at this number
                        // But we need to find the REAL address number later
                        continue;
                    }
                    // Also check for "A - 1" pattern
                    if (i == 2 && word == "1" && words[0].Equals("A", StringComparison.OrdinalIgnoreCase) && words[1] == "-")
                    {
                        _logger.LogInformation($"Skipping number '1' at position 2 because it follows 'A -' (A - 1 pattern)");
                        // This is "A - 1" pattern - skip looking for address at this number
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
                        businessAddressStartIndex = i;
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
                    businessAddressStartIndex = i;
                    break;
                }
            }
            
            // If we found an address, split the text
            _logger.LogInformation($"Final addressStartIndex: {businessAddressStartIndex}");
            if (businessAddressStartIndex >= 0)
            {
                // Calculate character position for the split
                int charPos = 0;
                for (int j = 0; j < businessAddressStartIndex; j++)
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
                
                // Get business endings from database
                var businessEndings = await _referenceDataService.GetBusinessEndingsAsync();
                
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
        
        // Pattern detection helps us identify name boundaries, but doesn't determine classification
        // We ALWAYS use word data to determine if something is business or residential
        if (looksLikeResidentialWithInitials)
        {
            // The pattern suggests where the name ends, but we'll verify with data
            if (residentialInitialPattern == "initial-name" && wordsForCheck.Length >= 2)
            {
                // First, we need to determine how much of the text is actually the name
                // We'll check different possibilities and use word data to decide
                if (wordsForCheck.Length >= 3)
                {
                    // Option 1: All words before the first number could be the name (e.g., "A Human Touch")
                    // Option 2: Just the first 2 words (e.g., "A Lucille" from "A Lucille Dieppe")
                    // Option 3: First 3 words if third is an initial (e.g., "A Mwinkeu C")
                    
                    // Let's check what the word data tells us about different combinations
                    var twoWordName = string.Join(" ", wordsForCheck.Take(2));
                    var threeWordName = string.Join(" ", wordsForCheck.Take(3));
                    
                    // Check if the 2-word version has strong business indicators
                    var twoWordAnalysis = await _businessWordService.AnalyzePhraseAsync(twoWordName);
                    
                    // Check if the 3-word version has even stronger business indicators
                    var threeWordAnalysis = await _businessWordService.AnalyzePhraseAsync(threeWordName);
                    
                    // If the 3-word version is strongly business, use that
                    if (threeWordAnalysis.isBusiness && threeWordAnalysis.maxStrength >= BusinessIndicatorStrength.Strong)
                    {
                        // Use all 3 words as the name
                        result.Name = threeWordName;
                        
                        // Extract remaining as address
                        if (wordsForCheck.Length > 3)
                        {
                            var remainingAfterName = string.Join(" ", wordsForCheck.Skip(3));
                            var addressParse = await ParsePhonebookFormatAsync(remainingAfterName, province);
                            result.Address = addressParse.Address;
                            result.Confidence.AddressConfidence = addressParse.AddressConfidence;
                        }
                        
                        // Now classify using the full name
                        var classification = await _classificationService.ClassifyAsync(result.Name);
                        result.IsBusinessName = classification.IsBusiness;
                        result.IsResidentialName = classification.IsResidential;
                        result.Confidence.NameConfidence = classification.Confidence;
                        
                        result.Confidence.PhoneConfidence = 100;
                        result.Success = true;
                        
                        _logger.LogInformation($"Used word data to determine 3-word name: '{result.Name}' classified as {(result.IsBusinessName ? "BUSINESS" : "RESIDENTIAL")}");
                        return result;
                    }
                    else if (wordsForCheck[2].Length == 1 && char.IsLetter(wordsForCheck[2][0]))
                    {
                        // Pattern suggests initial-surname-initial: "A Mwinkeu C"
                        result.Name = string.Join(" ", wordsForCheck.Take(3));
                        
                        // Extract address from remaining text
                        if (wordsForCheck.Length > 3)
                        {
                            var remainingAfterName = string.Join(" ", wordsForCheck.Skip(3));
                            var addressParse = await ParsePhonebookFormatAsync(remainingAfterName, province);
                            result.Address = addressParse.Address;
                            result.Confidence.AddressConfidence = addressParse.AddressConfidence;
                        }
                        else
                        {
                            result.Address = "";
                            result.Confidence.AddressConfidence = 0;
                        }
                        
                        // Use classification service to determine if business or residential
                        var classification = await _classificationService.ClassifyAsync(result.Name);
                        result.IsBusinessName = classification.IsBusiness;
                        result.IsResidentialName = classification.IsResidential;
                        result.Confidence.NameConfidence = classification.Confidence;
                        
                        // Only split into first/last if it's residential
                        if (result.IsResidentialName)
                        {
                            result.LastName = wordsForCheck[1]; // Middle word is surname
                            result.FirstName = $"{wordsForCheck[0]} {wordsForCheck[2]}"; // First and last are initials
                        }
                        
                        result.Confidence.PhoneConfidence = 100;
                        result.Success = true;
                        
                        _logger.LogInformation($"Detected initial-surname-initial from initial-name pattern: '{input}' -> Name: '{result.Name}', LastName: '{result.LastName}', FirstName: '{result.FirstName}'");
                        return result;
                    }
                    else
                    {
                        // Check if the next part is hyphenated (like "T Adesola - Adeoye")
                        // Look for a dash in positions 2 or 3
                        bool hasHyphen = false;
                        int hyphenPos = -1;
                        
                        for (int i = 2; i < Math.Min(wordsForCheck.Length, 4); i++)
                        {
                            if (wordsForCheck[i] == "-" || wordsForCheck[i].Contains("-"))
                            {
                                hasHyphen = true;
                                hyphenPos = i;
                                break;
                            }
                        }
                        
                        if (hasHyphen && hyphenPos < wordsForCheck.Length - 1)
                        {
                            // Handle hyphenated name after initial: "T Adesola - Adeoye"
                            // Reconstruct the hyphenated name
                            var nameEndIndex = hyphenPos + 1; // Include word after hyphen
                            if (wordsForCheck[hyphenPos] == "-")
                            {
                                // Separate hyphen, need to include next word
                                nameEndIndex = hyphenPos + 1;
                            }
                            
                            // Join the name parts and normalize hyphen
                            var fullName = string.Join(" ", wordsForCheck.Take(nameEndIndex + 1));
                            fullName = Regex.Replace(fullName, @"\s*-\s*", "-");
                            var normalizedParts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                            
                            result.Name = fullName;
                            result.FirstName = normalizedParts[0]; // Initial
                            result.LastName = string.Join(" ", normalizedParts.Skip(1)); // Hyphenated last name
                            
                            // Extract address from remaining text
                            if (wordsForCheck.Length > nameEndIndex + 1)
                            {
                                var remainingAfterName = string.Join(" ", wordsForCheck.Skip(nameEndIndex + 1));
                                var addressParse = await ParsePhonebookFormatAsync(remainingAfterName, province);
                                result.Address = addressParse.Address;
                                result.Confidence.AddressConfidence = addressParse.AddressConfidence;
                            }
                            else
                            {
                                result.Address = "";
                                result.Confidence.AddressConfidence = 0;
                            }
                            
                            _logger.LogInformation($"Detected hyphenated name after initial: '{input}' -> Name: '{result.Name}', LastName: '{result.LastName}', FirstName: '{result.FirstName}'");
                        }
                        else
                        {
                            // Standard initial-name pattern: "A Lucille" possibly followed by address
                            var nameParts = wordsForCheck.Take(2).ToArray();
                            result.Name = string.Join(" ", nameParts);
                            result.LastName = nameParts[1];
                            result.FirstName = nameParts[0];
                            
                            // Extract address from remaining text (like "Dieppe" in "A Lucille Dieppe")
                            if (wordsForCheck.Length > 2)
                            {
                                var remainingAfterName = string.Join(" ", wordsForCheck.Skip(2));
                                var addressParse = await ParsePhonebookFormatAsync(remainingAfterName, province);
                                result.Address = addressParse.Address;
                                result.Confidence.AddressConfidence = addressParse.AddressConfidence > 0 ? addressParse.AddressConfidence : 50;
                                
                                // If no clear address was found but there's text, treat it as possible community/address
                                if (string.IsNullOrEmpty(result.Address) && !string.IsNullOrEmpty(remainingAfterName))
                                {
                                    result.Address = remainingAfterName.Split(' ')[0]; // Take first word as likely community
                                    result.Confidence.AddressConfidence = 40;
                                }
                            }
                            else
                            {
                                result.Address = "";
                                result.Confidence.AddressConfidence = 0;
                            }
                            
                            // Use classification service to determine business vs residential
                            var classification = await _classificationService.ClassifyAsync(result.Name);
                            result.IsBusinessName = classification.IsBusiness;
                            result.IsResidentialName = classification.IsResidential;
                            result.Confidence.NameConfidence = classification.Confidence;
                            
                            // Only split names if residential
                            if (result.IsResidentialName)
                            {
                                // Already set LastName and FirstName above
                            }
                            
                            result.Confidence.PhoneConfidence = 100;
                            result.Success = true;
                            
                            _logger.LogInformation($"Parsed with initial-name pattern: '{input}' -> Name: '{result.Name}' classified as {(result.IsBusinessName ? "BUSINESS" : "RESIDENTIAL")}");
                            
                            return result;
                        }
                    }
                }
                else
                {
                    // Handle the case with only 2 words (no third word to check)
                    var nameParts = wordsForCheck.Take(2).ToArray();
                    result.Name = string.Join(" ", nameParts);
                    result.Address = "";
                    result.Confidence.AddressConfidence = 0;
                    
                    // Use classification service to determine business vs residential
                    var classification = await _classificationService.ClassifyAsync(result.Name);
                    result.IsBusinessName = classification.IsBusiness;
                    result.IsResidentialName = classification.IsResidential;
                    result.Confidence.NameConfidence = classification.Confidence;
                    
                    // Only split names if residential
                    if (result.IsResidentialName)
                    {
                        result.LastName = nameParts[1];
                        result.FirstName = nameParts[0];
                    }
                    
                    result.Confidence.PhoneConfidence = 100;
                    result.Success = true;
                    
                    _logger.LogInformation($"Parsed with initial-name pattern (2 words): '{input}' -> Name: '{result.Name}' classified as {(result.IsBusinessName ? "BUSINESS" : "RESIDENTIAL")}");
                    
                    return result;
                }
            }
            else if (residentialInitialPattern == "name-initial" && wordsForCheck.Length >= 2)
            {
                // Pattern like "Lucille A" or "Adery J" followed by address/phone
                var nameParts = wordsForCheck.Take(2).ToArray();
                result.Name = string.Join(" ", nameParts);
                
                // Extract address from remaining text
                if (wordsForCheck.Length > 2)
                {
                    var remainingAfterName = string.Join(" ", wordsForCheck.Skip(2));
                    
                    // If the remaining text starts with a number, it's clearly an address
                    if (Regex.IsMatch(wordsForCheck[2], @"^\d+"))
                    {
                        result.Address = remainingAfterName;
                        result.Confidence.AddressConfidence = 90;
                    }
                    else
                    {
                        var addressParse = await ParsePhonebookFormatAsync(remainingAfterName, province);
                        result.Address = addressParse.Address;
                        result.Confidence.AddressConfidence = addressParse.AddressConfidence;
                    }
                }
                else
                {
                    result.Address = "";
                    result.Confidence.AddressConfidence = 0;
                }
                
                // Split name: LastName = first word, FirstName = second initial
                result.LastName = nameParts[0];
                result.FirstName = nameParts[1];
                
                result.IsBusinessName = false;
                result.IsResidentialName = true;
                result.Confidence.NameConfidence = 85;
                result.Confidence.PhoneConfidence = 100;
                result.Success = true;
                
                _logger.LogInformation($"Parsed as residential with name-initial pattern: '{input}' -> Name: '{result.Name}', LastName: '{result.LastName}', FirstName: '{result.FirstName}', Address: '{result.Address}'");
                
                return result;
            }
            else if (wordsForCheck.Length == 3)
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
                else if (residentialInitialPattern == "name-initial-initial")
                {
                    // For "Smith J M", we want: LastName = "Smith", FirstName = "J M"
                    result.LastName = wordsForCheck[0]; // First word is the surname
                    result.FirstName = $"{wordsForCheck[1]} {wordsForCheck[2]}"; // Last two are initials
                }
                
                result.Confidence.PhoneConfidence = 100;
                result.Success = true;
                
                _logger.LogInformation($"Parsed as residential with {residentialInitialPattern} pattern: '{input}' -> Name: '{result.Name}', LastName: '{result.LastName}', FirstName: '{result.FirstName}'");
                
                return result;
            }
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
                if (isDebugRecord)
                {
                    _logger.LogWarning($"DEBUG: Classifying name from phonebook parse: '{result.Name}'");
                }
                
                // Use BusinessWordService to check if the name contains strong business indicators
                var nameBusinessAnalysis = await _businessWordService.AnalyzePhraseAsync(result.Name);
                
                if (isDebugRecord)
                {
                    _logger.LogWarning($"DEBUG: Name business analysis - isBusiness: {nameBusinessAnalysis.isBusiness}, maxStrength: {nameBusinessAnalysis.maxStrength}");
                }
                
                if (nameBusinessAnalysis.isBusiness && 
                    nameBusinessAnalysis.maxStrength >= BusinessIndicatorStrength.Strong)
                {
                    // Force as business
                    result.IsBusinessName = true;
                    result.IsResidentialName = false;
                    result.Confidence.NameConfidence = 95; // High confidence due to strong business words
                    
                    if (isDebugRecord)
                    {
                        _logger.LogWarning($"DEBUG: FORCED AS BUSINESS due to strong indicators");
                    }
                }
                else
                {
                    var classification = await _classificationService.ClassifyAsync(result.Name);
                    result.IsBusinessName = classification.IsBusiness;
                    result.IsResidentialName = classification.IsResidential;
                    result.Confidence.NameConfidence = classification.Confidence;
                    
                    if (isDebugRecord)
                    {
                        _logger.LogWarning($"DEBUG: Classification result - IsBusiness: {classification.IsBusiness}, IsResidential: {classification.IsResidential}, Confidence: {classification.Confidence}");
                        _logger.LogWarning($"DEBUG: Classification reason: {classification.Reason}");
                        if (classification.DetailedScores != null && classification.DetailedScores.Any())
                        {
                            _logger.LogWarning($"DEBUG: Detailed scores:");
                            foreach (var score in classification.DetailedScores)
                            {
                                _logger.LogWarning($"  {score.Key}: {score.Value}");
                            }
                        }
                    }
                    
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
        
        if (isDebugRecord)
        {
            _logger.LogWarning($"=== DEBUG RECORD END: '{originalInput}' ===");
            _logger.LogWarning($"    Final Classification: {(result.IsBusinessName ? "BUSINESS" : "RESIDENTIAL")}");
            _logger.LogWarning($"    Name: '{result.Name}'");
            _logger.LogWarning($"    LastName: '{result.LastName}'");
            _logger.LogWarning($"    FirstName: '{result.FirstName}'");
            _logger.LogWarning($"    Address: '{result.Address}'");
            _logger.LogWarning($"    Phone: '{result.Phone}'");
            _logger.LogWarning($"    Confidence: {result.Confidence.NameConfidence}%");
        }
        
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
        // Get province codes from database
        var provinceAbbreviations = await _referenceDataService.GetProvinceCodesAsync();
        
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
            // Get business suffixes from database (secondary indicators)
            var businessIndicatorList = await _context.BusinessIndicators
                .Where(b => (b.IndicatorType == "secondary_indicator" || b.IndicatorType == "primary_suffix") 
                    && (b.IsActive ?? true))
                .Select(b => b.IndicatorText.ToLower())
                .ToListAsync();
            var businessIndicators = new HashSet<string>(businessIndicatorList, StringComparer.OrdinalIgnoreCase);
            var businessSuffixes = businessIndicators;
            
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
        
        // Get skip words from database
        var skipWords = await _referenceDataService.GetSkipWordsAsync("general");
        
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
    
    private async Task<PhoneExtractionResult> ExtractPhoneNumber(string input, string? defaultAreaCode = null)
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
        
        // FIRST: Check for suite indicators BEFORE trying phone patterns
        // This prevents suite numbers from being mistaken for area codes
        var words = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var suiteIndicators = await _referenceDataService.GetSuiteIndicatorsAsync();
        
        // Look for suite indicators in the input
        bool hasSuiteIndicator = false;
        int suiteIndicatorIndex = -1;
        for (int i = 0; i < words.Length; i++)
        {
            var word = words[i].ToLower().TrimEnd('.', ',');
            if (suiteIndicators.Contains(word))
            {
                hasSuiteIndicator = true;
                suiteIndicatorIndex = i;
                _logger.LogInformation($"Found suite indicator '{word}' at position {i}");
                break;
            }
        }
        
        // Special handling when we have a suite indicator
        if (hasSuiteIndicator && suiteIndicatorIndex < words.Length - 1)
        {
            // Check if there's a pattern like "Suite 209857-5732" or "Suite 209 857-5732"
            // The number after the suite indicator might be concatenated with the phone
            var afterSuite = string.Join(" ", words.Skip(suiteIndicatorIndex + 1));
            
            // Pattern for suite number directly attached to phone: Suite NNNNNN-NNNN
            var suitePhonePattern = new Regex(@"^(\d{3,6})(\d{3}-\d{4})$");
            var match = suitePhonePattern.Match(afterSuite.Replace(" ", ""));
            
            if (match.Success)
            {
                // Split the concatenated number
                var suiteNum = match.Groups[1].Value;
                var phoneNum = match.Groups[2].Value;
                
                _logger.LogInformation($"Detected suite number '{suiteNum}' concatenated with phone '{phoneNum}'");
                
                result.Phone = NormalizePhoneNumber(phoneNum, defaultAreaCode);
                result.RemainingText = string.Join(" ", words.Take(suiteIndicatorIndex + 1)) + " " + suiteNum;
                result.Success = true;
                return result;
            }
            
            // Pattern for suite number with space before phone
            var nextWord = words[suiteIndicatorIndex + 1];
            if (Regex.IsMatch(nextWord, @"^\d+$"))
            {
                // Suite number is separate from phone
                var phoneStartIndex = suiteIndicatorIndex + 2;
                if (phoneStartIndex < words.Length)
                {
                    var remainingAfterSuite = string.Join(" ", words.Skip(phoneStartIndex));
                    var phoneMatchAfterSuite = Regex.Match(remainingAfterSuite, @"(\d{3}[\s-]?\d{4})");
                    if (phoneMatchAfterSuite.Success)
                    {
                        _logger.LogInformation($"Detected suite number '{nextWord}' with phone '{phoneMatchAfterSuite.Value}'");
                        result.Phone = NormalizePhoneNumber(phoneMatchAfterSuite.Value, defaultAreaCode);
                        result.RemainingText = string.Join(" ", words.Take(phoneStartIndex));
                        result.Success = true;
                        return result;
                    }
                }
            }
        }
        
        // Try area code pattern, but check if it's actually a road number
        var areaCodeMatch = _areaCodePhonePattern.Match(input);
        if (areaCodeMatch.Success)
        {
            _logger.LogInformation($"Area code pattern matched: '{areaCodeMatch.Value}' at position {areaCodeMatch.Index}");
            
            // Before accepting this as an area code, check if the number 
            // is preceded by a road type indicator or suite indicator
            var beforeMatch = input.Substring(0, areaCodeMatch.Index).Trim();
            var wordsBeforeArea = beforeMatch.Split(' ');
            
            _logger.LogInformation($"Before match: '{beforeMatch}', Words count: {wordsBeforeArea.Length}");
            
            if (wordsBeforeArea.Length > 0)
            {
                var lastWord = wordsBeforeArea[^1].ToLower().TrimEnd('.', ',');
                _logger.LogInformation($"Last word before potential area code: '{lastWord}'");
                
                // Get indicators from database
                var roadIndicators = await _referenceDataService.GetRoadIndicatorsAsync();
                var suiteIndicatorsArea = await _referenceDataService.GetSuiteIndicatorsAsync();
                
                // Check if it's a road indicator
                if (roadIndicators.Contains(lastWord))
                {
                    // This is a road number, not an area code
                    _logger.LogInformation($"Detected road indicator '{lastWord}' before number, not treating as area code");
                    
                    // Extract just the phone number (second part of the match)
                    var phoneOnly = areaCodeMatch.Groups[2].Value.Trim();
                    result.Phone = NormalizePhoneNumber(phoneOnly, defaultAreaCode);
                    result.RemainingText = input.Substring(0, areaCodeMatch.Index).Trim() + " " + areaCodeMatch.Groups[1].Value;
                    result.Success = true;
                    return result;
                }
                // Check if it's a suite indicator
                else if (suiteIndicatorsArea.Contains(lastWord))
                {
                    // This is a suite number, not an area code
                    _logger.LogInformation($"Detected suite indicator '{lastWord}' before number, not treating as area code");
                    
                    // Extract just the phone number (second part of the match)
                    var phoneOnly = areaCodeMatch.Groups[2].Value.Trim();
                    result.Phone = NormalizePhoneNumber(phoneOnly, defaultAreaCode);
                    result.RemainingText = input.Substring(0, areaCodeMatch.Index).Trim() + " " + areaCodeMatch.Groups[1].Value;
                    result.Success = true;
                    return result;
                }
                // Also check for suite indicators within the last few words
                else
                {
                    var suiteFound = false;
                    for (int i = Math.Max(0, wordsBeforeArea.Length - 4); i < wordsBeforeArea.Length; i++)
                    {
                        var word = wordsBeforeArea[i].ToLower().TrimEnd('.', ',');
                        if (suiteIndicatorsArea.Contains(word))
                        {
                            suiteFound = true;
                            _logger.LogInformation($"Found suite indicator '{word}' near potential area code, treating as suite number");
                            break;
                        }
                    }
                    
                    if (suiteFound)
                    {
                        // The first part is a suite number, not an area code
                        var phoneOnly = areaCodeMatch.Groups[2].Value.Trim();
                        result.Phone = NormalizePhoneNumber(phoneOnly, defaultAreaCode);
                        result.RemainingText = input.Substring(0, areaCodeMatch.Index).Trim() + " " + areaCodeMatch.Groups[1].Value;
                        result.Success = true;
                        return result;
                    }
                    else
                    {
                        // No suite or road indicator, treat as area code
                        _logger.LogInformation($"Accepting as area code: '{areaCodeMatch.Value}'");
                        var areaCode = areaCodeMatch.Groups[1].Value;
                        var localNumber = areaCodeMatch.Groups[2].Value;
                        result.Phone = NormalizePhoneNumber(areaCode + localNumber);
                        result.RemainingText = beforeMatch;
                        result.Success = true;
                        return result;
                    }
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
            var wordsRemaining = remaining.Split(' ');
            if (wordsRemaining.Length > 0)
            {
                var lastWord = wordsRemaining[^1];
                
                // Check if last word is a number that could be area code or suite
                // This could be 3 digits (typical area code) or more (suite number)
                if (Regex.IsMatch(lastWord, @"^\d+$"))
                {
                    var digitCount = lastWord.Length;
                    
                    // Check if word before the number indicates it's a suite/unit
                    // This is especially important for numbers that aren't exactly 3 digits
                    if (wordsRemaining.Length > 1)
                    {
                        var prevWord = wordsRemaining[^2].ToLower().TrimEnd('.', ',');
                        
                        // Get suite indicators and road indicators from database
                        var suiteIndicatorsPhone = await _referenceDataService.GetSuiteIndicatorsAsync();
                        var roadIndicators = await _referenceDataService.GetRoadIndicatorsAsync();
                        
                        _logger.LogDebug($"Checking if '{prevWord}' is a suite/road indicator (number: {lastWord}, digits: {digitCount})");
                        
                        // Check for suite indicators (Suite, Apt, Unit, etc.)
                        if (suiteIndicatorsPhone.Contains(prevWord))
                        {
                            // This is definitely a suite/unit number, not part of the phone
                            _logger.LogDebug($"Found suite indicator '{prevWord}' - treating {lastWord} as suite number");
                            result.Phone = NormalizePhoneNumber(phone, defaultAreaCode);
                            result.RemainingText = remaining;
                        }
                        // Check for road indicators (Highway, Route, etc.)
                        else if (roadIndicators.Contains(prevWord))
                        {
                            // This is a road number, not part of the phone
                            _logger.LogDebug($"Found road indicator '{prevWord}' - treating {lastWord} as road number");
                            result.Phone = NormalizePhoneNumber(phone, defaultAreaCode);
                            result.RemainingText = remaining;
                        }
                        // Special handling for multi-digit numbers after suite indicators
                        // Example: "Suite 209857-5732" where 2098 is the suite and 57-5732 is the phone
                        else if (digitCount > 3)
                        {
                            // Check if any suite indicator appears within the last few words
                            var suiteFound = false;
                            for (int i = Math.Max(0, wordsRemaining.Length - 4); i < wordsRemaining.Length - 1; i++)
                            {
                                var word = wordsRemaining[i].ToLower().TrimEnd('.', ',');
                                if (suiteIndicatorsPhone.Contains(word))
                                {
                                    suiteFound = true;
                                    _logger.LogDebug($"Found suite indicator '{word}' near end - treating {lastWord} as suite number");
                                    break;
                                }
                            }
                            
                            if (suiteFound)
                            {
                                // The number is a suite number
                                result.Phone = NormalizePhoneNumber(phone, defaultAreaCode);
                                result.RemainingText = remaining;
                            }
                            else
                            {
                                // No suite indicator, treat as potential area code if 3 digits
                                if (digitCount == 3)
                                {
                                    result.Phone = NormalizePhoneNumber(lastWord + phone);
                                    result.RemainingText = string.Join(" ", wordsRemaining.Take(wordsRemaining.Length - 1));
                                }
                                else
                                {
                                    // Not 3 digits and no suite indicator, leave as is
                                    result.Phone = NormalizePhoneNumber(phone, defaultAreaCode);
                                    result.RemainingText = remaining;
                                }
                            }
                        }
                        // Standard 3-digit number without suite/road indicator
                        else if (digitCount == 3)
                        {
                            // Likely an area code
                            result.Phone = NormalizePhoneNumber(lastWord + phone);
                            result.RemainingText = string.Join(" ", wordsRemaining.Take(wordsRemaining.Length - 1));
                        }
                        else
                        {
                            // Not 3 digits, leave as is
                            result.Phone = NormalizePhoneNumber(phone, defaultAreaCode);
                            result.RemainingText = remaining;
                        }
                    }
                    else if (digitCount == 3)
                    {
                        // Only one word before phone and it's 3 digits, assume it's area code
                        result.Phone = NormalizePhoneNumber(lastWord + phone);
                        result.RemainingText = string.Join(" ", wordsRemaining.Take(wordsRemaining.Length - 1));
                    }
                    else
                    {
                        // Single number that's not 3 digits, leave as is
                        result.Phone = NormalizePhoneNumber(phone, defaultAreaCode);
                        result.RemainingText = remaining;
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
        
        // First, handle hyphenated names properly
        // Normalize different hyphen formats: "C-C", "C - C", "C- C", "C -C" -> "C-C"
        name = Regex.Replace(name, @"\s*-\s*", "-");
        
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        if (parts.Length == 0)
            return;
            
        if (parts.Length == 1)
        {
            result.LastName = parts[0];
            result.FirstName = null;
            return;
        }
        
        // Special handling for patterns like "A Lucille Dieppe" (initial-firstname-lastname)
        // or "A Mwinkeu C" (initial-surname-initial)
        if (parts.Length == 3)
        {
            var first = parts[0];
            var middle = parts[1];
            var last = parts[2];
            
            // Check if first is an initial
            bool firstIsInitial = first.Length == 1 && char.IsLetter(first[0]);
            // Check if last is an initial
            bool lastIsInitial = last.Length == 1 && char.IsLetter(last[0]);
            
            if (firstIsInitial && !lastIsInitial)
            {
                // Pattern like "A Lucille Dieppe" -> LastName: "Dieppe", FirstName: "A Lucille"
                // or "A Smith-Jones William" -> LastName: "William", FirstName: "A Smith-Jones"
                result.LastName = last;
                result.FirstName = $"{first} {middle}";
                _logger.LogDebug($"Parsed initial-firstname-lastname pattern: '{name}' -> LastName: '{result.LastName}', FirstName: '{result.FirstName}'");
                return;
            }
            else if (firstIsInitial && lastIsInitial)
            {
                // Pattern like "A Mwinkeu C" -> LastName: "Mwinkeu", FirstName: "A C"
                result.LastName = middle;
                result.FirstName = $"{first} {last}";
                _logger.LogDebug($"Parsed initial-surname-initial pattern: '{name}' -> LastName: '{result.LastName}', FirstName: '{result.FirstName}'");
                return;
            }
        }
        
        // Handle hyphenated last names
        // Examples: "Adesola-Adeoye T" -> LastName: "Adesola-Adeoye", FirstName: "T"
        //          "T Adesola-Adeoye" -> LastName: "Adesola-Adeoye", FirstName: "T"
        var hyphenatedNamePattern = @"^(\w+)-(\w+)$";
        
        // Check if first part is hyphenated
        if (Regex.IsMatch(parts[0], hyphenatedNamePattern))
        {
            // First part is hyphenated, likely a last name
            result.LastName = parts[0];
            result.FirstName = FormatFirstName(string.Join(" ", parts.Skip(1)));
            _logger.LogDebug($"Parsed hyphenated last name first: '{name}' -> LastName: '{result.LastName}', FirstName: '{result.FirstName}'");
            return;
        }
        
        // Check if any part other than first is hyphenated
        for (int i = 1; i < parts.Length; i++)
        {
            if (Regex.IsMatch(parts[i], hyphenatedNamePattern))
            {
                // Found hyphenated name not in first position
                // If it's in second position and first is an initial, the hyphenated part is the last name
                if (i == 1 && IsInitialOrMultipleInitials(parts[0]))
                {
                    result.LastName = parts[1];
                    result.FirstName = parts[0];
                    if (parts.Length > 2)
                    {
                        // Add any remaining parts to first name
                        result.FirstName += " " + string.Join(" ", parts.Skip(2));
                    }
                    _logger.LogDebug($"Parsed initial then hyphenated last name: '{name}' -> LastName: '{result.LastName}', FirstName: '{result.FirstName}'");
                    return;
                }
            }
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
    
    private async Task<string> DetectAndCorrectOCRErrors(string input, string? province)
    {
        var words = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        // Pattern 1: Known name followed by "0" (likely OCR error for "O")
        // Example: "Adesina 0 382-4955" -> "Adesina O 382-4955"
        for (int i = 0; i < words.Length - 1; i++)
        {
            // Check if current word is a single "0" and previous word is a known name
            if (words[i] == "0" && i > 0)
            {
                var prevWordLower = words[i - 1].ToLower();
                
                // Check if previous word is a known last name or "both" type name
                var nameData = await _context.Set<WordData>()
                    .Where(w => w.WordLower == prevWordLower && 
                           (w.WordType == "last" || w.WordType == "both"))
                    .FirstOrDefaultAsync();
                
                if (nameData != null && nameData.WordCount > 10) // Confidence threshold
                {
                    // This is likely an OCR error: 0 should be O
                    words[i] = "O";
                    _logger.LogInformation($"OCR correction: Detected '0' after known name '{words[i-1]}', correcting to 'O'");
                }
            }
        }
        
        // Pattern 2: Initial+Number combined (e.g., "J7" should be "J 7")
        // Example: "Adery J7 Point Park" -> "Adery J 7 Point Park"
        for (int i = 0; i < words.Length; i++)
        {
            var word = words[i];
            
            // Check for pattern: single letter followed by numbers
            var combinedPattern = Regex.Match(word, @"^([A-Z])(\d+)$");
            if (combinedPattern.Success)
            {
                // Check if previous word is a known name (suggesting this is an initial+address)
                bool shouldSplit = false;
                
                if (i > 0)
                {
                    var prevWordLower = words[i - 1].ToLower();
                    var nameData = await _context.Set<WordData>()
                        .Where(w => w.WordLower == prevWordLower && 
                               (w.WordType == "last" || w.WordType == "both"))
                        .FirstOrDefaultAsync();
                    
                    if (nameData != null && nameData.WordCount > 5)
                    {
                        shouldSplit = true;
                    }
                }
                
                // Also split if the next word looks like part of an address
                if (!shouldSplit && i < words.Length - 1)
                {
                    var nextWord = words[i + 1];
                    // Common street names or types
                    if (Regex.IsMatch(nextWord, @"^(Park|Street|St|Avenue|Ave|Drive|Dr|Road|Rd|Point|Place|Pl)$", RegexOptions.IgnoreCase))
                    {
                        shouldSplit = true;
                    }
                }
                
                if (shouldSplit)
                {
                    // Split the combined initial+number
                    var letter = combinedPattern.Groups[1].Value;
                    var number = combinedPattern.Groups[2].Value;
                    
                    // Create new array with the split
                    var newWords = new List<string>();
                    for (int j = 0; j < i; j++)
                        newWords.Add(words[j]);
                    
                    newWords.Add(letter);
                    newWords.Add(number);
                    
                    for (int j = i + 1; j < words.Length; j++)
                        newWords.Add(words[j]);
                    
                    words = newWords.ToArray();
                    _logger.LogInformation($"OCR correction: Split '{word}' into '{letter}' and '{number}'");
                    
                    // Increment i since we added an extra word
                    i++;
                }
            }
        }
        
        // Pattern 3: Check for other common OCR errors
        // "l" misread as "1", "I" misread as "1", etc. in name context
        for (int i = 0; i < words.Length; i++)
        {
            // If we have a single "1" in a position that should be a name
            if (words[i] == "1")
            {
                // Check if surrounding context suggests this should be an initial
                bool likelyInitial = false;
                
                // If previous word is a known name and next is not a number
                if (i > 0 && i < words.Length - 1)
                {
                    var prevWordLower = words[i - 1].ToLower();
                    var nextWord = words[i + 1];
                    
                    // Check if previous is a known name
                    var nameData = await _context.Set<WordData>()
                        .Where(w => w.WordLower == prevWordLower && 
                               (w.WordType == "first" || w.WordType == "last" || w.WordType == "both"))
                        .FirstOrDefaultAsync();
                    
                    // If previous is a name and next is not a number, likely an initial
                    if (nameData != null && !Regex.IsMatch(nextWord, @"^\d"))
                    {
                        likelyInitial = true;
                    }
                }
                
                if (likelyInitial)
                {
                    words[i] = "I"; // Most common correction for "1" in name context
                    _logger.LogInformation($"OCR correction: Detected '1' in name context, correcting to 'I'");
                }
            }
        }
        
        return string.Join(" ", words);
    }
}