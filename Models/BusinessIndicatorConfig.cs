namespace IsBus.Models;

public class BusinessIndicatorConfig
{
    public List<string> PrimarySuffixes { get; set; } = new();
    public List<string> SecondaryIndicators { get; set; } = new();
    public List<string> CommonStopWords { get; set; } = new();
}