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
        
        // CHECK BUSINESS_INDICATORS TABLE FIRST for specific business keywords
        var businessIndicator = await _context.BusinessIndicators
            .Where(b => b.IndicatorText.ToLower() == wordLower && (b.IsActive ?? true))
            .FirstOrDefaultAsync();
            
        if (businessIndicator != null)
        {
            // Map weight to strength - these are known business terms
            BusinessIndicatorStrength indicatorStrength = businessIndicator.Weight switch
            {
                >= 95 => BusinessIndicatorStrength.Absolute,
                >= 85 => BusinessIndicatorStrength.Strong,  
                >= 70 => BusinessIndicatorStrength.Medium,
                >= 50 => BusinessIndicatorStrength.Weak,
                _ => BusinessIndicatorStrength.None
            };
            
            if (indicatorStrength != BusinessIndicatorStrength.None)
            {
                _logger.LogDebug($"Word '{wordLower}' found in business_indicators with weight {businessIndicator.Weight}, strength: {indicatorStrength}");
                _cache.Set(cacheKey, indicatorStrength, _cacheExpiration);
                return indicatorStrength;
            }
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
                >= 500 => BusinessIndicatorStrength.Strong,  // Lowered from 1000 - words like "Appraisals" with 696 count should be Strong
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
    
    private bool IsDebugPhrase(string phrase)
    {
        if (string.IsNullOrEmpty(phrase)) return false;
        
        var debugStarts = new[] { 
            "Aguayo Jesus",
            "Albert Luc",
            "Allain Arthur",
            "Allain Bernard",
            "Allain C",
            "Allain D",
            "Allain E",
            "Allain Eric",
            "Allain Gerald",
            "Allain Gisele",
            "Allain Herve",
            "Allain Jacques"
        };
        
        return debugStarts.Any(s => phrase.StartsWith(s, StringComparison.OrdinalIgnoreCase));
    }
    
    private bool IsCardinalDirection(string word)
    {
        var directions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "north", "south", "east", "west",
            "northeast", "northwest", "southeast", "southwest",
            "n", "s", "e", "w", "ne", "nw", "se", "sw"
        };
        
        return directions.Contains(word);
    }
    
    private async Task<bool> IsCommunityNameAsync(string word)
    {
        // Check if this word exists in the communities table
        var exists = await _context.Communities
            .AnyAsync(c => c.CommunityName.ToLower() == word.ToLower());
        
        return exists;
    }
    
    private async Task<(bool found, string communityName)> CheckForCommunityPhraseAsync(string phrase)
    {
        if (string.IsNullOrWhiteSpace(phrase))
            return (false, string.Empty);
        
        // Get all communities from database
        var communities = await _context.Communities
            .Select(c => c.CommunityName)
            .ToListAsync();
        
        // Check if the phrase contains any multi-word community names
        foreach (var community in communities)
        {
            if (string.IsNullOrWhiteSpace(community))
                continue;
            
            // Check for exact match or variations with hyphens
            // "Grand - Barachois" should match "Grand-Barachois" or "Grand Barachois"
            var normalizedCommunity = community.Replace("-", " ").Replace("  ", " ").Trim();
            var normalizedPhrase = phrase.Replace("-", " ").Replace("  ", " ").Trim();
            
            if (normalizedPhrase.Contains(normalizedCommunity, StringComparison.OrdinalIgnoreCase))
            {
                return (true, community);
            }
            
            // Also check with hyphens
            var hyphenatedCommunity = community.Replace(" ", "-");
            if (phrase.Contains(hyphenatedCommunity, StringComparison.OrdinalIgnoreCase))
            {
                return (true, community);
            }
        }
        
        return (false, string.Empty);
    }
    
    public async Task<Dictionary<string, BusinessIndicatorStrength>> AnalyzeWordsAsync(string[] words)
    {
        var result = new Dictionary<string, BusinessIndicatorStrength>();
        
        if (words == null || words.Length == 0)
            return result;
        
        // Clean and deduplicate words
        // IMPORTANT: Skip single letters (initials) as they should not be considered business indicators
        var cleanWords = words
            .Where(w => !string.IsNullOrWhiteSpace(w))
            .Select(w => w.ToLower().Trim('.', ',', '\'', '"'))
            .Where(w => w.Length > 1) // Skip single letters (initials)
            .Distinct()
            .ToArray();
        
        // Batch query for business indicators FIRST
        var businessIndicators = await _context.BusinessIndicators
            .Where(b => cleanWords.Contains(b.IndicatorText.ToLower()) && (b.IsActive ?? true))
            .ToListAsync();
            
        // Create a dictionary for quick lookup
        var indicatorDict = businessIndicators.ToDictionary(
            b => b.IndicatorText.ToLower(),
            b => b.Weight,
            StringComparer.OrdinalIgnoreCase
        );
        
        // Batch query for ALL word data (not just business type)
        var allWordDataList = await _context.WordData
            .Where(w => cleanWords.Contains(w.WordLower))
            .ToListAsync();
        
        // Ensure corporate suffixes are loaded
        await EnsureCorporateSuffixesLoadedAsync();
        
        foreach (var word in cleanWords)
        {
            // Skip cardinal directions - they're address words, not business indicators
            if (IsCardinalDirection(word))
            {
                result[word] = BusinessIndicatorStrength.None;
                continue;
            }
            
            // Skip community names - they're location words, not business indicators
            if (await IsCommunityNameAsync(word))
            {
                result[word] = BusinessIndicatorStrength.None;
                continue;
            }
            
            // Check corporate suffix first
            if (_corporateSuffixes?.Contains(word) ?? false)
            {
                result[word] = BusinessIndicatorStrength.Absolute;
                continue;
            }
            
            // CHECK BUSINESS_INDICATORS TABLE for known business keywords
            if (indicatorDict.TryGetValue(word, out var indicatorWeight))
            {
                BusinessIndicatorStrength indicatorStrength = indicatorWeight switch
                {
                    >= 95 => BusinessIndicatorStrength.Absolute,
                    >= 85 => BusinessIndicatorStrength.Strong,
                    >= 70 => BusinessIndicatorStrength.Medium,
                    >= 50 => BusinessIndicatorStrength.Weak,
                    _ => BusinessIndicatorStrength.None
                };
                
                if (indicatorStrength != BusinessIndicatorStrength.None)
                {
                    result[word] = indicatorStrength;
                    continue;
                }
            }
            
            // Get all entries for this word
            var wordEntries = allWordDataList.Where(w => w.WordLower == word).ToList();
            
            // Special debug for problem words
            if (word == "abdelhadi" || word == "aberathna")
            {
                _logger.LogInformation($"DEBUG BusinessWordService: Found {wordEntries.Count} entries for '{word}':");
                foreach (var entry in wordEntries)
                {
                    _logger.LogInformation($"  - {entry.WordType}: {entry.WordCount}");
                }
            }
            
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
            
            // CRITICAL: Use the HIGHEST count to determine the word's primary type
            // If name counts are higher than business count, it's NOT a business word
            if (maxNameCount > businessData.WordCount)
            {
                result[word] = BusinessIndicatorStrength.None;
            }
            // Only if business count is the HIGHEST should we consider it a business word
            else if (businessData.WordCount >= maxNameCount)
            {
                // But still check if it's a significant business indicator
                result[word] = businessData.WordCount switch
                {
                    >= 5000 => BusinessIndicatorStrength.Absolute,
                    >= 1000 => BusinessIndicatorStrength.Strong,
                    >= 100 => BusinessIndicatorStrength.Medium,
                    >= 10 => BusinessIndicatorStrength.Weak,
                    _ => BusinessIndicatorStrength.None
                };
                
                // If name counts are close (within 50% of business count), reduce strength
                if (maxNameCount > businessData.WordCount * 0.5)
                {
                    result[word] = result[word] switch
                    {
                        BusinessIndicatorStrength.Absolute => BusinessIndicatorStrength.Strong,
                        BusinessIndicatorStrength.Strong => BusinessIndicatorStrength.Medium,
                        BusinessIndicatorStrength.Medium => BusinessIndicatorStrength.Weak,
                        _ => BusinessIndicatorStrength.None
                    };
                }
            }
        }
        
        return result;
    }
    
    public async Task<(bool isBusiness, BusinessIndicatorStrength maxStrength, string reason)> AnalyzePhraseAsync(string phrase)
    {
        if (string.IsNullOrWhiteSpace(phrase))
            return (false, BusinessIndicatorStrength.None, "Empty phrase");
        
        // Debug logging for specific records
        bool enableDebug = IsDebugPhrase(phrase);
        if (enableDebug)
        {
            _logger.LogWarning($"DEBUG BusinessWordService.AnalyzePhraseAsync: '{phrase}'");
        }
        
        // First check if the phrase contains multi-word community names
        // This needs to happen before splitting into individual words
        var phraseContainsCommunity = await CheckForCommunityPhraseAsync(phrase);
        
        var words = phrase.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        // If the phrase contains a known community, filter those words out
        Dictionary<string, BusinessIndicatorStrength> wordStrengths;
        if (phraseContainsCommunity.found)
        {
            if (enableDebug)
            {
                _logger.LogWarning($"  Found community phrase: '{phraseContainsCommunity.communityName}'");
                _logger.LogWarning($"  Filtering out community words from analysis");
            }
            
            // Filter out the community words from analysis
            var communityWords = phraseContainsCommunity.communityName
                .Split(new[] { ' ', '-' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(w => w.ToLower())
                .ToHashSet();
            
            var filteredWords = words
                .Where(w => !communityWords.Contains(w.ToLower().Trim('.', ',', '\'', '"', '-')))
                .ToArray();
            
            wordStrengths = await AnalyzeWordsAsync(filteredWords);
        }
        else
        {
            wordStrengths = await AnalyzeWordsAsync(words);
        }
        
        if (enableDebug)
        {
            _logger.LogWarning($"  Input phrase: '{phrase}'");
            _logger.LogWarning($"  Split into words: [{string.Join(", ", words.Select(w => $"'{w}'"))}]");
            _logger.LogWarning($"  Word strengths analyzed ({wordStrengths.Count} unique words):");
            foreach (var kvp in wordStrengths)
            {
                _logger.LogWarning($"    '{kvp.Key}': {kvp.Value}");
                
                // Get the actual database counts for debugging
                var wordData = await _context.WordData
                    .Where(w => w.WordLower == kvp.Key.ToLower())
                    .ToListAsync();
                
                if (wordData.Any())
                {
                    foreach (var data in wordData)
                    {
                        _logger.LogWarning($"      Database: {data.WordType}={data.WordCount}");
                    }
                }
                else
                {
                    _logger.LogWarning($"      NOT IN DATABASE");
                }
            }
            
            // Show which words were not analyzed (duplicates)
            var analyzedWords = wordStrengths.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var word in words)
            {
                var clean = word.ToLower().Trim('.', ',', '\'', '"');
                if (!string.IsNullOrEmpty(clean) && !analyzedWords.Contains(clean))
                {
                    _logger.LogWarning($"    '{clean}': DUPLICATE (not analyzed)");
                }
            }
        }
        
        // Debug logging for important cases
        if (phrase.Contains("Abraham", StringComparison.OrdinalIgnoreCase) || 
            phrase.Contains("Equipment", StringComparison.OrdinalIgnoreCase) ||
            phrase.Contains("Cranes", StringComparison.OrdinalIgnoreCase) ||
            phrase.Contains("Leil", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation($"Analyzing phrase: '{phrase}'");
            _logger.LogInformation($"  Word count in phrase: {words.Length}");
            _logger.LogInformation($"  Words analyzed: {wordStrengths.Count}");
            
            // Log detailed word analysis
            foreach (var kvp in wordStrengths)
            {
                _logger.LogInformation($"  Word '{kvp.Key}' strength: {kvp.Value}");
                
                // Get detailed counts for this word
                var wordData = await _context.WordData
                    .Where(w => w.WordLower == kvp.Key.ToLower())
                    .ToListAsync();
                
                foreach (var data in wordData)
                {
                    _logger.LogInformation($"    {kvp.Key} - {data.WordType}: {data.WordCount}");
                }
                
                if (!wordData.Any())
                {
                    _logger.LogInformation($"    {kvp.Key} - NO DATABASE ENTRIES");
                }
            }
            
            // Also check words that might have been skipped
            foreach (var word in words)
            {
                var cleanWord = word.ToLower().Trim('.', ',', '\'', '"');
                if (!wordStrengths.ContainsKey(cleanWord))
                {
                    _logger.LogInformation($"  Word '{cleanWord}' was not analyzed (likely duplicate or empty)");
                }
            }
        }
        
        if (wordStrengths.Count == 0)
            return (false, BusinessIndicatorStrength.None, "No analyzable words");
        
        var maxStrength = wordStrengths.Values.Max();
        var strongWords = wordStrengths.Where(kvp => kvp.Value >= BusinessIndicatorStrength.Strong).ToList();
        var mediumWords = wordStrengths.Where(kvp => kvp.Value == BusinessIndicatorStrength.Medium).ToList();
        
        if (enableDebug)
        {
            _logger.LogWarning($"  Analysis summary:");
            _logger.LogWarning($"    Max strength: {maxStrength}");
            _logger.LogWarning($"    Strong words ({strongWords.Count}): [{string.Join(", ", strongWords.Select(w => $"'{w.Key}'"))}]");
            _logger.LogWarning($"    Medium words ({mediumWords.Count}): [{string.Join(", ", mediumWords.Select(w => $"'{w.Key}'"))}]");
        }
        
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
            if (enableDebug)
            {
                _logger.LogWarning($"  DECISION: Classifying as BUSINESS because it contains strong business word: {strongWord}");
            }
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
        
        if (enableDebug)
        {
            _logger.LogWarning($"  DECISION: NOT classifying as business - no strong indicators found");
        }
        return (false, maxStrength, "No strong business indicators found");
    }
}