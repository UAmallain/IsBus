using IsBus.Data;
using IsBus.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace IsBus.Services;

/// <summary>
/// Enhanced street name service that uses the road_network table with 2.25M+ records
/// instead of the old street_names table
/// </summary>
public class EnhancedStreetNameService : IStreetNameService
{
    private readonly PhonebookContext _context;
    private readonly IMemoryCache _cache;
    private readonly ILogger<EnhancedStreetNameService> _logger;
    private const string CACHE_KEY_PREFIX = "enhanced_street_names_";
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromHours(4);

    public EnhancedStreetNameService(
        PhonebookContext context,
        IMemoryCache cache,
        ILogger<EnhancedStreetNameService> logger)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
    }

    // Removed hard-coded list - always use database

    public async Task<bool> IsKnownStreetNameAsync(string name, string? province = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        var normalizedName = name.Trim().ToLower();
        
        // Create a cache key based on name and province
        var cacheKey = $"{CACHE_KEY_PREFIX}exists_{normalizedName}_{province ?? "all"}";
        
        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);
            
            try
            {
                // Query the road_network table
                var query = _context.RoadNetworks
                    .Where(rn => rn.Name != null && rn.Name.ToLower() == normalizedName);

                // If province is specified, filter by it
                if (!string.IsNullOrEmpty(province))
                {
                    var provinceCode = ResolveProvinceCode(province);
                    if (!string.IsNullOrEmpty(provinceCode))
                    {
                        query = query.Where(rn => 
                            rn.ProvinceUidLeft == provinceCode || 
                            rn.ProvinceUidRight == provinceCode);
                    }
                }

                var exists = await query.AnyAsync();
                
                if (!exists)
                {
                    // Also check if the name matches the beginning of a street name
                    // This helps with cases like "Indian Mountain" being part of "Indian Mountain Road"
                    // BUT we need to be careful not to match business words
                    
                    // Only do partial match for multi-word inputs (avoid single words like "Company")
                    if (normalizedName.Contains(" "))
                    {
                        query = _context.RoadNetworks
                            .Where(rn => rn.Name != null && 
                                       (rn.Name.ToLower().StartsWith(normalizedName + " ") ||
                                        rn.Name.ToLower() == normalizedName));
                        
                        if (!string.IsNullOrEmpty(province))
                        {
                            var provinceCode = ResolveProvinceCode(province);
                            if (!string.IsNullOrEmpty(provinceCode))
                            {
                                query = query.Where(rn => 
                                    rn.ProvinceUidLeft == provinceCode || 
                                    rn.ProvinceUidRight == provinceCode);
                            }
                        }
                        
                        exists = await query.AnyAsync();
                    }
                }
                
                _logger.LogDebug($"Street name lookup: '{name}' in {province ?? "all"} = {exists}");
                return exists;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking street name: {name}");
                // No fallback - only use road_network table
                return false;
            }
        });
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

            // Check if this street exists in road_network first
            var existsInRoadNetwork = await _context.RoadNetworks
                .AnyAsync(rn => rn.Name != null && rn.Name.ToLower() == nameLower);

            if (existsInRoadNetwork)
            {
                _logger.LogDebug($"Street '{streetName}' already exists in road_network table");
                return;
            }

            // If not in road_network, record it in street_names for future reference
            var existing = await _context.StreetNames
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
                
                _context.StreetNames.Add(newStreetName);
            }

            await _context.SaveChangesAsync();
            
            // Clear cache for this province
            if (!string.IsNullOrWhiteSpace(province))
            {
                _cache.Remove($"{CACHE_KEY_PREFIX}all_{province.ToUpper()}");
            }
            _cache.Remove($"{CACHE_KEY_PREFIX}all_all");
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
            var cacheKey = $"{CACHE_KEY_PREFIX}all_{province ?? "all"}";
            
            return await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = _cacheExpiration;
                
                // First get unique street names from road_network
                var query = _context.RoadNetworks
                    .Where(rn => rn.Name != null)
                    .Select(rn => new { rn.Name, rn.Type, rn.ProvinceUidLeft, rn.ProvinceUidRight });

                if (!string.IsNullOrWhiteSpace(province))
                {
                    var provinceCode = ResolveProvinceCode(province);
                    if (!string.IsNullOrEmpty(provinceCode))
                    {
                        query = query.Where(rn => 
                            rn.ProvinceUidLeft == provinceCode || 
                            rn.ProvinceUidRight == provinceCode);
                    }
                }

                var roadNetworkStreets = await query
                    .GroupBy(x => new { x.Name, x.Type })
                    .Select(g => new StreetName
                    {
                        Name = g.Key.Name!,
                        NameLower = g.Key.Name!.ToLower(),
                        StreetType = g.Key.Type,
                        OccurrenceCount = g.Count(),
                        ProvinceCode = g.First().ProvinceUidLeft ?? g.First().ProvinceUidRight
                    })
                    .Take(1000) // Limit for performance
                    .ToListAsync();

                // Also include any custom street names from the street_names table
                var customStreets = await _context.StreetNames
                    .Where(s => province == null || s.ProvinceCode == province.ToUpper())
                    .ToListAsync();

                // Merge the lists, preferring road_network data
                var allStreets = roadNetworkStreets.Concat(customStreets)
                    .GroupBy(s => s.NameLower)
                    .Select(g => g.First())
                    .OrderByDescending(s => s.OccurrenceCount)
                    .ToList();

                return allStreets;
            }) ?? new List<StreetName>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching street names for province: {Province}", province);
            return new List<StreetName>();
        }
    }

    private string? ResolveProvinceCode(string? provinceCodeOrName)
    {
        if (string.IsNullOrWhiteSpace(provinceCodeOrName))
            return null;

        // Check if it's already a valid 2-char province code
        if (provinceCodeOrName.Length == 2 && 
            ProvinceMapping.ProvinceCodeToName.ContainsKey(provinceCodeOrName.ToUpper()))
        {
            return provinceCodeOrName.ToUpper();
        }

        // Try to resolve from province name
        return ProvinceMapping.GetProvinceCode(provinceCodeOrName);
    }
}