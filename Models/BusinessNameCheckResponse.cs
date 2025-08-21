namespace IsBus.Models;

public class BusinessNameCheckResponse
{
    public string Input { get; set; } = string.Empty;
    public bool IsBusinessName { get; set; }
    public double Confidence { get; set; }
    public List<string> MatchedIndicators { get; set; } = new();
    public int WordsProcessed { get; set; }
}