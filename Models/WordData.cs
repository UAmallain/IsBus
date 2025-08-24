using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IsBus.Models;

[Table("word_data")]
public class WordData
{
    [Key]
    [Column("word_id")]
    public int WordId { get; set; }

    [Required]
    [Column("word_lower")]
    [StringLength(255)]
    public string WordLower { get; set; } = string.Empty;

    [Required]
    [Column("word_type")]
    [StringLength(20)]
    public string WordType { get; set; } = "indeterminate"; // 'first', 'last', 'both', 'business', 'indeterminate'

    [Column("word_count")]
    public int WordCount { get; set; } = 1;

    [Column("last_seen")]
    public DateTime LastSeen { get; set; } = DateTime.Now;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

public enum WordTypeEnum
{
    First,
    Last,
    Both,
    Business,
    Indeterminate,
    Initial,     // Single letter (A-Z)
    Connector,   // & or "and"
    Unknown
}

public class WordContext
{
    public string Word { get; set; } = string.Empty;
    public int FirstCount { get; set; }
    public int LastCount { get; set; }
    public int BothCount { get; set; }
    public int BusinessCount { get; set; }
    public int IndeterminateCount { get; set; }
    public WordTypeEnum PrimaryType { get; set; }
    public int MaxCount { get; set; }
    
    public string GetContextString()
    {
        return $"{Word} (first={FirstCount}, last={LastCount}, both={BothCount}, business={BusinessCount} = {PrimaryType})";
    }
}