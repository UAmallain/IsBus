using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using IsBus.Data;
using IsBus.Models;

namespace IsBus.Services;

public interface IReferenceDataService
{
    Task<HashSet<string>> GetStreetTypesAsync();
    Task<HashSet<string>> GetBusinessEndingsAsync();
    Task<HashSet<string>> GetProvinceCodesAsync();
    Task<HashSet<string>> GetSkipWordsAsync(string context = "general");
    Task<HashSet<string>> GetRoadIndicatorsAsync();
    Task<HashSet<string>> GetSuiteIndicatorsAsync();
    Task<bool> IsBusinessIndicatorAsync(string word);
    Task<int> GetBusinessIndicatorWeightAsync(string word);
}

public class ReferenceDataService : IReferenceDataService
{
    private readonly PhonebookContext _context;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ReferenceDataService> _logger;
    private const string CACHE_PREFIX = "refdata_";
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromHours(1);

    public ReferenceDataService(
        PhonebookContext context,
        IMemoryCache cache,
        ILogger<ReferenceDataService> logger)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
    }

    public async Task<HashSet<string>> GetStreetTypesAsync()
    {
        var cacheKey = $"{CACHE_PREFIX}street_types";
        
        if (!_cache.TryGetValue<HashSet<string>>(cacheKey, out var streetTypes))
        {
            // Get from street_type_mapping table
            var types = await _context.StreetTypeMappings
                .Select(s => s.Abbreviation)
                .ToListAsync();
            
            // Also get full names
            var fullNames = await _context.StreetTypeMappings
                .Select(s => s.FullName)
                .ToListAsync();
            
            streetTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            streetTypes.UnionWith(types);
            streetTypes.UnionWith(fullNames);
            
            _cache.Set(cacheKey, streetTypes, _cacheExpiration);
            _logger.LogDebug($"Loaded {streetTypes.Count} street types from database");
        }
        
        return streetTypes;
    }

    public async Task<HashSet<string>> GetBusinessEndingsAsync()
    {
        var cacheKey = $"{CACHE_PREFIX}business_endings";
        
        if (!_cache.TryGetValue<HashSet<string>>(cacheKey, out var endings))
        {
            // Get from business_indicators table where type is primary_suffix
            var indicators = await _context.BusinessIndicators
                .Where(b => b.IndicatorType == "primary_suffix" && (b.IsActive ?? true))
                .Select(b => b.IndicatorText)
                .ToListAsync();
            
            endings = new HashSet<string>(indicators, StringComparer.OrdinalIgnoreCase);
            
            _cache.Set(cacheKey, endings, _cacheExpiration);
            _logger.LogDebug($"Loaded {endings.Count} business endings from database");
        }
        
        return endings;
    }

    public async Task<HashSet<string>> GetProvinceCodesAsync()
    {
        var cacheKey = $"{CACHE_PREFIX}province_codes";
        
        if (!_cache.TryGetValue<HashSet<string>>(cacheKey, out var provinces))
        {
            var codes = await _context.ProvinceMappings
                .Select(p => p.ProvinceCode)
                .ToListAsync();
            
            provinces = new HashSet<string>(codes, StringComparer.OrdinalIgnoreCase);
            
            _cache.Set(cacheKey, provinces, _cacheExpiration);
            _logger.LogDebug($"Loaded {provinces.Count} province codes from database");
        }
        
        return provinces;
    }

    public async Task<HashSet<string>> GetSkipWordsAsync(string context = "general")
    {
        var cacheKey = $"{CACHE_PREFIX}skip_words_{context}";
        
        if (!_cache.TryGetValue<HashSet<string>>(cacheKey, out var skipWords))
        {
            // Get stop words from business_indicators table
            var words = await _context.BusinessIndicators
                .Where(b => b.IndicatorType == "stop_word" && (b.IsActive ?? true))
                .Select(b => b.IndicatorText)
                .ToListAsync();
            
            skipWords = new HashSet<string>(words, StringComparer.OrdinalIgnoreCase);
            
            _cache.Set(cacheKey, skipWords, _cacheExpiration);
            _logger.LogDebug($"Loaded {skipWords.Count} skip words from database");
        }
        
        return skipWords;
    }

    public async Task<HashSet<string>> GetRoadIndicatorsAsync()
    {
        var cacheKey = $"{CACHE_PREFIX}road_indicators";
        
        if (!_cache.TryGetValue<HashSet<string>>(cacheKey, out var indicators))
        {
            // Get from street_type_mapping table - fetch data first, then process in memory
            var mappings = await _context.StreetTypeMappings
                .ToListAsync();
            
            // Now process in memory to combine abbreviations and full names
            var types = mappings
                .SelectMany(s => new[] { s.Abbreviation, s.FullName })
                .Where(t => !string.IsNullOrEmpty(t))
                .ToList();
            
            indicators = new HashSet<string>(types, StringComparer.OrdinalIgnoreCase);
            
            _cache.Set(cacheKey, indicators, _cacheExpiration);
            _logger.LogDebug($"Loaded {indicators.Count} road indicators from database");
        }
        
        return indicators;
    }

    public async Task<HashSet<string>> GetSuiteIndicatorsAsync()
    {
        var cacheKey = $"{CACHE_PREFIX}suite_indicators";
        
        if (!_cache.TryGetValue<HashSet<string>>(cacheKey, out var indicators))
        {
            // Define suite indicators (could be added to business_indicators with a specific type)
            var suiteWords = new[] { "Suite", "Unit", "Apt", "Apartment", "Room", "Rm", "Floor", "Fl", "#" };
            
            indicators = new HashSet<string>(suiteWords, StringComparer.OrdinalIgnoreCase);
            
            _cache.Set(cacheKey, indicators, _cacheExpiration);
            _logger.LogDebug($"Loaded {indicators.Count} suite indicators");
        }
        
        return indicators;
    }

    public async Task<bool> IsBusinessIndicatorAsync(string word)
    {
        if (string.IsNullOrWhiteSpace(word))
            return false;
        
        var cacheKey = $"{CACHE_PREFIX}is_business_{word.ToLower()}";
        
        if (!_cache.TryGetValue<bool>(cacheKey, out var isBusiness))
        {
            isBusiness = await _context.BusinessIndicators
                .AnyAsync(b => b.IndicatorText.ToLower() == word.ToLower() 
                    && b.IndicatorType != "stop_word"
                    && (b.IsActive ?? true));
            
            _cache.Set(cacheKey, isBusiness, _cacheExpiration);
        }
        
        return isBusiness;
    }

    public async Task<int> GetBusinessIndicatorWeightAsync(string word)
    {
        if (string.IsNullOrWhiteSpace(word))
            return 0;
        
        var cacheKey = $"{CACHE_PREFIX}weight_{word.ToLower()}";
        
        if (!_cache.TryGetValue<int>(cacheKey, out var weight))
        {
            var indicator = await _context.BusinessIndicators
                .Where(b => b.IndicatorText.ToLower() == word.ToLower() 
                    && (b.IsActive ?? true))
                .FirstOrDefaultAsync();
            
            weight = indicator?.Weight ?? 0;
            
            _cache.Set(cacheKey, weight, _cacheExpiration);
        }
        
        return weight;
    }
}