using IsBus.Data;
using IsBus.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace IsBus.Services;

public interface IRoadNetworkStreetService
{
    Task<bool> IsKnownStreetNameAsync(string name, string? provinceCodeOrName = null);
    Task<List<RoadNetworkStreetResult>> SearchStreetsAsync(string searchTerm, string? provinceCodeOrName = null, int maxResults = 100);
    Task<List<string>> GetUniqueStreetTypesAsync();
    Task<RoadNetworkStats> GetStreetStatsAsync(string? provinceCodeOrName = null);
}

public class RoadNetworkStreetResult
{
    public string StreetName { get; set; } = string.Empty;
    public string? StreetType { get; set; }
    public string? StreetTypeNormalized { get; set; }
    public string? StreetTypeAbbreviation { get; set; }
    public int OccurrenceCount { get; set; }
    public List<string> Communities { get; set; } = new();
    public string? ProvinceCode { get; set; }
    public string? ProvinceName { get; set; }
}

public class RoadNetworkStats
{
    public int TotalStreets { get; set; }
    public int UniqueStreetNames { get; set; }
    public int UniqueCommunities { get; set; }
    public Dictionary<string, int> StreetTypeDistribution { get; set; } = new();
}

public class RoadNetworkStreetService : IRoadNetworkStreetService
{
    private readonly PhonebookContext _context;
    private readonly IMemoryCache _cache;
    private readonly ILogger<RoadNetworkStreetService> _logger;
    private const string CACHE_KEY_PREFIX = "road_network_";
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromHours(4);

    public RoadNetworkStreetService(
        PhonebookContext context,
        IMemoryCache cache,
        ILogger<RoadNetworkStreetService> logger)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
    }

    public async Task<bool> IsKnownStreetNameAsync(string name, string? provinceCodeOrName = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        try
        {
            var normalizedName = name.Trim().ToLower();
            var provinceCode = ResolveProvinceCode(provinceCodeOrName);

            var query = _context.RoadNetworks
                .Where(rn => rn.Name != null && rn.Name.ToLower().Contains(normalizedName));

            if (!string.IsNullOrEmpty(provinceCode))
            {
                query = query.Where(rn => 
                    rn.ProvinceUidLeft == provinceCode || 
                    rn.ProvinceUidRight == provinceCode);
            }

            return await query.AnyAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking street name: {StreetName}", name);
            return false;
        }
    }

    public async Task<List<RoadNetworkStreetResult>> SearchStreetsAsync(
        string searchTerm, 
        string? provinceCodeOrName = null, 
        int maxResults = 100)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return new List<RoadNetworkStreetResult>();

        try
        {
            var normalizedSearch = searchTerm.Trim().ToLower();
            var provinceCode = ResolveProvinceCode(provinceCodeOrName);
            
            var cacheKey = $"{CACHE_KEY_PREFIX}search_{normalizedSearch}_{provinceCode ?? "all"}_{maxResults}";
            
            return await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = _cacheExpiration;
                
                var query = _context.RoadNetworks
                    .Where(rn => rn.Name != null && rn.Name.ToLower().Contains(normalizedSearch));

                if (!string.IsNullOrEmpty(provinceCode))
                {
                    query = query.Where(rn => 
                        rn.ProvinceUidLeft == provinceCode || 
                        rn.ProvinceUidRight == provinceCode);
                }

                var groupedResults = await query
                    .GroupBy(rn => new { rn.Name, rn.Type })
                    .Select(g => new
                    {
                        StreetName = g.Key.Name,
                        StreetType = g.Key.Type,
                        Count = g.Count(),
                        Communities = g.Select(x => x.CsdNameLeft ?? x.CsdNameRight).Distinct().ToList(),
                        ProvinceCode = g.Select(x => x.ProvinceUidLeft ?? x.ProvinceUidRight).FirstOrDefault()
                    })
                    .OrderByDescending(x => x.Count)
                    .Take(maxResults)
                    .ToListAsync();

                var results = new List<RoadNetworkStreetResult>();
                foreach (var item in groupedResults)
                {
                    var normalizedType = StreetTypeMapping.NormalizeStreetType(item.StreetType);
                    results.Add(new RoadNetworkStreetResult
                    {
                        StreetName = item.StreetName ?? string.Empty,
                        StreetType = item.StreetType,
                        StreetTypeNormalized = normalizedType,
                        StreetTypeAbbreviation = StreetTypeMapping.GetAbbreviation(normalizedType),
                        OccurrenceCount = item.Count,
                        Communities = item.Communities.Where(c => !string.IsNullOrEmpty(c)).Select(c => c!).ToList(),
                        ProvinceCode = item.ProvinceCode,
                        ProvinceName = ProvinceMapping.GetProvinceName(item.ProvinceCode)
                    });
                }

                return results;
            }) ?? new List<RoadNetworkStreetResult>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching streets with term: {SearchTerm}", searchTerm);
            return new List<RoadNetworkStreetResult>();
        }
    }

    public async Task<List<string>> GetUniqueStreetTypesAsync()
    {
        try
        {
            var cacheKey = $"{CACHE_KEY_PREFIX}street_types";
            
            return await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24);
                
                return await _context.RoadNetworks
                    .Where(rn => rn.Type != null && rn.Type != "")
                    .Select(rn => rn.Type!)
                    .Distinct()
                    .OrderBy(t => t)
                    .ToListAsync();
            }) ?? new List<string>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching unique street types");
            return new List<string>();
        }
    }

    public async Task<RoadNetworkStats> GetStreetStatsAsync(string? provinceCodeOrName = null)
    {
        try
        {
            var provinceCode = ResolveProvinceCode(provinceCodeOrName);
            var cacheKey = $"{CACHE_KEY_PREFIX}stats_{provinceCode ?? "all"}";
            
            return await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(12);
                
                var query = _context.RoadNetworks.AsQueryable();

                if (!string.IsNullOrEmpty(provinceCode))
                {
                    query = query.Where(rn => 
                        rn.ProvinceUidLeft == provinceCode || 
                        rn.ProvinceUidRight == provinceCode);
                }

                var stats = new RoadNetworkStats
                {
                    TotalStreets = await query.CountAsync(),
                    UniqueStreetNames = await query
                        .Where(rn => rn.Name != null)
                        .Select(rn => rn.Name)
                        .Distinct()
                        .CountAsync(),
                    UniqueCommunities = await query
                        .Select(rn => rn.CsdNameLeft ?? rn.CsdNameRight)
                        .Where(c => c != null)
                        .Distinct()
                        .CountAsync()
                };

                // Get street type distribution
                var typeGroups = await query
                    .Where(rn => rn.Type != null && rn.Type != "")
                    .GroupBy(rn => rn.Type!)
                    .Select(g => new { Type = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count)
                    .Take(20)
                    .ToListAsync();

                stats.StreetTypeDistribution = typeGroups.ToDictionary(x => x.Type, x => x.Count);

                return stats;
            }) ?? new RoadNetworkStats();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting street statistics");
            return new RoadNetworkStats();
        }
    }

    private string? ResolveProvinceCode(string? provinceCodeOrName)
    {
        if (string.IsNullOrWhiteSpace(provinceCodeOrName))
            return null;

        // Check if it's already a numeric SGC code (like "13" for NB)
        if (ProvinceCodeMapping.NumericToAlpha.ContainsKey(provinceCodeOrName))
        {
            return provinceCodeOrName; // Return the numeric code as-is
        }

        // Check if it's a 2-char province code (like "NB")
        if (provinceCodeOrName.Length == 2 && 
            ProvinceMapping.ProvinceCodeToName.ContainsKey(provinceCodeOrName.ToUpper()))
        {
            // Convert to numeric SGC code for database query
            return ProvinceCodeMapping.GetNumericCode(provinceCodeOrName.ToUpper());
        }

        // Try to resolve from province name
        var alphaCode = ProvinceMapping.GetProvinceCode(provinceCodeOrName);
        if (alphaCode != null)
        {
            // Convert to numeric SGC code for database query
            return ProvinceCodeMapping.GetNumericCode(alphaCode);
        }

        return null;
    }
}