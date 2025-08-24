using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IsBus.Models;

[Table("province_mapping")]
public class ProvinceMapping
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("province_code")]
    [StringLength(2)]
    public string ProvinceCode { get; set; } = string.Empty;

    [Required]
    [Column("province_name")]
    [StringLength(100)]
    public string ProvinceName { get; set; } = string.Empty;

    [Column("province_name_french")]
    [StringLength(100)]
    public string? ProvinceNameFrench { get; set; }

    [Column("region")]
    [StringLength(50)]
    public string? Region { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    // Static helper dictionary for quick lookups without DB access
    public static readonly Dictionary<string, string> ProvinceCodeToName = new()
    {
        { "NL", "Newfoundland and Labrador" },
        { "PE", "Prince Edward Island" },
        { "NS", "Nova Scotia" },
        { "NB", "New Brunswick" },
        { "QC", "Quebec" },
        { "ON", "Ontario" },
        { "MB", "Manitoba" },
        { "SK", "Saskatchewan" },
        { "AB", "Alberta" },
        { "BC", "British Columbia" },
        { "YT", "Yukon" },
        { "NT", "Northwest Territories" },
        { "NU", "Nunavut" }
    };

    public static readonly Dictionary<string, string> ProvinceNameToCode = new()
    {
        { "newfoundland and labrador", "NL" },
        { "prince edward island", "PE" },
        { "nova scotia", "NS" },
        { "new brunswick", "NB" },
        { "quebec", "QC" },
        { "ontario", "ON" },
        { "manitoba", "MB" },
        { "saskatchewan", "SK" },
        { "alberta", "AB" },
        { "british columbia", "BC" },
        { "yukon", "YT" },
        { "northwest territories", "NT" },
        { "nunavut", "NU" },
        // Common abbreviations and variations
        { "nfld", "NL" },
        { "pei", "PE" },
        { "nwt", "NT" },
        { "bc", "BC" },
        { "ab", "AB" },
        { "sk", "SK" },
        { "mb", "MB" },
        { "on", "ON" },
        { "qc", "QC" },
        { "nb", "NB" },
        { "ns", "NS" },
        { "pe", "PE" },
        { "nl", "NL" },
        { "yt", "YT" },
        { "nt", "NT" },
        { "nu", "NU" }
    };

    public static string? GetProvinceName(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;

        return ProvinceCodeToName.TryGetValue(code.ToUpper(), out var name) ? name : null;
    }

    public static string? GetProvinceCode(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        return ProvinceNameToCode.TryGetValue(name.ToLower().Trim(), out var code) ? code : null;
    }
}