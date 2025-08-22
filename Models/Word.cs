using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IsBus.Models;

[Table("words")]
public class Word
{
    [Key]
    [Column("word_id")]
    public int WordId { get; set; }

    [Required]
    [Column("word_lower")]
    [StringLength(255)]
    public string WordLower { get; set; } = string.Empty;

    [Column("word_count")]
    public int WordCount { get; set; }

    [Column("last_seen")]
    public DateTime LastSeen { get; set; } = DateTime.Now;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}