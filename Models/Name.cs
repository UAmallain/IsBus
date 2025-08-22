using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IsBus.Models;

[Table("names")]
public class Name
{
    [Key]
    [Column("name_id")]
    public int NameId { get; set; }

    [Required]
    [Column("name_lower")]
    [StringLength(255)]
    public string NameLower { get; set; } = string.Empty;

    [Required]
    [Column("name_type")]
    [StringLength(10)]
    public string NameType { get; set; } = "both"; // 'first', 'last', or 'both'

    [Column("name_count")]
    public int NameCount { get; set; } = 1;

    [Column("last_seen")]
    public DateTime LastSeen { get; set; } = DateTime.Now;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

public enum NameTypeEnum
{
    First,
    Last,
    Both
}