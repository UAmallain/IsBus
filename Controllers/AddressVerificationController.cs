using Microsoft.AspNetCore.Mvc;
using IsBus.Services;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace IsBus.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AddressVerificationController : ControllerBase
{
    private readonly IAddressVerificationService _verificationService;
    private readonly ILogger<AddressVerificationController> _logger;
    
    public AddressVerificationController(
        IAddressVerificationService verificationService,
        ILogger<AddressVerificationController> logger)
    {
        _verificationService = verificationService;
        _logger = logger;
    }
    
    /// <summary>
    /// Verify if an address is valid based on road network data
    /// </summary>
    [HttpPost("verify-address")]
    public async Task<IActionResult> VerifyAddress([FromBody] VerifyAddressRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Address))
        {
            return BadRequest(new { error = "Address is required" });
        }
        
        try
        {
            var result = await _verificationService.VerifyAddressAsync(request.Address, request.ProvinceCode);
            
            return Ok(new
            {
                isValid = result.IsValid,
                streetName = result.StreetName,
                streetType = result.StreetType,
                cityName = result.CityName,
                province = result.Province,
                provinceCode = result.ProvinceCode,
                civicNumber = result.CivicNumber,
                isInRange = result.IsInRange,
                formattedAddress = result.FormattedAddress,
                confidence = result.Confidence,
                matchQuality = result.MatchQuality,
                validationMessages = result.ValidationMessages
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying address: {Address}", request.Address);
            return StatusCode(500, new { error = "An error occurred while verifying the address" });
        }
    }
    
    /// <summary>
    /// Verify if a city name exists in the road network
    /// </summary>
    [HttpPost("verify-city")]
    public async Task<IActionResult> VerifyCity([FromBody] VerifyCityRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CityName))
        {
            return BadRequest(new { error = "City name is required" });
        }
        
        try
        {
            var result = await _verificationService.VerifyCityAsync(request.CityName, request.ProvinceCode);
            
            return Ok(new
            {
                isCity = result.IsCity,
                cityName = result.CityName,
                province = result.Province,
                provinceCode = result.ProvinceCode,
                matchCount = result.MatchCount,
                confidence = result.Confidence
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying city: {CityName}", request.CityName);
            return StatusCode(500, new { error = "An error occurred while verifying the city" });
        }
    }
    
    /// <summary>
    /// Extract and verify multiple potential locations from text
    /// </summary>
    [HttpPost("verify-locations")]
    public async Task<IActionResult> VerifyMultipleLocations([FromBody] VerifyLocationsRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
        {
            return BadRequest(new { error = "Text is required" });
        }
        
        try
        {
            var results = await _verificationService.VerifyMultipleLocationsAsync(request.Text, request.ProvinceCode);
            
            var response = results.Select(r => new
            {
                isCity = r.IsCity,
                cityName = r.CityName,
                province = r.Province,
                provinceCode = r.ProvinceCode,
                matchCount = r.MatchCount,
                confidence = r.Confidence
            });
            
            return Ok(new
            {
                locationsFound = results.Count,
                locations = response
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying locations in text: {Text}", request.Text);
            return StatusCode(500, new { error = "An error occurred while verifying locations" });
        }
    }
    
    /// <summary>
    /// Get all unique cities/towns in a province
    /// </summary>
    [HttpGet("cities/{provinceCode}")]
    public async Task<IActionResult> GetCitiesInProvince(string provinceCode)
    {
        if (string.IsNullOrWhiteSpace(provinceCode))
        {
            return BadRequest(new { error = "Province code is required" });
        }
        
        try
        {
            var cities = await _verificationService.GetCitiesInProvinceAsync(provinceCode);
            
            return Ok(new
            {
                provinceCode = provinceCode.ToUpper(),
                cityCount = cities.Count,
                cities = cities
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cities for province: {ProvinceCode}", provinceCode);
            return StatusCode(500, new { error = "An error occurred while retrieving cities" });
        }
    }
    
    /// <summary>
    /// Get all streets in a specific city
    /// </summary>
    [HttpGet("streets")]
    public async Task<IActionResult> GetStreetsInCity([FromQuery] string cityName, [FromQuery] string? provinceCode = null)
    {
        if (string.IsNullOrWhiteSpace(cityName))
        {
            return BadRequest(new { error = "City name is required" });
        }
        
        try
        {
            var streets = await _verificationService.GetStreetsInCityAsync(cityName, provinceCode);
            
            return Ok(new
            {
                cityName = cityName,
                provinceCode = provinceCode,
                streetCount = streets.Count,
                streets = streets
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting streets for city: {CityName}", cityName);
            return StatusCode(500, new { error = "An error occurred while retrieving streets" });
        }
    }
    
    /// <summary>
    /// Batch verify multiple addresses
    /// </summary>
    [HttpPost("batch-verify")]
    public async Task<IActionResult> BatchVerifyAddresses([FromBody] BatchVerifyRequest request)
    {
        if (request.Addresses == null || !request.Addresses.Any())
        {
            return BadRequest(new { error = "At least one address is required" });
        }
        
        try
        {
            var results = new List<object>();
            
            foreach (var address in request.Addresses)
            {
                var result = await _verificationService.VerifyAddressAsync(address, request.ProvinceCode);
                results.Add(new
                {
                    input = address,
                    isValid = result.IsValid,
                    formattedAddress = result.FormattedAddress,
                    confidence = result.Confidence,
                    matchQuality = result.MatchQuality,
                    cityName = result.CityName,
                    validationMessages = result.ValidationMessages
                });
            }
            
            var validCount = results.Count(r => ((dynamic)r).isValid);
            
            return Ok(new
            {
                totalProcessed = results.Count,
                validCount = validCount,
                invalidCount = results.Count - validCount,
                results = results
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error batch verifying addresses");
            return StatusCode(500, new { error = "An error occurred while batch verifying addresses" });
        }
    }
}

// Request DTOs
public class VerifyAddressRequest
{
    public string Address { get; set; } = string.Empty;
    public string? ProvinceCode { get; set; }
}

public class VerifyCityRequest
{
    public string CityName { get; set; } = string.Empty;
    public string? ProvinceCode { get; set; }
}

public class VerifyLocationsRequest
{
    public string Text { get; set; } = string.Empty;
    public string? ProvinceCode { get; set; }
}

public class BatchVerifyRequest
{
    public List<string> Addresses { get; set; } = new();
    public string? ProvinceCode { get; set; }
}