using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IsBus.Models;

[Table("street_names")]
public class StreetName
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("street_name")]
    [StringLength(255)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [Column("street_name_lower")]
    [StringLength(255)]
    public string NameLower { get; set; } = string.Empty;

    [Column("street_type")]
    [StringLength(50)]
    public string? StreetType { get; set; }

    [Column("province_code")]
    [StringLength(2)]
    public string? ProvinceCode { get; set; }

    [Column("community")]
    [StringLength(255)]
    public string? Community { get; set; }

    [Column("occurrence_count")]
    public int OccurrenceCount { get; set; } = 1;

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }
}