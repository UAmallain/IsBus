using System.ComponentModel.DataAnnotations;

namespace IsBus.Models;

public class BusinessNameCheckRequest
{
    [Required]
    [StringLength(500, MinimumLength = 1)]
    public string Input { get; set; } = string.Empty;
}