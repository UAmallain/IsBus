namespace IsBus.Models;

/// <summary>
/// Maps between Statistics Canada SGC province codes and standard 2-letter codes
/// </summary>
public static class ProvinceCodeMapping
{
    /// <summary>
    /// Statistics Canada SGC numeric codes to 2-letter province codes
    /// </summary>
    public static readonly Dictionary<string, string> NumericToAlpha = new()
    {
        { "10", "NL" }, // Newfoundland and Labrador
        { "11", "PE" }, // Prince Edward Island
        { "12", "NS" }, // Nova Scotia
        { "13", "NB" }, // New Brunswick
        { "24", "QC" }, // Quebec
        { "35", "ON" }, // Ontario
        { "46", "MB" }, // Manitoba
        { "47", "SK" }, // Saskatchewan
        { "48", "AB" }, // Alberta
        { "59", "BC" }, // British Columbia
        { "60", "YT" }, // Yukon
        { "61", "NT" }, // Northwest Territories
        { "62", "NU" }  // Nunavut
    };

    /// <summary>
    /// 2-letter province codes to Statistics Canada SGC numeric codes
    /// </summary>
    public static readonly Dictionary<string, string> AlphaToNumeric = new()
    {
        { "NL", "10" }, // Newfoundland and Labrador
        { "PE", "11" }, // Prince Edward Island
        { "NS", "12" }, // Nova Scotia
        { "NB", "13" }, // New Brunswick
        { "QC", "24" }, // Quebec
        { "ON", "35" }, // Ontario
        { "MB", "46" }, // Manitoba
        { "SK", "47" }, // Saskatchewan
        { "AB", "48" }, // Alberta
        { "BC", "59" }, // British Columbia
        { "YT", "60" }, // Yukon
        { "NT", "61" }, // Northwest Territories
        { "NU", "62" }  // Nunavut
    };

    /// <summary>
    /// Convert a 2-letter province code to SGC numeric code
    /// </summary>
    public static string? GetNumericCode(string? alphaCode)
    {
        if (string.IsNullOrWhiteSpace(alphaCode))
            return null;

        return AlphaToNumeric.TryGetValue(alphaCode.ToUpper(), out var numeric) ? numeric : null;
    }

    /// <summary>
    /// Convert an SGC numeric code to 2-letter province code
    /// </summary>
    public static string? GetAlphaCode(string? numericCode)
    {
        if (string.IsNullOrWhiteSpace(numericCode))
            return null;

        return NumericToAlpha.TryGetValue(numericCode, out var alpha) ? alpha : null;
    }
}