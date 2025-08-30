using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IsBus.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace IsBus.Services;

public class BusinessWordService : IBusinessWordService
{
    private readonly PhonebookContext _context;
    private readonly ILogger<BusinessWordService> _logger;
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(30);
    
    // Cache for corporate suffixes loaded from database
    private HashSet<string>? _corporateSuffixes = null;
    private DateTime _corporateSuffixesLoadTime = DateTime.MinValue;
    private readonly TimeSpan _corporateSuffixCacheExpiration = TimeSpan.FromHours(24);
    
    public BusinessWordService(
        PhonebookContext context,
        ILogger<BusinessWordService> logger,
        IMemoryCache cache)
    {
        _context = context;
        _logger = logger;
        _cache = cache;
    }
    
    public async Task<BusinessIndicatorStrength> GetWordStrengthAsync(string word)
    {
        if (string.IsNullOrWhiteSpace(word))
            return BusinessIndicatorStrength.None;
        
        var wordLower = word.ToLower().Trim('.', ',', '\'', '"');
        
        // Check cache first
        var cacheKey = $"word_strength_{wordLower}";
        if (_cache.TryGetValue<BusinessIndicatorStrength>(cacheKey, out var cachedStrength))
        {
            return cachedStrength;
        }
        
        // Check if it's a corporate suffix first
        await EnsureCorporateSuffixesLoadedAsync();
        if (_corporateSuffixes?.Contains(wordLower) ?? false)
        {
            var strength = BusinessIndicatorStrength.Absolute;
            _cache.Set(cacheKey, strength, _cacheExpiration);
            return strength;
        }
        
        // Get ALL word_data entries for this word to compare counts
        var allWordData = await _context.WordData
            .Where(w => w.WordLower == wordLower)
            .ToListAsync();
        
        // Find the business entry specifically
        var businessData = allWordData.FirstOrDefault(w => w.WordType == "business");
        
        if (businessData == null)
        {
            _cache.Set(cacheKey, BusinessIndicatorStrength.None, _cacheExpiration);
            return BusinessIndicatorStrength.None;
        }
        
        // Get the name counts for comparison
        var firstCount = allWordData.FirstOrDefault(w => w.WordType == "first")?.WordCount ?? 0;
        var lastCount = allWordData.FirstOrDefault(w => w.WordType == "last")?.WordCount ?? 0;
        var bothCount = allWordData.FirstOrDefault(w => w.WordType == "both")?.WordCount ?? 0;
        
        // Find the maximum name count
        var maxNameCount = Math.Max(Math.Max(firstCount, lastCount), bothCount);
        
        BusinessIndicatorStrength resultStrength;
        
        // If name counts are significantly higher than business count, it's not a business word
        if (maxNameCount > businessData.WordCount * 2 && maxNameCount >= 50)
        {
            _logger.LogDebug($"Word '{wordLower}' has higher name count ({maxNameCount}) than business count ({businessData.WordCount}), marking as None");
            resultStrength = BusinessIndicatorStrength.None;
        }
        // If business count is significantly higher than name counts, use normal strength calculation
        else if (businessData.WordCount > maxNameCount * 2 || maxNameCount < 10)
        {
            resultStrength = businessData.WordCount switch
            {
                >= 5000 => BusinessIndicatorStrength.Absolute,
                >= 1000 => BusinessIndicatorStrength.Strong,
                >= 100 => BusinessIndicatorStrength.Medium,
                >= 10 => BusinessIndicatorStrength.Weak,
                _ => BusinessIndicatorStrength.None
            };
            _logger.LogDebug($"Word '{wordLower}' has business count {businessData.WordCount} (vs max name {maxNameCount}), strength: {resultStrength}");
        }
        // Counts are comparable - reduce strength since it could be either
        else
        {
            _logger.LogDebug($"Word '{wordLower}' has comparable business ({businessData.WordCount}) and name ({maxNameCount}) counts, reducing strength");
            // Reduce the strength by one level due to ambiguity
            var baseStrength = businessData.WordCount switch
            {
                >= 5000 => BusinessIndicatorStrength.Absolute,
                >= 1000 => BusinessIndicatorStrength.Strong,
                >= 100 => BusinessIndicatorStrength.Medium,
                >= 10 => BusinessIndicatorStrength.Weak,
                _ => BusinessIndicatorStrength.None
            };
            
            // Reduce by one level
            resultStrength = baseStrength switch
            {
                BusinessIndicatorStrength.Absolute => BusinessIndicatorStrength.Strong,
                BusinessIndicatorStrength.Strong => BusinessIndicatorStrength.Medium,
                BusinessIndicatorStrength.Medium => BusinessIndicatorStrength.Weak,
                _ => BusinessIndicatorStrength.None
            };
        }
        
        _cache.Set(cacheKey, resultStrength, _cacheExpiration);
        return resultStrength;
    }
    
    public async Task<bool> IsStrongBusinessWordAsync(string word)
    {
        var strength = await GetWordStrengthAsync(word);
        return strength >= BusinessIndicatorStrength.Strong;
    }
    
    public async Task<bool> IsCorporateSuffixAsync(string word)
    {
        if (string.IsNullOrWhiteSpace(word))
            return false;
            
        var wordLower = word.ToLower().Trim('.', ',', '\'', '"');
        
        // Load or refresh corporate suffixes from database if needed
        await EnsureCorporateSuffixesLoadedAsync();
        
        return _corporateSuffixes?.Contains(wordLower) ?? false;
    }
    
    private async Task EnsureCorporateSuffixesLoadedAsync()
    {
        // Check if we need to reload the corporate suffixes
        if (_corporateSuffixes == null || 
            DateTime.UtcNow - _corporateSuffixesLoadTime > _corporateSuffixCacheExpiration)
        {
            // Load corporate suffixes from database 
            // Now using 'corporate' word type OR business words with count >= 99999
            var corporateSuffixes = await _context.WordData
                .Where(w => w.WordType == "corporate" || 
                           (w.WordType == "business" && w.WordCount >= 99999))
                .Select(w => w.WordLower)
                .ToListAsync();
            
            _corporateSuffixes = new HashSet<string>(corporateSuffixes, StringComparer.OrdinalIgnoreCase);
            _corporateSuffixesLoadTime = DateTime.UtcNow;
            
            _logger.LogInformation($"Loaded {_corporateSuffixes.Count} corporate suffixes from database");
        }
    }
    
    public async Task<Dictionary<string, BusinessIndicatorStrength>> AnalyzeWordsAsync(string[] words)
    {
        var result = new Dictionary<string, BusinessIndicatorStrength>();
        
        if (words == null || words.Length == 0)
            return result;
        
        // Clean and deduplicate words
        var cleanWords = words
            .Where(w => !string.IsNullOrWhiteSpace(w))
            .Select(w => w.ToLower().Trim('.', ',', '\'', '"'))
            .Distinct()
            .ToArray();
        
        // Batch query for ALL word data (not just business type)
        var allWordDataList = await _context.WordData
            .Where(w => cleanWords.Contains(w.WordLower))
            .ToListAsync();
        
        // Ensure corporate suffixes are loaded
        await EnsureCorporateSuffixesLoadedAsync();
        
        foreach (var word in cleanWords)
        {
            // Check corporate suffix first
            if (_corporateSuffixes?.Contains(word) ?? false)
            {
                result[word] = BusinessIndicatorStrength.Absolute;
                continue;
            }
            
            // Get all entries for this word
            var wordEntries = allWordDataList.Where(w => w.WordLower == word).ToList();
            
            // Find the business entry specifically
            var businessData = wordEntries.FirstOrDefault(w => w.WordType == "business");
            
            if (businessData == null)
            {
                result[word] = BusinessIndicatorStrength.None;
                continue;
            }
            
            // Get the name counts for comparison
            var firstCount = wordEntries.FirstOrDefault(w => w.WordType == "first")?.WordCount ?? 0;
            var lastCount = wordEntries.FirstOrDefault(w => w.WordType == "last")?.WordCount ?? 0;
            var bothCount = wordEntries.FirstOrDefault(w => w.WordType == "both")?.WordCount ?? 0;
            
            // Find the maximum name count
            var maxNameCount = Math.Max(Math.Max(firstCount, lastCount), bothCount);
            
            // Apply the same logic as GetWordStrengthAsync
            // If name counts are significantly higher than business count, it's not a business word
            if (maxNameCount > businessData.WordCount * 2 && maxNameCount >= 50)
            {
                result[word] = BusinessIndicatorStrength.None;
            }
            // If business count is significantly higher than name counts, use normal strength calculation
            else if (businessData.WordCount > maxNameCount * 2 || maxNameCount < 10)
            {
                result[word] = businessData.WordCount switch
                {
                    >= 5000 => BusinessIndicatorStrength.Absolute,
                    >= 1000 => BusinessIndicatorStrength.Strong,
                    >= 100 => BusinessIndicatorStrength.Medium,
                    >= 10 => BusinessIndicatorStrength.Weak,
                    _ => BusinessIndicatorStrength.None
                };
            }
            // Counts are comparable - reduce strength since it could be either
            else
            {
                // Reduce the strength by one level due to ambiguity
                var baseStrength = businessData.WordCount switch
                {
                    >= 5000 => BusinessIndicatorStrength.Absolute,
                    >= 1000 => BusinessIndicatorStrength.Strong,
                    >= 100 => BusinessIndicatorStrength.Medium,
                    >= 10 => BusinessIndicatorStrength.Weak,
                    _ => BusinessIndicatorStrength.None
                };
                
                // Reduce by one level
                result[word] = baseStrength switch
                {
                    BusinessIndicatorStrength.Absolute => BusinessIndicatorStrength.Strong,
                    BusinessIndicatorStrength.Strong => BusinessIndicatorStrength.Medium,
                    BusinessIndicatorStrength.Medium => BusinessIndicatorStrength.Weak,
                    _ => BusinessIndicatorStrength.None
                };
            }
        }
        
        return result;
    }
    
    public async Task<(bool isBusiness, BusinessIndicatorStrength maxStrength, string reason)> AnalyzePhraseAsync(string phrase)
    {
        if (string.IsNullOrWhiteSpace(phrase))
            return (false, BusinessIndicatorStrength.None, "Empty phrase");
        
        var words = phrase.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var wordStrengths = await AnalyzeWordsAsync(words);
        
        // Debug logging for Abraham Kaine case
        if (phrase.Contains("Abraham", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation($"Analyzing phrase: {phrase}");
            
            // Get detailed counts for Abraham
            var abrahamData = await _context.WordData
                .Where(w => w.WordLower == "abraham")
                .ToListAsync();
            
            foreach (var data in abrahamData)
            {
                _logger.LogInformation($"  Abraham - {data.WordType}: {data.WordCount}");
            }
            
            foreach (var kvp in wordStrengths)
            {
                _logger.LogInformation($"  Word '{kvp.Key}' strength: {kvp.Value}");
            }
        }
        
        if (wordStrengths.Count == 0)
            return (false, BusinessIndicatorStrength.None, "No analyzable words");
        
        var maxStrength = wordStrengths.Values.Max();
        var strongWords = wordStrengths.Where(kvp => kvp.Value >= BusinessIndicatorStrength.Strong).ToList();
        var mediumWords = wordStrengths.Where(kvp => kvp.Value == BusinessIndicatorStrength.Medium).ToList();
        
        // Decision logic - log decision for Abraham
        if (phrase.Contains("Abraham", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation($"Decision for '{phrase}': maxStrength={maxStrength}, strongWords={strongWords.Count}, mediumWords={mediumWords.Count}");
        }
        
        if (maxStrength == BusinessIndicatorStrength.Absolute)
        {
            var absoluteWord = wordStrengths.First(kvp => kvp.Value == BusinessIndicatorStrength.Absolute).Key;
            return (true, maxStrength, $"Contains absolute business indicator: {absoluteWord}");
        }
        
        if (strongWords.Any())
        {
            var strongWord = strongWords.First().Key;
            return (true, maxStrength, $"Contains strong business word: {strongWord}");
        }
        
        if (mediumWords.Count >= 2)
        {
            var mediumWordList = string.Join(", ", mediumWords.Take(2).Select(kvp => kvp.Key));
            return (true, BusinessIndicatorStrength.Strong, $"Multiple medium business indicators: {mediumWordList}");
        }
        
        if (mediumWords.Count == 1)
        {
            var mediumWord = mediumWords.First().Key;
            // Single medium word alone is not enough - need more business context
            // Check if there are weak indicators that support it
            var weakWords = wordStrengths.Where(kvp => kvp.Value == BusinessIndicatorStrength.Weak).ToList();
            if (weakWords.Count >= 2)
            {
                return (true, BusinessIndicatorStrength.Medium, $"Medium indicator '{mediumWord}' with supporting weak indicators");
            }
            return (false, maxStrength, $"Single medium indicator '{mediumWord}' - needs more context");
        }
        
        // Weak indicators alone are not enough to classify as business
        var weakCount = wordStrengths.Count(kvp => kvp.Value == BusinessIndicatorStrength.Weak);
        if (weakCount > 0)
        {
            _logger.LogDebug($"Found {weakCount} weak business indicators in '{phrase}' - not enough for business classification");
        }
        
        return (false, maxStrength, "No strong business indicators found");
    }
}