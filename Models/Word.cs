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
}