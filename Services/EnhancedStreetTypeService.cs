using System.Text.RegularExpressions;
using IsBus.Data;
using IsBus.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace IsBus.Services;

/// <summary>
/// Enhanced street type service that uses the database street_type_mapping table
/// instead of hard-coded values
/// </summary>
public class EnhancedStreetTypeService : IStreetTypeService
{
    private readonly PhonebookContext _context;
    private readonly IMemoryCache _cache;
    private readonly ILogger<EnhancedStreetTypeService> _logger;
    private const string CACHE_KEY = "street_type_mappings";
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromHours(24);

    // Titles that might conflict with street abbreviations
    private readonly HashSet<string> _titles = new(StringComparer.OrdinalIgnoreCase)
    {
        "dr", "doctor", "docteur", "mr", "mrs", "ms", "miss", "mme", "mlle", "m"
    };

    public EnhancedStreetTypeService(
        PhonebookContext context,
        IMemoryCache cache,
        ILogger<EnhancedStreetTypeService> logger)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
    }

    private async Task<Dictionary<string, string>> GetStreetTypeMappingsAsync()
    {
        return await _cache.GetOrCreateAsync(CACHE_KEY, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = _cacheExpiration;
            
            // First try to get from database
            try
            {
                var mappings = await _context.StreetTypeMappings
                    .Where(m => m.IsPrimary)
                    .ToDictionaryAsync(
                        m => m.Abbreviation.ToLower(),
                        m => m.FullName
                    );

                if (mappings.Count > 0)
                {
                    _logger.LogInformation($"Loaded {mappings.Count} street type mappings from database");
                    return mappings;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not load street type mappings from database, using fallback");
            }

            // Fallback to in-memory mappings if database is not available
            return StreetTypeMapping.CommonAbbreviations.ToDictionary(
                kvp => kvp.Key.ToLower(),
                kvp => kvp.Value,
                StringComparer.OrdinalIgnoreCase
            );
        }) ?? new Dictionary<string, string>();
    }

    public bool IsStreetType(string word)
    {
        if (string.IsNullOrWhiteSpace(word))
            return false;

        word = word.Trim().Trim('.', ',').ToLower();
        
        // Use the static helper first for performance
        if (StreetTypeMapping.IsKnownStreetType(word))
            return true;

        // Check database mappings
        var mappings = GetStreetTypeMappingsAsync().GetAwaiter().GetResult();
        return mappings.ContainsKey(word);
    }

    public string? GetStreetTypeStandardForm(string word)
    {
        if (string.IsNullOrWhiteSpace(word))
            return null;

        word = word.Trim().Trim('.', ',');
        
        // Use the static helper first for performance
        var normalized = StreetTypeMapping.NormalizeStreetType(word);
        if (normalized != word)
            return normalized;

        // Check database mappings
        var mappings = GetStreetTypeMappingsAsync().GetAwaiter().GetResult();
        return mappings.TryGetValue(word.ToLower(), out var result) ? result : null;
    }

    public bool ContainsStreetType(string text, out string? streetType, out int position)
    {
        streetType = null;
        position = -1;

        if (string.IsNullOrWhiteSpace(text))
            return false;

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        for (int i = 0; i < words.Length; i++)
        {
            var word = words[i].Trim().Trim('.', ',');
            
            // Special handling for "Dr" - check if it's likely a title
            if (word.Equals("dr", StringComparison.OrdinalIgnoreCase))
            {
                if (IsLikelyDoctorTitle(text, i))
                    continue;
            }
            
            if (IsStreetType(word))
            {
                streetType = GetStreetTypeStandardForm(word);
                position = i;
                
                // Calculate character position in original string
                int charPos = 0;
                for (int j = 0; j < i; j++)
                {
                    charPos += words[j].Length + 1; // +1 for space
                }
                position = charPos;
                
                return true;
            }
        }

        return false;
    }

    public bool IsLikelyDoctorTitle(string text, int wordPosition)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        // If "Dr" is at position 0 or 1, it's likely a title
        if (wordPosition <= 1)
            return true;
        
        // If followed by a name-like word (capitalized), it's likely a title
        if (wordPosition < words.Length - 1)
        {
            var nextWord = words[wordPosition + 1];
            if (char.IsUpper(nextWord[0]) && !IsStreetType(nextWord))
            {
                // Check if the next word looks like a first or last name
                if (!Regex.IsMatch(nextWord, @"^\d+"))
                {
                    return true;
                }
            }
        }
        
        // If preceded by a number, it's likely "Drive"
        if (wordPosition > 0)
        {
            var prevWord = words[wordPosition - 1];
            if (Regex.IsMatch(prevWord, @"^\d+$"))
            {
                return false; // It's likely "Drive"
            }
        }
        
        // Default to treating it as Drive if we're not sure
        return false;
    }
}