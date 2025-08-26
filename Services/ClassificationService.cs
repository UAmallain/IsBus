using IsBus.Data;
using IsBus.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace IsBus.Services;

public class ClassificationService : IClassificationService
{
    private readonly PhonebookContext _context;
    private readonly ILogger<ClassificationService> _logger;
    private readonly IWordFrequencyService _wordFrequencyService;
    
    // Absolute business indicators - these override everything else
    private readonly string[] _absoluteBusinessIndicators = new[]
    {
        "inc", "incorporated", "corp", "corporation", "ltd", "limited", 
        "llc", "llp", "lp", "plc", "gmbh", "ag", "sa", "nv", "bv"
    };
    
    // Patterns that strongly indicate business
    private readonly string[] _strongBusinessPatterns = new[]
    {
        @"\b(enterprises|holdings|group|partners|associates|solutions|services)\b",
        @"\b(consulting|management|marketing|agency|studio|clinic|center|centre)\b",
        @"\b(restaurant|cafe|bistro|grill|pizza|sushi|bakery|deli|pub|bar|tavern)\b",
        @"\b(shop|store|mart|market|boutique|outlet|supply|supplies)\b",
        @"\b(salon|spa|fitness|gym|wellness|health|medical|dental|pharmacy)\b",
        @"\b(hotel|motel|inn|lodge|resort|suites)\b",
        @"\b(automotive|motors|auto|garage|repair|towing)\b",
        @"\b(construction|contracting|roofing|plumbing|electric|hvac)\b",
        @"\b(real estate|realty|properties|property management)\b"
    };
    
    // Patterns that indicate residential (family names)
    private readonly string[] _residentialPatterns = new[]
    {
        @"^(the\s+)?[a-z]+s$", // The Smiths, Johnsons
        @"\b(family|residence|household)\b",
        @"\b(mr|mrs|ms|miss|dr|prof)\b\s+[a-z]+",
        @"^[a-z]+\s+(and|&)\s+[a-z]+$" // John and Mary, Smith & Jones
    };

    public ClassificationService(
        PhonebookContext context,
        ILogger<ClassificationService> logger,
        IWordFrequencyService wordFrequencyService)
    {
        _context = context;
        _logger = logger;
        _wordFrequencyService = wordFrequencyService;
    }

    public async Task<ClassificationResult> ClassifyAsync(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return new ClassificationResult
            {
                Input = input,
                Classification = "unknown",
                Confidence = 0,
                IsBusiness = false,
                IsResidential = false,
                Reason = "Empty input"
            };
        }

        var normalizedInput = input.Trim().ToLowerInvariant();
        var words = normalizedInput.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        var result = new ClassificationResult
        {
            Input = input,
            Words = words.ToList()
        };

        // Initialize scoring components
        var scores = new Dictionary<string, double>();
        
        // CHECK FOR ABSOLUTE BUSINESS INDICATORS FIRST
        foreach (var word in words)
        {
            if (_absoluteBusinessIndicators.Contains(word.ToLower().Trim('.')))
            {
                scores["absolute_business"] = 100;
                
                // This is definitely a business - override everything
                result.Classification = "business";
                result.Confidence = 100;
                result.IsBusiness = true;
                result.IsResidential = false;
                result.Reason = $"Contains corporate identifier: {word.ToUpper()}";
                result.DetailedScores = scores;
                result.BusinessScore = 100;
                result.ResidentialScore = 0;
                
                _logger.LogInformation($"Classification: {input} -> BUSINESS (absolute indicator: {word})");
                return result;
            }
        }
        
        // 1. Check for strong business patterns
        var businessPatternScore = CheckBusinessPatterns(normalizedInput);
        scores["business_patterns"] = businessPatternScore;
        
        // 2. Check for valid residential pattern FIRST
        var isValidResidential = await IsValidResidentialPattern(words);
        scores["valid_residential_pattern"] = isValidResidential ? 40 : -30; // Penalty if not valid pattern
        
        // 3. Check for residential patterns
        var residentialPatternScore = CheckResidentialPatterns(normalizedInput);
        scores["residential_patterns"] = residentialPatternScore;
        
        // 4. Comprehensive word analysis - check BOTH words and names tables
        var wordAnalysis = await AnalyzeWordsComprehensive(words);
        scores["business_word_score"] = wordAnalysis.BusinessWordScore;
        scores["name_score"] = wordAnalysis.NameScore;
        scores["word_vs_name_ratio"] = wordAnalysis.WordToNameRatio;
        
        // 5. Analyze first word
        var firstWordAnalysis = await AnalyzeFirstWord(words.FirstOrDefault());
        
        // Only give residential points if it's ACTUALLY a valid residential pattern
        if (firstWordAnalysis.IsLastName && wordAnalysis.FirstWordIsName && isValidResidential)
        {
            scores["first_word_lastname"] = 30;
        }
        else
        {
            scores["first_word_lastname"] = 0;
        }
        
        // 5. Check for possessive patterns
        var possessiveAnalysis = AnalyzePossessive(normalizedInput);
        scores["possessive_business"] = possessiveAnalysis.IsBusinessPossessive ? 40 : 0;
        scores["possessive_simple"] = possessiveAnalysis.IsSimplePossessive ? -20 : 0;
        
        // 6. Check word count and structure
        var structureScore = AnalyzeStructure(words);
        scores["structure"] = structureScore;
        
        // 7. Special case: "Name's Business Type" pattern
        if (possessiveAnalysis.IsBusinessPossessive && firstWordAnalysis.IsLastName)
        {
            scores["name_business_pattern"] = 60;
        }
        
        // 8. Apply word vs name ratio logic
        // If words appear more in business context than as names, it's likely business
        if (wordAnalysis.WordToNameRatio > 2.0)
        {
            scores["high_business_ratio"] = 30;
        }
        else if (wordAnalysis.WordToNameRatio > 1.0)
        {
            scores["moderate_business_ratio"] = 15;
        }
        
        // Calculate final scores
        var businessScore = scores["business_patterns"] + 
                          scores["possessive_business"] + 
                          scores["business_word_score"] +
                          scores.GetValueOrDefault("name_business_pattern", 0) +
                          scores.GetValueOrDefault("high_business_ratio", 0) +
                          scores.GetValueOrDefault("moderate_business_ratio", 0) +
                          (scores["structure"] > 0 ? scores["structure"] : 0) +
                          (scores["valid_residential_pattern"] < 0 ? Math.Abs(scores["valid_residential_pattern"]) : 0);
                          
        var residentialScore = scores["residential_patterns"] + 
                             scores["first_word_lastname"] + 
                             scores["possessive_simple"] +
                             scores["name_score"] +
                             (scores["valid_residential_pattern"] > 0 ? scores["valid_residential_pattern"] : 0);
        
        // Normalize scores to 0-100
        var totalScore = businessScore + residentialScore;
        if (totalScore > 0)
        {
            businessScore = (businessScore / totalScore) * 100;
            residentialScore = (residentialScore / totalScore) * 100;
        }
        
        // Determine classification
        if (businessScore > residentialScore)
        {
            result.Classification = "business";
            result.Confidence = Math.Min(100, (int)businessScore);
            result.IsBusiness = true;
            result.IsResidential = false;
            result.Reason = DetermineBusinessReason(scores, possessiveAnalysis, wordAnalysis);
        }
        else
        {
            result.Classification = "residential";
            result.Confidence = Math.Min(100, (int)residentialScore);
            result.IsBusiness = false;
            result.IsResidential = true;
            result.Reason = DetermineResidentialReason(scores, firstWordAnalysis, wordAnalysis);
        }
        
        // Add detailed scoring for debugging
        result.DetailedScores = scores;
        result.BusinessScore = (int)businessScore;
        result.ResidentialScore = (int)residentialScore;
        
        _logger.LogInformation($"Classification: {input} -> {result.Classification} ({result.Confidence}%)");
        
        return result;
    }
    
    private double CheckBusinessPatterns(string input)
    {
        double score = 0;
        foreach (var pattern in _strongBusinessPatterns)
        {
            if (Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase))
            {
                score += 30;
            }
        }
        return Math.Min(score, 80); // Cap at 80
    }
    
    private double CheckResidentialPatterns(string input)
    {
        double score = 0;
        foreach (var pattern in _residentialPatterns)
        {
            if (Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase))
            {
                score += 25;
            }
        }
        return Math.Min(score, 60); // Cap at 60
    }
    
    private async Task<bool> IsValidResidentialPattern(string[] words)
    {
        // Residential MUST have at least 2 components (first + last)
        if (words.Length < 2)
            return false;
        
        // Check for patterns like:
        // - "John Smith" (first last)
        // - "Smith John" (last first - also valid!)
        // - "J Smith" (initial last)
        // - "J. Smith" (initial with period last)
        // - "John & Mary Smith" (multiple first names)
        // - "J & M Smith" (multiple initials)
        // - "John and Mary Smith"
        
        // Check if it matches initial patterns
        var hasInitial = Regex.IsMatch(words[0], @"^[a-z]\.?$", RegexOptions.IgnoreCase);
        
        // Check for ampersand/and patterns
        var hasMultipleNames = words.Any(w => w == "&" || w.ToLower() == "and");
        
        if (hasMultipleNames)
        {
            // Pattern like "John & Mary Smith" or "J & M Smith"
            // Need at least one word after the connector
            var andIndex = Array.FindIndex(words, w => w == "&" || w.ToLower() == "and");
            if (andIndex > 0 && andIndex < words.Length - 1)
            {
                // Check if last word is a potential last name
                var lastWord = words[words.Length - 1];
                var lastName = await _context.Names
                    .FirstOrDefaultAsync(n => n.NameLower == lastWord.ToLower() && 
                                             (n.NameType == "last" || n.NameType == "both"));
                return lastName != null;
            }
        }
        
        // Standard pattern: first/initial + last OR last + first
        if (words.Length == 2)
        {
            // Get ALL name records for each word (could be multiple types)
            var word0Names = await _context.Names
                .Where(n => n.NameLower == words[0].ToLower())
                .ToListAsync();
            
            var word1Names = await _context.Names
                .Where(n => n.NameLower == words[1].ToLower())
                .ToListAsync();
            
            _logger.LogDebug($"IsValidResidentialPattern: word0={words[0]} found={word0Names.Count} records");
            _logger.LogDebug($"IsValidResidentialPattern: word1={words[1]} found={word1Names.Count} records");
            
            // If both words are in the names table, check patterns
            if (word0Names.Any() && word1Names.Any())
            {
                // Get max counts for each word
                var word0MaxCount = word0Names.Max(n => n.NameCount);
                var word1MaxCount = word1Names.Max(n => n.NameCount);
                
                // CRITICAL: Check if these are REAL names or just noise
                // A count of 1-5 in names table is likely noise/error, not a real name
                const int MIN_NAME_COUNT = 10;
                
                // Also check business word counts
                var word0Business = await _context.WordData
                    .FirstOrDefaultAsync(w => w.WordLower == words[0].ToLower() && w.WordType == "business");
                var word1Business = await _context.WordData
                    .FirstOrDefaultAsync(w => w.WordLower == words[1].ToLower() && w.WordType == "business");
                
                // If either word is overwhelmingly business (50x more), not residential
                if ((word0Business != null && word0MaxCount < MIN_NAME_COUNT && word0Business.WordCount > word0MaxCount * 20) ||
                    (word1Business != null && word1MaxCount < MIN_NAME_COUNT && word1Business.WordCount > word1MaxCount * 20))
                {
                    _logger.LogDebug("One or both words are overwhelmingly business words, not valid residential");
                    return false;
                }
                
                // Check if both have "both" type entries with substantial counts
                var word0Both = word0Names.Any(n => n.NameType == "both" && n.NameCount >= MIN_NAME_COUNT);
                var word1Both = word1Names.Any(n => n.NameType == "both" && n.NameCount >= MIN_NAME_COUNT);
                
                if (word0Both && word1Both)
                {
                    _logger.LogDebug("Both words have type 'both' with substantial counts - valid residential pattern");
                    return true; // Valid residential pattern
                }
                
                // Check both patterns with minimum count threshold:
                // Pattern 1: FirstName LastName
                var word0CanBeFirst = word0Names.Any(n => (n.NameType == "first" || n.NameType == "both") && n.NameCount >= MIN_NAME_COUNT);
                var word1CanBeLast = word1Names.Any(n => (n.NameType == "last" || n.NameType == "both") && n.NameCount >= MIN_NAME_COUNT);
                var pattern1Valid = word0CanBeFirst && word1CanBeLast;
                
                // Pattern 2: LastName FirstName  
                var word0CanBeLast = word0Names.Any(n => (n.NameType == "last" || n.NameType == "both") && n.NameCount >= MIN_NAME_COUNT);
                var word1CanBeFirst = word1Names.Any(n => (n.NameType == "first" || n.NameType == "both") && n.NameCount >= MIN_NAME_COUNT);
                var pattern2Valid = word0CanBeLast && word1CanBeFirst;
                
                _logger.LogDebug($"Pattern1 (first+last): {pattern1Valid}, Pattern2 (last+first): {pattern2Valid}");
                return pattern1Valid || pattern2Valid;
            }
            
            // Check for initial + name pattern
            if (hasInitial && word1Names.Any() && word1Names.Any(n => n.NameType == "last" || n.NameType == "both"))
            {
                return true;
            }
            
            _logger.LogDebug("Not a valid residential pattern");
            return false;
        }
        
        // For longer patterns, check if it ends with a last name
        if (words.Length > 2)
        {
            var lastWord = words[words.Length - 1];
            var lastName = await _context.Names
                .FirstOrDefaultAsync(n => n.NameLower == lastWord.ToLower() && 
                                         (n.NameType == "last" || n.NameType == "both"));
            
            // Check if first word is a name or initial
            var firstName = await _context.Names
                .FirstOrDefaultAsync(n => n.NameLower == words[0].ToLower() && 
                                         (n.NameType == "first" || n.NameType == "both"));
            
            var firstIsInitial = Regex.IsMatch(words[0], @"^[a-z]\.?$", RegexOptions.IgnoreCase);
            
            return lastName != null && (firstName != null || firstIsInitial);
        }
        
        return false;
    }
    
    private async Task<FirstWordAnalysis> AnalyzeFirstWord(string? firstWord)
    {
        var analysis = new FirstWordAnalysis();
        if (string.IsNullOrEmpty(firstWord))
            return analysis;
        
        var name = await _context.Names
            .FirstOrDefaultAsync(n => n.NameLower == firstWord.ToLower());
        
        if (name != null)
        {
            analysis.IsLastName = name.NameType == "last" || name.NameType == "both";
            analysis.IsFirstName = name.NameType == "first" || name.NameType == "both";
            analysis.NameCount = name.NameCount;
            
            // Strong last name at the beginning suggests residential
            if (analysis.IsLastName && name.NameCount > 100)
            {
                analysis.Confidence = Math.Min(80, name.NameCount / 10);
            }
        }
        
        return analysis;
    }
    
    private PossessiveAnalysis AnalyzePossessive(string input)
    {
        var analysis = new PossessiveAnalysis();
        
        // Check for possessive pattern
        var possessiveMatch = Regex.Match(input, @"(\w+)'s\s+(.+)", RegexOptions.IgnoreCase);
        if (possessiveMatch.Success)
        {
            var afterPossessive = possessiveMatch.Groups[2].Value.ToLower();
            
            // Check if what comes after possessive is a business type
            var businessTypeWords = new[] 
            {
                "pizza", "restaurant", "cafe", "shop", "store", "salon", "spa",
                "garage", "auto", "mart", "market", "grill", "deli", "bakery",
                "services", "repair", "clinic", "dental", "contracting", "plumbing",
                "electric", "roofing", "landscaping", "cleaning", "catering"
            };
            
            analysis.IsBusinessPossessive = businessTypeWords.Any(w => afterPossessive.Contains(w));
            analysis.IsSimplePossessive = !analysis.IsBusinessPossessive;
            analysis.PossessiveWord = possessiveMatch.Groups[1].Value;
        }
        
        return analysis;
    }
    
    private async Task<WordAnalysisResult> AnalyzeWordsComprehensive(string[] words)
    {
        var analysis = new WordAnalysisResult();
        
        double totalBusinessScore = 0;
        double totalNameScore = 0;
        bool hasValidNamePattern = false;
        
        // First check if this looks like a name pattern
        if (words.Length == 2)
        {
            var word0Names = await _context.Names
                .Where(n => n.NameLower == words[0].ToLower())
                .ToListAsync();
            var word1Names = await _context.Names
                .Where(n => n.NameLower == words[1].ToLower())
                .ToListAsync();
            
            // Check if it's FirstName LastName or LastName FirstName
            if (word0Names.Any() && word1Names.Any())
            {
                const int MIN_NAME_COUNT = 10;
                
                // Get max counts
                var word0MaxCount = word0Names.Max(n => n.NameCount);
                var word1MaxCount = word1Names.Max(n => n.NameCount);
                
                // Check business word counts too
                var word0Business = await _context.WordData
                    .FirstOrDefaultAsync(w => w.WordLower == words[0].ToLower() && w.WordType == "business");
                var word1Business = await _context.WordData
                    .FirstOrDefaultAsync(w => w.WordLower == words[1].ToLower() && w.WordType == "business");
                
                // Don't treat as valid name pattern if words are overwhelmingly business
                bool word0IsRealName = word0MaxCount >= MIN_NAME_COUNT || 
                                       (word0Business == null || word0Business.WordCount < word0MaxCount * 5);
                bool word1IsRealName = word1MaxCount >= MIN_NAME_COUNT || 
                                       (word1Business == null || word1Business.WordCount < word1MaxCount * 5);
                
                if (word0IsRealName && word1IsRealName)
                {
                    // When BOTH words have type "both" with substantial counts
                    var word0Both = word0Names.Any(n => n.NameType == "both" && n.NameCount >= MIN_NAME_COUNT);
                    var word1Both = word1Names.Any(n => n.NameType == "both" && n.NameCount >= MIN_NAME_COUNT);
                    
                    if (word0Both && word1Both)
                    {
                        hasValidNamePattern = true;
                    }
                    else
                    {
                        var isFirstLast = word0Names.Any(n => (n.NameType == "first" || n.NameType == "both") && n.NameCount >= MIN_NAME_COUNT) &&
                                         word1Names.Any(n => (n.NameType == "last" || n.NameType == "both") && n.NameCount >= MIN_NAME_COUNT);
                        var isLastFirst = word0Names.Any(n => (n.NameType == "last" || n.NameType == "both") && n.NameCount >= MIN_NAME_COUNT) &&
                                         word1Names.Any(n => (n.NameType == "first" || n.NameType == "both") && n.NameCount >= MIN_NAME_COUNT);
                        
                        hasValidNamePattern = isFirstLast || isLastFirst;
                    }
                }
            }
        }
        
        for (int i = 0; i < words.Length; i++)
        {
            var word = words[i].ToLower();
            
            // Check words table (business keywords)
            var businessWord = await _context.WordData
                .FirstOrDefaultAsync(w => w.WordLower == word && w.WordType == "business");
            
            // Check names table - get ALL entries for this word
            var names = await _context.Names
                .Where(n => n.NameLower == word)
                .ToListAsync();
            
            if (businessWord != null && !hasValidNamePattern)
            {
                // Only count business score if not a valid name pattern
                var wordScore = Math.Min(businessWord.WordCount / 100.0, 20);
                totalBusinessScore += wordScore;
                analysis.BusinessWords.Add(word, businessWord.WordCount);
            }
            
            if (names.Any())
            {
                // Get the highest count among all name types
                var maxNameCount = names.Max(n => n.NameCount);
                var primaryNameType = names.OrderByDescending(n => n.NameCount).First().NameType;
                
                var nameScore = Math.Min(maxNameCount / 100.0, 15);
                
                // If we have a valid name pattern, always count name scores
                if (hasValidNamePattern)
                {
                    totalNameScore += nameScore * 2; // Boost name score for valid patterns
                    analysis.Names.Add(word, maxNameCount);
                }
                // If name count is HIGHER than business count, it's primarily a name
                else if (businessWord == null || maxNameCount >= businessWord.WordCount)
                {
                    totalNameScore += nameScore * 1.5; // Boost when name count is higher
                    analysis.Names.Add(word, maxNameCount);
                }
                // If business count is significantly higher, check the ratio
                else if (businessWord != null && businessWord.WordCount > maxNameCount * 10)
                {
                    // This is overwhelmingly a business word (10x more business usage)
                    // Don't count as name
                }
                else
                {
                    // Mixed usage - count both but reduced name score
                    totalNameScore += nameScore * 0.5;
                    analysis.Names.Add(word, maxNameCount);
                }
                
                if (i == 0)
                {
                    analysis.FirstWordIsName = true;
                    analysis.FirstNameType = primaryNameType;
                }
            }
        }
        
        analysis.BusinessWordScore = totalBusinessScore;
        analysis.NameScore = totalNameScore;
        
        // Calculate ratio - but don't penalize valid name patterns
        if (hasValidNamePattern)
        {
            analysis.WordToNameRatio = 0; // Don't apply ratio penalty for valid name patterns
        }
        else if (totalNameScore > 0)
        {
            analysis.WordToNameRatio = totalBusinessScore / totalNameScore;
        }
        else if (totalBusinessScore > 0)
        {
            analysis.WordToNameRatio = 100; // All business, no names
        }
        else
        {
            analysis.WordToNameRatio = 0;
        }
        
        return analysis;
    }
    
    private async Task<NameAnalysis> AnalyzeNamesInInput(string[] words)
    {
        var analysis = new NameAnalysis();
        
        foreach (var word in words)
        {
            var name = await _context.Names
                .FirstOrDefaultAsync(n => n.NameLower == word.ToLower());
            
            if (name != null)
            {
                analysis.NameCount++;
                if (analysis.NameCount == 1 && words[0].ToLower() == word.ToLower())
                {
                    analysis.StartsWithName = true;
                    analysis.FirstNameType = name.NameType;
                }
            }
        }
        
        return analysis;
    }
    
    private double AnalyzeStructure(string[] words)
    {
        // CRITICAL RULE: Single word is ALWAYS business
        // Residential must have both first and last name components
        if (words.Length == 1)
            return 50; // Strong business indicator - residential needs first + last
        
        if (words.Length == 2)
        {
            // Could be "FirstName LastName" or "Business Type"
            // Need to check if it matches name pattern
            return 0; // Neutral - needs further analysis
        }
        
        if (words.Length >= 3 && words.Length <= 5)
            return 15; // Moderate business indicator
        
        if (words.Length > 5)
            return 25; // Strong business indicator
        
        return 0;
    }
    
    private string DetermineBusinessReason(
        Dictionary<string, double> scores,
        PossessiveAnalysis possessive,
        WordAnalysisResult wordAnalysis)
    {
        var reasons = new List<string>();
        
        if (scores.GetValueOrDefault("absolute_business", 0) > 0)
            return "Contains corporate identifier";
        
        if (scores["business_patterns"] > 30)
            reasons.Add("contains business keywords");
        
        if (scores.GetValueOrDefault("name_business_pattern", 0) > 0)
            reasons.Add($"follows pattern: {possessive.PossessiveWord}'s [business type]");
        
        if (scores.GetValueOrDefault("high_business_ratio", 0) > 0)
            reasons.Add($"words have business usage {wordAnalysis.WordToNameRatio:F1}x higher than name usage");
        
        if (scores["business_word_score"] > 20)
            reasons.Add("high frequency business words");
        
        if (reasons.Count == 0)
            reasons.Add("general business pattern");
        
        return string.Join("; ", reasons);
    }
    
    private string DetermineResidentialReason(
        Dictionary<string, double> scores,
        FirstWordAnalysis firstWord,
        WordAnalysisResult wordAnalysis)
    {
        var reasons = new List<string>();
        
        if (scores["first_word_lastname"] > 0)
            reasons.Add("starts with last name");
        
        if (scores["residential_patterns"] > 0)
            reasons.Add("matches residential pattern");
        
        if (scores["name_score"] > 30)
            reasons.Add($"contains {wordAnalysis.Names.Count} personal names");
        
        if (reasons.Count == 0)
            reasons.Add("general residential pattern");
        
        return string.Join("; ", reasons);
    }
}

// Supporting classes
public class ClassificationResult
{
    public string Input { get; set; } = string.Empty;
    public string Classification { get; set; } = "unknown"; // "business", "residential", or "unknown"
    public int Confidence { get; set; }
    public bool IsBusiness { get; set; }
    public bool IsResidential { get; set; }
    public string Reason { get; set; } = string.Empty;
    public List<string> Words { get; set; } = new();
    public Dictionary<string, double> DetailedScores { get; set; } = new();
    public int BusinessScore { get; set; }
    public int ResidentialScore { get; set; }
}

public class FirstWordAnalysis
{
    public bool IsLastName { get; set; }
    public bool IsFirstName { get; set; }
    public int NameCount { get; set; }
    public double Confidence { get; set; }
}

public class PossessiveAnalysis
{
    public bool IsBusinessPossessive { get; set; }
    public bool IsSimplePossessive { get; set; }
    public string PossessiveWord { get; set; } = string.Empty;
}

public class WordAnalysisResult
{
    public double BusinessWordScore { get; set; }
    public double NameScore { get; set; }
    public double WordToNameRatio { get; set; }
    public Dictionary<string, int> BusinessWords { get; set; } = new();
    public Dictionary<string, int> Names { get; set; } = new();
    public bool FirstWordIsName { get; set; }
    public string FirstNameType { get; set; } = string.Empty;
}

public class NameAnalysis
{
    public int NameCount { get; set; }
    public bool StartsWithName { get; set; }
    public string FirstNameType { get; set; } = string.Empty;
}