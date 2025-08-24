using IsBus.Services;
using Microsoft.AspNetCore.Mvc;

namespace IsBus.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StreetSearchController : ControllerBase
{
    private readonly IRoadNetworkStreetService _streetService;
    private readonly ILogger<StreetSearchController> _logger;

    public StreetSearchController(
        IRoadNetworkStreetService streetService,
        ILogger<StreetSearchController> logger)
    {
        _streetService = streetService;
        _logger = logger;
    }

    /// <summary>
    /// Search for streets in the road network database
    /// </summary>
    /// <param name="searchTerm">The street name to search for</param>
    /// <param name="province">Optional: Province code (e.g., 'ON') or name (e.g., 'Ontario')</param>
    /// <param name="maxResults">Maximum number of results to return (default: 100)</param>
    [HttpGet("search")]
    public async Task<IActionResult> SearchStreets(
        [FromQuery] string searchTerm,
        [FromQuery] string? province = null,
        [FromQuery] int maxResults = 100)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return BadRequest(new { error = "Search term is required" });
        }

        if (searchTerm.Length < 2)
        {
            return BadRequest(new { error = "Search term must be at least 2 characters" });
        }

        try
        {
            var results = await _streetService.SearchStreetsAsync(searchTerm, province, maxResults);
            
            return Ok(new
            {
                searchTerm,
                province,
                totalResults = results.Count,
                results
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching streets with term: {SearchTerm}", searchTerm);
            return StatusCode(500, new { error = "An error occurred while searching streets" });
        }
    }

    /// <summary>
    /// Check if a street name exists in the database
    /// </summary>
    /// <param name="name">The street name to check</param>
    /// <param name="province">Optional: Province code or name</param>
    [HttpGet("exists")]
    public async Task<IActionResult> CheckStreetExists(
        [FromQuery] string name,
        [FromQuery] string? province = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest(new { error = "Street name is required" });
        }

        try
        {
            var exists = await _streetService.IsKnownStreetNameAsync(name, province);
            
            return Ok(new
            {
                streetName = name,
                province,
                exists
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking street name: {Name}", name);
            return StatusCode(500, new { error = "An error occurred while checking street name" });
        }
    }

    /// <summary>
    /// Get all unique street types in the database
    /// </summary>
    [HttpGet("types")]
    public async Task<IActionResult> GetStreetTypes()
    {
        try
        {
            var types = await _streetService.GetUniqueStreetTypesAsync();
            
            return Ok(new
            {
                totalTypes = types.Count,
                types
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching street types");
            return StatusCode(500, new { error = "An error occurred while fetching street types" });
        }
    }

    /// <summary>
    /// Get statistics about streets in the database
    /// </summary>
    /// <param name="province">Optional: Province code or name to filter statistics</param>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStreetStatistics([FromQuery] string? province = null)
    {
        try
        {
            var stats = await _streetService.GetStreetStatsAsync(province);
            
            return Ok(new
            {
                province,
                statistics = stats
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching street statistics");
            return StatusCode(500, new { error = "An error occurred while fetching statistics" });
        }
    }

    /// <summary>
    /// Get the list of supported provinces with their codes and names
    /// </summary>
    [HttpGet("provinces")]
    public IActionResult GetProvinces()
    {
        var provinces = Models.ProvinceMapping.ProvinceCodeToName
            .Select(kvp => new
            {
                code = kvp.Key,
                name = kvp.Value
            })
            .OrderBy(p => p.name);

        return Ok(new
        {
            totalProvinces = provinces.Count(),
            provinces
        });
    }

    /// <summary>
    /// Get street type abbreviation mappings
    /// </summary>
    [HttpGet("type-mappings")]
    public IActionResult GetStreetTypeMappings()
    {
        var mappings = Models.StreetTypeMapping.CommonAbbreviations
            .Select(kvp => new
            {
                abbreviation = kvp.Key,
                fullName = kvp.Value
            })
            .OrderBy(m => m.fullName)
            .ThenBy(m => m.abbreviation);

        return Ok(new
        {
            totalMappings = mappings.Count(),
            categories = Models.StreetTypeMapping.Categories,
            mappings
        });
    }

    /// <summary>
    /// Normalize a street type (convert abbreviation to full name)
    /// </summary>
    /// <param name="streetType">The street type to normalize (e.g., 'rd', 'ave', 'street')</param>
    [HttpGet("normalize-type")]
    public IActionResult NormalizeStreetType([FromQuery] string streetType)
    {
        if (string.IsNullOrWhiteSpace(streetType))
        {
            return BadRequest(new { error = "Street type is required" });
        }

        var normalized = Models.StreetTypeMapping.NormalizeStreetType(streetType);
        var abbreviation = Models.StreetTypeMapping.GetAbbreviation(normalized);
        var isKnown = Models.StreetTypeMapping.IsKnownStreetType(streetType);

        return Ok(new
        {
            input = streetType,
            normalized = normalized,
            abbreviation = abbreviation,
            isKnownType = isKnown
        });
    }

    /// <summary>
    /// Get abbreviation for a street type
    /// </summary>
    /// <param name="fullName">The full street type name (e.g., 'Street', 'Avenue')</param>
    [HttpGet("get-abbreviation")]
    public IActionResult GetStreetAbbreviation([FromQuery] string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return BadRequest(new { error = "Full name is required" });
        }

        var abbreviation = Models.StreetTypeMapping.GetAbbreviation(fullName);
        var isKnown = Models.StreetTypeMapping.IsKnownStreetType(fullName);

        return Ok(new
        {
            fullName = fullName,
            abbreviation = abbreviation,
            isKnownType = isKnown
        });
    }
}