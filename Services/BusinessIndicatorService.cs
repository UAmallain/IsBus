using IsBus.Models;
using Microsoft.Extensions.Caching.Memory;

namespace IsBus.Services;

public class BusinessIndicatorService : IBusinessIndicatorService
{
    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _cache;
    private readonly ILogger<BusinessIndicatorService> _logger;
    private const string CacheKey = "BusinessIndicators";

    public BusinessIndicatorService(
        IConfiguration configuration, 
        IMemoryCache cache, 
        ILogger<BusinessIndicatorService> logger)
    {
        _configuration = configuration;
        _cache = cache;
        _logger = logger;
    }

    public BusinessIndicatorConfig GetIndicators()
    {
        return _cache.GetOrCreate(CacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24);
            
            var config = new BusinessIndicatorConfig();
            _configuration.GetSection("BusinessIndicators").Bind(config);
            
            _logger.LogInformation("Loaded {PrimaryCount} primary suffixes and {SecondaryCount} secondary indicators",
                config.PrimarySuffixes.Count, config.SecondaryIndicators.Count);
            
            return config;
        })!;
    }

    public bool IsPrimarySuffix(string word)
    {
        if (string.IsNullOrWhiteSpace(word)) return false;
        
        var indicators = GetIndicators();
        return indicators.PrimarySuffixes.Any(suffix => 
            word.Equals(suffix, StringComparison.OrdinalIgnoreCase) ||
            word.Equals(suffix.Replace(".", ""), StringComparison.OrdinalIgnoreCase));
    }

    public bool IsSecondaryIndicator(string word)
    {
        if (string.IsNullOrWhiteSpace(word)) return false;
        
        var indicators = GetIndicators();
        return indicators.SecondaryIndicators.Any(indicator => 
            word.Equals(indicator, StringComparison.OrdinalIgnoreCase));
    }

    public bool IsStopWord(string word)
    {
        if (string.IsNullOrWhiteSpace(word)) return false;
        
        var indicators = GetIndicators();
        return indicators.CommonStopWords.Any(stopWord => 
            word.Equals(stopWord, StringComparison.OrdinalIgnoreCase));
    }
}