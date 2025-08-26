using System.Text.RegularExpressions;
using IsBus.Models;

namespace IsBus.Services;

public class BusinessNameDetectionService : IBusinessNameDetectionService
{
    private readonly IBusinessIndicatorService _indicatorService;
    private readonly IWordProcessingService _wordProcessingService;
    private readonly IWordFrequencyService _frequencyService;
    private readonly ILogger<BusinessNameDetectionService> _logger;

    public BusinessNameDetectionService(
        IBusinessIndicatorService indicatorService,
        IWordProcessingService wordProcessingService,
        IWordFrequencyService frequencyService,
        ILogger<BusinessNameDetectionService> logger)
    {
        _indicatorService = indicatorService;
        _wordProcessingService = wordProcessingService;
        _frequencyService = frequencyService;
        _logger = logger;
    }

    public async Task<BusinessNameCheckResponse> CheckBusinessNameAsync(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return new BusinessNameCheckResponse
            {
                Input = input ?? string.Empty,
                IsBusinessName = false,
                Confidence = 0,
                MatchedIndicators = new List<string>(),
                WordsProcessed = 0
            };
        }

        var scoringResult = await CalculateConfidenceAsync(input);
        
        var response = new BusinessNameCheckResponse
        {
            Input = input,
            IsBusinessName = scoringResult.Confidence >= 55,  // Lowered from 65 to 55 for better detection
            Confidence = Math.Round(scoringResult.Confidence, 1),
            MatchedIndicators = scoringResult.MatchedIndicators,
            WordsProcessed = 0
        };

        if (response.IsBusinessName)
        {
            try
            {
                var wordsProcessed = await _wordProcessingService.ProcessBusinessNameWordsAsync(input);
                response.WordsProcessed = wordsProcessed;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing words for business name: {Input}", input);
            }
        }

        _logger.LogInformation("Business name check completed: {Input}, IsBusinessName: {IsBusinessName}, Confidence: {Confidence}",
            input, response.IsBusinessName, response.Confidence);

        return response;
    }

    private async Task<(double Confidence, List<string> MatchedIndicators)> CalculateConfidenceAsync(string input)
    {
        double score = 0;
        var matchedIndicators = new List<string>();
        var words = ExtractWords(input);

        // Check for possessive pattern FIRST - automatic business name
        if (ContainsPossessive(input))
        {
            matchedIndicators.Add("Possessive pattern");
            
            // If it's JUST a possessive (like "John's"), minimum score
            if (Regex.IsMatch(input.Trim(), @"^\w+'s?$", RegexOptions.IgnoreCase))
            {
                return (65, matchedIndicators); // Minimum passing score
            }
            
            // Otherwise, give a strong base score for possessive patterns
            score += 40;
        }

        // Get word frequencies from database - THIS IS THE KEY PART!
        var wordFrequencies = await _frequencyService.GetWordFrequenciesAsync(words);
        
        // Log if we didn't get any word frequencies (database lookup failed)
        if (!wordFrequencies.Any() || wordFrequencies.Values.All(v => v == 0))
        {
            _logger.LogWarning("No word frequencies retrieved from database for: {Words}", string.Join(", ", words));
        }
        
        // Calculate database-based score FIRST
        score += CalculateDatabaseScore(words, wordFrequencies, matchedIndicators);

        score += CalculateBusinessIndicatorScore(words, matchedIndicators);
        
        score += CalculateCapitalizationScore(input, words);
        
        score += CalculateStructureScore(input, words);
        
        score += CalculateSeparatorScore(input);
        
        score += CalculateLengthComplexityScore(input, words);
        
        // Remove or reduce weight of hardcoded patterns since we have database
        // score += CalculateBusinessPatternScore(input, words, matchedIndicators);

        return (Math.Min(score, 100), matchedIndicators);
    }

    private bool ContainsPossessive(string input)
    {
        return Regex.IsMatch(input, @"'s?\b", RegexOptions.IgnoreCase);
    }

    private double CalculateDatabaseScore(List<string> words, Dictionary<string, int> wordFrequencies, List<string> matchedIndicators)
    {
        double score = 0;
        int highFreqWords = 0;
        int mediumFreqWords = 0;
        int totalFrequency = 0;
        int maxFrequency = 0;

        foreach (var word in words)
        {
            // Clean possessive forms for lookup (John's -> johns)
            var cleanWord = word.Replace("'s", "").Replace("'", "");
            var lowerWord = cleanWord.ToLowerInvariant();
            
            if (wordFrequencies.TryGetValue(lowerWord, out int frequency))
            {
                totalFrequency += frequency;
                maxFrequency = Math.Max(maxFrequency, frequency);
                
                if (frequency >= 1000)  // Very high frequency business word
                {
                    highFreqWords++;
                    score += 35;  // Increased from 25
                    matchedIndicators.Add($"{word} (freq:{frequency})");
                }
                else if (frequency >= 500)  // High frequency business word
                {
                    highFreqWords++;
                    score += 25;
                    matchedIndicators.Add($"{word} (freq:{frequency})");
                }
                else if (frequency >= 100)  // Medium frequency business word
                {
                    mediumFreqWords++;
                    score += 15;
                    matchedIndicators.Add($"{word} (freq:{frequency})");
                }
                else if (frequency >= 10)  // Low but present
                {
                    score += 5;
                }
            }
        }

        // Special case: One VERY strong business word (like Pizza, Restaurant, Bank)
        if (maxFrequency >= 1000)
        {
            score += 25;  // Strong business indicator deserves extra weight
        }
        
        // Bonus for multiple high-frequency words
        if (highFreqWords >= 2)
        {
            score += 20;  // Strong signal - multiple common business words
        }
        else if (highFreqWords == 1 && mediumFreqWords >= 1)
        {
            score += 10;  // Good signal - mix of common business words
        }

        // Log for debugging
        _logger.LogInformation("Database scoring for '{Input}': High freq: {High}, Medium freq: {Medium}, Max freq: {Max}, Total freq: {Total}, Score: {Score}",
            string.Join(" ", words), highFreqWords, mediumFreqWords, maxFrequency, totalFrequency, score);

        return Math.Min(score, 70);  // Increased cap from 60 to 70
    }

    private double CalculateBusinessIndicatorScore(List<string> words, List<string> matchedIndicators)
    {
        double score = 0;
        int secondaryCount = 0;

        foreach (var word in words)
        {
            if (_indicatorService.IsPrimarySuffix(word))
            {
                score += 30;  // Increased from 25
                matchedIndicators.Add(word);
            }
            else if (_indicatorService.IsSecondaryIndicator(word))
            {
                // Hospitality and retail indicators are VERY strong signals
                var strongBusinessWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase) 
                {
                    "Inn", "Hotel", "Motel", "Lodge", "Resort", "Suites",
                    "Restaurant", "Cafe", "Bistro", "Bar", "Pub", "Grill",
                    "Store", "Shop", "Mart", "Market", "Pharmacy", "Clinic",
                    "Bank", "Motors", "Auto", "Garage", "Pizza", "Dental"
                };
                
                if (strongBusinessWords.Contains(word))
                {
                    score += 35;  // Strong business words get extra weight
                    matchedIndicators.Add($"{word} (strong)");
                }
                else
                {
                    // First secondary indicator gets full points, subsequent ones get less
                    score += (secondaryCount == 0) ? 20 : 10;
                    matchedIndicators.Add(word);
                }
                secondaryCount++;
            }
        }

        // Having multiple business indicators is a strong signal
        if (matchedIndicators.Count >= 2)
        {
            score += 10;  // Bonus for multiple indicators
        }

        return Math.Min(score, 50);  // Increased cap from 40 to 50
    }

    private double CalculateCapitalizationScore(string input, List<string> words)
    {
        double score = 0;

        if (words.Count > 0)
        {
            var capitalizedWords = words.Count(w => 
                !string.IsNullOrEmpty(w) && 
                char.IsUpper(w[0]) && 
                !_indicatorService.IsStopWord(w));

            var capitalizedRatio = (double)capitalizedWords / words.Count;

            if (capitalizedRatio >= 0.8)
                score += 15;
            else if (capitalizedRatio >= 0.6)
                score += 10;
            else if (capitalizedRatio >= 0.4)
                score += 5;
        }

        if (Regex.IsMatch(input, @"^[A-Z]{2,}(?:\s+[A-Z]{2,})*$"))
        {
            score += 5;
        }

        return Math.Min(score, 20);
    }

    private double CalculateStructureScore(string input, List<string> words)
    {
        double score = 0;

        if (words.Count >= 2 && words.Count <= 6)
            score += 10;
        else if (words.Count == 1 && input.Length >= 5)
            score += 5;
        else if (words.Count > 6 && words.Count <= 8)
            score += 3;

        if (Regex.IsMatch(input, @"^[A-Z][a-z]+(?:\s+[A-Z][a-z]+)*$"))
        {
            score += 5;
        }

        if (Regex.IsMatch(input, @"\b(?:and|&)\b", RegexOptions.IgnoreCase))
        {
            score += 5;
        }

        // Possessive pattern (John's, Sam's, etc.) followed by business word
        if (Regex.IsMatch(input, @"^\w+'s\s+\w+", RegexOptions.IgnoreCase))
        {
            score += 15;  // Strong business pattern
            
            // Check if it's a person's name possessive (common pattern)
            if (Regex.IsMatch(input, @"^[A-Z][a-z]+('s)?\s+"))
            {
                score += 5;  // Additional points for name pattern
            }
        }

        return Math.Min(score, 25);  // Increased cap slightly
    }

    private double CalculateSeparatorScore(string input)
    {
        double score = 0;

        if (input.Contains('&'))
            score += 7;
        
        if (input.Contains('-') && !Regex.IsMatch(input, @"^\d+.*-.*\d+$"))
            score += 3;

        if (input.Contains(','))
            score += 2;

        if (input.Contains('.') && !Regex.IsMatch(input, @"^\d+\.\d+$"))
            score += 3;

        return Math.Min(score, 10);
    }

    private double CalculateLengthComplexityScore(string input, List<string> words)
    {
        double score = 0;

        if (input.Length >= 10 && input.Length <= 50)
            score += 5;
        else if (input.Length > 50 && input.Length <= 100)
            score += 3;

        if (Regex.IsMatch(input, @"\d+"))
        {
            score += 2;
        }

        if (Regex.IsMatch(input, @"^\w+\s+\w+\s+(?:of|for|in)\s+\w+", RegexOptions.IgnoreCase))
        {
            score += 3;
        }

        return Math.Min(score, 10);
    }

    private double CalculateBusinessPatternScore(string input, List<string> words, List<string> matchedIndicators)
    {
        double score = 0;
        
        // Common business name patterns (food, retail, etc.)
        var businessKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "King", "Queen", "Prince", "Royal",
            "Burger", "Pizza", "Coffee", "Cafe", "Restaurant", "Diner", "Grill", "Kitchen",
            "Shop", "Store", "Market", "Mart", "Mall", "Outlet", "Boutique",
            "Bank", "Financial", "Capital", "Investment", "Fund",
            "Tech", "Digital", "Cyber", "Data", "Cloud", "Smart",
            "Health", "Medical", "Clinic", "Care", "Wellness",
            "Auto", "Motor", "Car", "Vehicle",
            "Express", "Quick", "Fast", "Rapid", "Speed",
            "Pro", "Professional", "Expert", "Master", "Premier",
            "First", "One", "Prime", "Best", "Top", "Super", "Ultra", "Mega",
            "Home", "House", "Property", "Real", "Estate",
            "Energy", "Power", "Electric", "Solar", "Green"
        };
        
        int keywordMatches = 0;
        foreach (var word in words)
        {
            if (businessKeywords.Contains(word))
            {
                keywordMatches++;
                if (!matchedIndicators.Contains(word))
                {
                    matchedIndicators.Add($"{word} (pattern)");
                }
            }
        }
        
        // Score based on keyword matches
        if (keywordMatches >= 2)
        {
            score += 35;  // Strong indicator with 2+ business keywords
        }
        else if (keywordMatches == 1)
        {
            score += 20;  // Moderate indicator with 1 business keyword
        }
        
        // Pattern: Two capitalized words (like "Burger King", "Home Depot")
        if (words.Count == 2 && 
            words.All(w => !string.IsNullOrEmpty(w) && char.IsUpper(w[0])))
        {
            score += 15;
        }
        
        // Pattern: Three capitalized words (like "Kentucky Fried Chicken")
        if (words.Count == 3 && 
            words.All(w => !string.IsNullOrEmpty(w) && char.IsUpper(w[0])))
        {
            score += 10;
        }
        
        return score;
    }

    private List<string> ExtractWords(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return new List<string>();

        var words = Regex.Split(input, @"[\s,\.\-&]+")
            .Where(w => !string.IsNullOrWhiteSpace(w))
            .Select(w => w.Trim())
            .ToList();

        return words;
    }
}