using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IsBus.Models;

/// <summary>
/// Simplified Community model that only maps essential columns
/// to avoid column mismatch errors
/// </summary>
[Table("communities")]
public class CommunitySimple
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("community_name")]
    public string CommunityName { get; set; } = string.Empty;

    [Column("province")]
    public string? Province { get; set; }
    
    // Ignore other columns that might not exist or have different names
    [NotMapped]
    public string? Region { get; set; }
}