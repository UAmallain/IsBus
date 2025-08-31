using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IsBus.Models;

[Table("street_types")]
public class StreetType
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("type_name")]
    [StringLength(50)]
    public string TypeName { get; set; } = string.Empty;

    [Column("type_abbr")]
    [StringLength(20)]
    public string? TypeAbbr { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }
}

[Table("business_endings")]
public class BusinessEnding
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("ending")]
    [StringLength(50)]
    public string Ending { get; set; } = string.Empty;

    [Required]
    [Column("ending_lower")]
    [StringLength(50)]
    public string EndingLower { get; set; } = string.Empty;

    [Column("full_form")]
    [StringLength(100)]
    public string? FullForm { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }
}

[Table("province_codes")]
public class ProvinceCode
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("code")]
    [StringLength(2)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [Column("name")]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Column("country")]
    [StringLength(2)]
    public string Country { get; set; } = "CA";

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }
}

[Table("skip_words")]
public class SkipWord
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("word")]
    [StringLength(50)]
    public string Word { get; set; } = string.Empty;

    [Required]
    [Column("word_lower")]
    [StringLength(50)]
    public string WordLower { get; set; } = string.Empty;

    [Column("context")]
    [StringLength(50)]
    public string Context { get; set; } = "general";

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }
}

[Table("road_indicators")]
public class RoadIndicator
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("indicator")]
    [StringLength(50)]
    public string Indicator { get; set; } = string.Empty;

    [Required]
    [Column("indicator_lower")]
    [StringLength(50)]
    public string IndicatorLower { get; set; } = string.Empty;

    [Column("indicator_type")]
    [StringLength(50)]
    public string IndicatorType { get; set; } = "suffix";

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }
}

[Table("suite_indicators")]
public class SuiteIndicator
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("indicator")]
    [StringLength(50)]
    public string Indicator { get; set; } = string.Empty;

    [Required]
    [Column("indicator_lower")]
    [StringLength(50)]
    public string IndicatorLower { get; set; } = string.Empty;

    [Column("requires_number")]
    public bool RequiresNumber { get; set; } = true;

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }
}

[Table("business_context_words")]
public class BusinessContextWord
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("word")]
    [StringLength(50)]
    public string Word { get; set; } = string.Empty;

    [Required]
    [Column("word_lower")]
    [StringLength(50)]
    public string WordLower { get; set; } = string.Empty;

    [Column("context_type")]
    [StringLength(50)]
    public string ContextType { get; set; } = "general";

    [Column("strength")]
    public int Strength { get; set; } = 50;

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }
}