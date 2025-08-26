using IsBus.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace IsBus.Services;

public class WordFrequencyService : IWordFrequencyService
{
    private readonly PhonebookContext _context;
    private readonly IMemoryCache _cache;
    private readonly ILogger<WordFrequencyService> _logger;
    private const string HIGH_FREQ_CACHE_KEY = "HighFreqBusinessWords";

    public WordFrequencyService(
        PhonebookContext context,
        IMemoryCache cache,
        ILogger<WordFrequencyService> logger)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
    }

    public async Task<Dictionary<string, int>> GetWordFrequenciesAsync(List<string> words)
    {
        if (words == null || !words.Any())
            return new Dictionary<string, int>();

        try
        {
            // Clean possessives and prepare lookup list
            var lookupWords = new List<string>();
            foreach (var word in words)
            {
                // Remove possessive markers for database lookup
                var cleanWord = word.Replace("'s", "").Replace("'", "");
                lookupWords.Add(cleanWord.ToLowerInvariant());
            }
            
            lookupWords = lookupWords.Distinct().ToList();
            
            // Group by word_lower to handle duplicates, taking the max count
            var frequencies = await _context.WordData
                .Where(w => lookupWords.Contains(w.WordLower))
                .GroupBy(w => w.WordLower)
                .Select(g => new { WordLower = g.Key, WordCount = g.Max(w => w.WordCount) })
                .ToDictionaryAsync(w => w.WordLower, w => w.WordCount);

            // Map back to original words
            var result = new Dictionary<string, int>();
            foreach (var word in words)
            {
                var cleanWord = word.Replace("'s", "").Replace("'", "").ToLowerInvariant();
                result[cleanWord] = frequencies.ContainsKey(cleanWord) ? frequencies[cleanWord] : 0;
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting word frequencies");
            return new Dictionary<string, int>();
        }
    }

    public async Task<bool> IsHighFrequencyBusinessWordAsync(string word, int threshold = 100)
    {
        if (string.IsNullOrWhiteSpace(word))
            return false;

        try
        {
            var lowerWord = word.ToLowerInvariant();
            
            // Cache high frequency words for performance
            var highFreqWords = await _cache.GetOrCreateAsync(HIGH_FREQ_CACHE_KEY, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
                
                return await _context.WordData
                    .Where(w => w.WordCount >= threshold)
                    .Select(w => w.WordLower)
                    .ToListAsync();
            });

            return highFreqWords?.Contains(lowerWord) ?? false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking high frequency word: {Word}", word);
            return false;
        }
    }
}