using IsBus.Data;
using IsBus.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace IsBus.Services;

public interface IStreetNameService
{
    Task<bool> IsKnownStreetNameAsync(string name, string? province = null);
    Task RecordStreetNameAsync(string streetName, string? streetType = null, string? province = null, string? community = null);
    Task<List<StreetName>> GetStreetNamesAsync(string? province = null);
}

public class StreetNameService : IStreetNameService
{
    private readonly PhonebookContext _context;
    private readonly IMemoryCache _cache;
    private readonly ILogger<StreetNameService> _logger;
    private const string CACHE_KEY_PREFIX = "street_names_";
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromHours(2);

    public StreetNameService(
        PhonebookContext context,
        IMemoryCache cache,
        ILogger<StreetNameService> logger)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
    }

    public async Task<bool> IsKnownStreetNameAsync(string name, string? province = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        var normalizedName = name.Trim().ToLower();
        var streetNames = await GetStreetNamesAsync(province);
        
        return streetNames.Any(s => s.NameLower == normalizedName || 
                                    s.NameLower.Contains(normalizedName));
    }

    public async Task RecordStreetNameAsync(string streetName, string? streetType = null, 
        string? province = null, string? community = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(streetName))
                return;

            var normalizedName = streetName.Trim();
            var nameLower = normalizedName.ToLower();

            // Check if street name already exists
            var existing = await _context.Set<StreetName>()
                .FirstOrDefaultAsync(s => s.NameLower == nameLower && 
                                         (province == null || s.ProvinceCode == province));

            if (existing != null)
            {
                // Increment occurrence count
                existing.OccurrenceCount++;
                existing.UpdatedAt = DateTime.Now;
            }
            else
            {
                // Add new street name
                var newStreetName = new StreetName
                {
                    Name = normalizedName,
                    NameLower = nameLower,
                    StreetType = streetType,
                    ProvinceCode = province?.ToUpper(),
                    Community = community,
                    OccurrenceCount = 1,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };
                
                _context.Set<StreetName>().Add(newStreetName);
            }

            await _context.SaveChangesAsync();
            
            // Clear cache for this province
            if (!string.IsNullOrWhiteSpace(province))
            {
                _cache.Remove($"{CACHE_KEY_PREFIX}{province.ToUpper()}");
            }
            _cache.Remove($"{CACHE_KEY_PREFIX}all");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording street name: {StreetName}", streetName);
        }
    }

    public async Task<List<StreetName>> GetStreetNamesAsync(string? province = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(province))
            {
                var cacheKey = $"{CACHE_KEY_PREFIX}all";
                return await _cache.GetOrCreateAsync(cacheKey, async entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = _cacheExpiration;
                    return await _context.Set<StreetName>()
                        .OrderByDescending(s => s.OccurrenceCount)
                        .ToListAsync();
                }) ?? new List<StreetName>();
            }

            var provinceCacheKey = $"{CACHE_KEY_PREFIX}{province.ToUpper()}";
            return await _cache.GetOrCreateAsync(provinceCacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = _cacheExpiration;
                var provinceCode = province.ToUpper();
                return await _context.Set<StreetName>()
                    .Where(s => s.ProvinceCode == provinceCode)
                    .OrderByDescending(s => s.OccurrenceCount)
                    .ToListAsync();
            }) ?? new List<StreetName>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching street names for province: {Province}", province);
            return new List<StreetName>();
        }
    }
}