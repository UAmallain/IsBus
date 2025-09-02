using System.Threading.Tasks;
using System.Collections.Generic;

namespace IsBus.Services;

public class AddressVerificationResult
{
    public bool IsValid { get; set; }
    public string? StreetName { get; set; }
    public string? StreetType { get; set; }
    public string? CityName { get; set; }
    public string? Province { get; set; }
    public string? ProvinceCode { get; set; }
    public int? CivicNumber { get; set; }
    public bool IsInRange { get; set; }
    public string? FormattedAddress { get; set; }
    public double Confidence { get; set; }
    public string? MatchQuality { get; set; } // "exact", "partial", "none"
    public List<string> ValidationMessages { get; set; } = new();
}

public class CityVerificationResult
{
    public bool IsCity { get; set; }
    public string? CityName { get; set; }
    public string? Province { get; set; }
    public string? ProvinceCode { get; set; }
    public int MatchCount { get; set; }
    public double Confidence { get; set; }
}

public interface IAddressVerificationService
{
    /// <summary>
    /// Verify if an address is valid based on road network data
    /// </summary>
    Task<AddressVerificationResult> VerifyAddressAsync(string address, string? provinceCode = null);
    
    /// <summary>
    /// Verify if a city name exists in the road network
    /// </summary>
    Task<CityVerificationResult> VerifyCityAsync(string cityName, string? provinceCode = null);
    
    /// <summary>
    /// Extract and verify multiple potential locations from text
    /// </summary>
    Task<List<CityVerificationResult>> VerifyMultipleLocationsAsync(string text, string? provinceCode = null);
    
    /// <summary>
    /// Get all unique cities/towns in a province
    /// </summary>
    Task<List<string>> GetCitiesInProvinceAsync(string provinceCode);
    
    /// <summary>
    /// Get all streets in a specific city
    /// </summary>
    Task<List<string>> GetStreetsInCityAsync(string cityName, string? provinceCode = null);
}