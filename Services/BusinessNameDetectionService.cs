using System.Text.RegularExpressions;
using IsBus.Models;

namespace IsBus.Services;

public class BusinessNameDetectionService : IBusinessNameDetectionService
{
    private readonly IBusinessIndicatorService _indicatorService;
    private readonly IWordProcessingService _wordProcessingService;
    private readonly ILogger<BusinessNameDetectionService> _logger;

    public BusinessNameDetectionService(
        IBusinessIndicatorService indicatorService,
        IWordProcessingService wordProcessingService,
        ILogger<BusinessNameDetectionService> logger)
    {
        _indicatorService = indicatorService;
        _wordProcessingService = wordProcessingService;
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

        var scoringResult = CalculateConfidence(input);
        
        var response = new BusinessNameCheckResponse
        {
            Input = input,
            IsBusinessName = scoringResult.Confidence >= 70,
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

    private (double Confidence, List<string> MatchedIndicators) CalculateConfidence(string input)
    {
        double score = 0;
        var matchedIndicators = new List<string>();
        var words = ExtractWords(input);

        score += CalculateBusinessIndicatorScore(words, matchedIndicators);
        
        score += CalculateCapitalizationScore(input, words);
        
        score += CalculateStructureScore(input, words);
        
        score += CalculateSeparatorScore(input);
        
        score += CalculateLengthComplexityScore(input, words);

        return (Math.Min(score, 100), matchedIndicators);
    }

    private double CalculateBusinessIndicatorScore(List<string> words, List<string> matchedIndicators)
    {
        double score = 0;

        foreach (var word in words)
        {
            if (_indicatorService.IsPrimarySuffix(word))
            {
                score += 25;
                matchedIndicators.Add(word);
            }
            else if (_indicatorService.IsSecondaryIndicator(word))
            {
                score += 15;
                matchedIndicators.Add(word);
            }
        }

        return Math.Min(score, 40);
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

        return Math.Min(score, 20);
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