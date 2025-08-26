using IsBus.Data;
using IsBus.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Data;

namespace IsBus.Services;

public interface ICommunityService
{
    Task<List<Community>> GetCommunitiesByProvinceAsync(string? province);
    Task<Community?> FindCommunityAsync(string name, string? province = null);
    Task<bool> IsCommunityNameAsync(string name, string? province = null);
    Task<(bool Found, string? CommunityName, int StartIndex)> FindCommunityAtEndAsync(string text, string? province = null);
}

public class CommunityService : ICommunityService
{
    private readonly PhonebookContext _context;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CommunityService> _logger;
    private const string CACHE_KEY_PREFIX = "communities_";
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromHours(1);
    
    public CommunityService(
        PhonebookContext context,
        IMemoryCache cache,
        ILogger<CommunityService> logger)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
    }
    
    public async Task<List<Community>> GetCommunitiesByProvinceAsync(string? province)
    {
        if (string.IsNullOrWhiteSpace(province))
        {
            // Return all communities if no province specified
            var cacheKey = $"{CACHE_KEY_PREFIX}all";
            return await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = _cacheExpiration;
                return await _context.Communities.ToListAsync();
            }) ?? new List<Community>();
        }
        
        var provinceCacheKey = $"{CACHE_KEY_PREFIX}{province.ToUpper()}";
        return await _cache.GetOrCreateAsync(provinceCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = _cacheExpiration;
            // Use ProvinceCode column and convert province to uppercase 2-letter code
            var provinceCode = province.ToUpper();
            if (provinceCode.Length > 2)
            {
                // Convert full name to code if needed (e.g., "New Brunswick" to "NB")
                provinceCode = provinceCode.Substring(0, 2);
            }
            return await _context.Communities
                .Where(c => c.ProvinceCode == provinceCode)
                .ToListAsync();
        }) ?? new List<Community>();
    }
    
    public async Task<Community?> FindCommunityAsync(string name, string? province = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;
        
        var normalizedName = name.Trim().ToLower();
        var communities = await GetCommunitiesByProvinceAsync(province);
        
        // Use NameLower for efficient case-insensitive matching
        var community = communities.FirstOrDefault(c => 
            c.NameLower == normalizedName);
        
        return community;
    }
    
    public async Task<bool> IsCommunityNameAsync(string name, string? province = null)
    {
        var community = await FindCommunityAsync(name, province);
        return community != null;
    }
    
    /// <summary>
    /// Checks if the end of the text contains a community name (handles multi-word communities)
    /// Returns the community name and its starting position if found
    /// </summary>
    public async Task<(bool Found, string? CommunityName, int StartIndex)> FindCommunityAtEndAsync(
        string text, string? province = null)
    {
        if (string.IsNullOrWhiteSpace(text))
            return (false, null, -1);
        
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
            return (false, null, -1);
        
        var communities = await GetCommunitiesByProvinceAsync(province);
        
        // Try matching from longest possible community name to shortest
        // Start with last 3 words, then 2, then 1
        for (int wordsToCheck = Math.Min(3, words.Length); wordsToCheck >= 1; wordsToCheck--)
        {
            var potentialCommunity = string.Join(" ", words.Skip(words.Length - wordsToCheck));
            var normalizedName = potentialCommunity.ToLower();
            
            var community = communities.FirstOrDefault(c => c.NameLower == normalizedName);
            if (community != null)
            {
                // Calculate the character position where this community starts
                int charPos = 0;
                for (int i = 0; i < words.Length - wordsToCheck; i++)
                {
                    charPos += words[i].Length + 1; // +1 for space
                }
                
                return (true, community.CommunityName, charPos);
            }
        }
        
        return (false, null, -1);
    }
}