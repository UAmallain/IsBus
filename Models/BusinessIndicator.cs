using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IsBus.Models;

[Table("business_indicators")]
public class BusinessIndicator
{
    [Key]
    [Column("indicator_id")]
    public int IndicatorId { get; set; }

    [Required]
    [Column("indicator_text")]
    [StringLength(100)]
    public string IndicatorText { get; set; } = string.Empty;

    [Required]
    [Column("indicator_type")]
    [StringLength(50)]
    public string IndicatorType { get; set; } = string.Empty;

    [Column("weight")]
    public int? Weight { get; set; }

    [Column("is_active")]
    public bool? IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }
}