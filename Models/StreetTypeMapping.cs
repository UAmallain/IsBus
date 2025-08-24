using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IsBus.Models;

[Table("street_type_mapping")]
public class StreetTypeMapping
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("abbreviation")]
    [StringLength(20)]
    public string Abbreviation { get; set; } = string.Empty;

    [Required]
    [Column("full_name")]
    [StringLength(50)]
    public string FullName { get; set; } = string.Empty;

    [Column("french_name")]
    [StringLength(50)]
    public string? FrenchName { get; set; }

    [Column("category")]
    [StringLength(30)]
    public string? Category { get; set; }

    [Column("is_primary")]
    public bool IsPrimary { get; set; }

    [Column("display_order")]
    public int DisplayOrder { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    // Static dictionaries for quick lookups without DB access
    public static readonly Dictionary<string, string> CommonAbbreviations = new()
    {
        // Primary road types
        { "st", "Street" },
        { "ave", "Avenue" },
        { "av", "Avenue" },
        { "rd", "Road" },
        { "dr", "Drive" },
        { "blvd", "Boulevard" },
        { "boul", "Boulevard" },
        { "way", "Way" },
        { "wy", "Way" },
        { "pl", "Place" },
        { "cres", "Crescent" },
        { "cresc", "Crescent" },
        { "ct", "Court" },
        { "crt", "Court" },
        { "ln", "Lane" },
        { "terr", "Terrace" },
        { "ter", "Terrace" },
        { "cir", "Circle" },
        { "crcl", "Circle" },
        { "sq", "Square" },
        { "pk", "Park" },
        { "pkwy", "Parkway" },
        { "pky", "Parkway" },
        
        // Highway types
        { "hwy", "Highway" },
        { "fwy", "Freeway" },
        { "expy", "Expressway" },
        { "tpke", "Turnpike" },
        { "rte", "Route" },
        
        // Secondary road types
        { "tr", "Trail" },
        { "trl", "Trail" },
        { "path", "Path" },
        { "walk", "Walk" },
        { "row", "Row" },
        { "gate", "Gate" },
        { "grn", "Green" },
        { "mall", "Mall" },
        { "alley", "Alley" },
        { "loop", "Loop" },
        { "ramp", "Ramp" },
        
        // Geographic features
        { "bay", "Bay" },
        { "beach", "Beach" },
        { "bend", "Bend" },
        { "cape", "Cape" },
        { "cliff", "Cliff" },
        { "cove", "Cove" },
        { "dale", "Dale" },
        { "dell", "Dell" },
        { "glen", "Glen" },
        { "grove", "Grove" },
        { "hill", "Hill" },
        { "hts", "Heights" },
        { "ht", "Height" },
        { "island", "Island" },
        { "isle", "Isle" },
        { "lake", "Lake" },
        { "mdw", "Meadow" },
        { "mtn", "Mountain" },
        { "mt", "Mount" },
        { "orch", "Orchard" },
        { "pt", "Point" },
        { "ridge", "Ridge" },
        { "rdg", "Ridge" },
        { "shore", "Shore" },
        { "vale", "Vale" },
        { "valley", "Valley" },
        { "view", "View" },
        { "wood", "Wood" },
        { "woods", "Woods" },
        
        // Development types
        { "gdns", "Gardens" },
        { "gdn", "Garden" },
        { "est", "Estate" },
        { "mnr", "Manor" },
        { "villas", "Villas" },
        { "villg", "Village" },
        { "cmn", "Common" },
        { "ctr", "Centre" },
        { "plz", "Plaza" },
        
        // Canadian-specific
        { "conc", "Concession" },
        { "line", "Line" },
        { "rg", "Range" },
        { "rang", "Rang" },
        { "sdrd", "Sideroad" },
        
        // French Canadian
        { "ch", "Chemin" },
        { "rue", "Rue" },
        { "mont", "Montée" },
        { "imp", "Impasse" }
    };

    public static readonly Dictionary<string, string> FullNameToAbbreviation = new()
    {
        { "street", "st" },
        { "avenue", "ave" },
        { "road", "rd" },
        { "drive", "dr" },
        { "boulevard", "blvd" },
        { "way", "way" },
        { "place", "pl" },
        { "crescent", "cres" },
        { "court", "ct" },
        { "lane", "ln" },
        { "terrace", "terr" },
        { "circle", "cir" },
        { "square", "sq" },
        { "park", "pk" },
        { "parkway", "pkwy" },
        { "highway", "hwy" },
        { "freeway", "fwy" },
        { "expressway", "expy" },
        { "turnpike", "tpke" },
        { "route", "rte" },
        { "trail", "tr" },
        { "path", "path" },
        { "walk", "walk" },
        { "row", "row" },
        { "gate", "gate" },
        { "green", "grn" },
        { "mall", "mall" },
        { "alley", "alley" },
        { "loop", "loop" },
        { "ramp", "ramp" },
        { "bay", "bay" },
        { "beach", "beach" },
        { "bend", "bend" },
        { "cape", "cape" },
        { "cliff", "cliff" },
        { "cove", "cove" },
        { "dale", "dale" },
        { "dell", "dell" },
        { "glen", "glen" },
        { "grove", "grove" },
        { "hill", "hill" },
        { "heights", "hts" },
        { "height", "ht" },
        { "island", "island" },
        { "isle", "isle" },
        { "lake", "lake" },
        { "meadow", "mdw" },
        { "mountain", "mtn" },
        { "mount", "mt" },
        { "orchard", "orch" },
        { "point", "pt" },
        { "ridge", "ridge" },
        { "shore", "shore" },
        { "vale", "vale" },
        { "valley", "valley" },
        { "view", "view" },
        { "wood", "wood" },
        { "woods", "woods" },
        { "gardens", "gdns" },
        { "garden", "gdn" },
        { "estate", "est" },
        { "manor", "mnr" },
        { "villas", "villas" },
        { "village", "villg" },
        { "common", "cmn" },
        { "centre", "ctr" },
        { "center", "ctr" },
        { "plaza", "plz" },
        { "concession", "conc" },
        { "line", "line" },
        { "range", "rg" },
        { "rang", "rang" },
        { "sideroad", "sdrd" },
        { "chemin", "ch" },
        { "rue", "rue" },
        { "montée", "mont" },
        { "impasse", "imp" }
    };

    /// <summary>
    /// Normalizes a street type abbreviation to its full name
    /// </summary>
    public static string NormalizeStreetType(string? streetType)
    {
        if (string.IsNullOrWhiteSpace(streetType))
            return string.Empty;

        var lower = streetType.ToLower().Trim();
        
        // Check if it's an abbreviation
        if (CommonAbbreviations.TryGetValue(lower, out var fullName))
            return fullName;
        
        // Check if it's already a full name (return with proper casing)
        var matchingFullName = FullNameToAbbreviation.Keys
            .FirstOrDefault(k => k.Equals(lower, StringComparison.OrdinalIgnoreCase));
        
        if (matchingFullName != null)
        {
            // Return with proper casing
            return CommonAbbreviations.Values
                .FirstOrDefault(v => v.Equals(matchingFullName, StringComparison.OrdinalIgnoreCase)) 
                ?? streetType;
        }
        
        // Return original if no match found
        return streetType;
    }

    /// <summary>
    /// Gets the abbreviation for a full street type name
    /// </summary>
    public static string GetAbbreviation(string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            return string.Empty;

        var lower = fullName.ToLower().Trim();
        
        // Check if it's already an abbreviation
        if (CommonAbbreviations.ContainsKey(lower))
            return lower;
        
        // Get abbreviation from full name
        return FullNameToAbbreviation.TryGetValue(lower, out var abbreviation) 
            ? abbreviation 
            : fullName.ToLower();
    }

    /// <summary>
    /// Checks if a given string is a known street type (either abbreviation or full name)
    /// </summary>
    public static bool IsKnownStreetType(string? streetType)
    {
        if (string.IsNullOrWhiteSpace(streetType))
            return false;

        var lower = streetType.ToLower().Trim();
        return CommonAbbreviations.ContainsKey(lower) || 
               FullNameToAbbreviation.ContainsKey(lower);
    }

    /// <summary>
    /// Gets all street type categories
    /// </summary>
    public static readonly List<string> Categories = new()
    {
        "road",
        "highway",
        "geographic",
        "development"
    };
}