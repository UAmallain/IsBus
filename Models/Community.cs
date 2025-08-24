using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IsBus.Models;

[Table("communities")]
public class Community
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("name")]
    [StringLength(255)]
    public string CommunityName { get; set; } = string.Empty;

    [Required]
    [Column("name_lower")]
    [StringLength(255)]
    public string NameLower { get; set; } = string.Empty;

    [Required]
    [Column("province_code")]
    [StringLength(2)]
    public string ProvinceCode { get; set; } = string.Empty;

    [Required]
    [Column("community_type")]
    [StringLength(50)]
    public string CommunityType { get; set; } = string.Empty;

    [Column("word_count")]
    public int WordCount { get; set; } = 1;

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }
    
    // For compatibility with the service
    [NotMapped]
    public string? Province => ProvinceCode;
}