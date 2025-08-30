using System.Collections.Generic;
using System.Threading.Tasks;

namespace IsBus.Services;

public enum BusinessIndicatorStrength
{
    None = 0,
    Weak = 1,      // word_count 10-99, or has name entry
    Medium = 2,    // word_count 100-999, no name entry
    Strong = 3,    // word_count 1000-4999, no name entry
    Absolute = 4   // word_count >= 5000, no name entry OR corporate suffix
}

public interface IBusinessWordService
{
    /// <summary>
    /// Gets the business indicator strength for a single word based on word_data table
    /// </summary>
    Task<BusinessIndicatorStrength> GetWordStrengthAsync(string word);
    
    /// <summary>
    /// Checks if a word is a strong business indicator (count >= 1000 with no name entry)
    /// </summary>
    Task<bool> IsStrongBusinessWordAsync(string word);
    
    /// <summary>
    /// Checks if a word is a corporate suffix (Inc, Ltd, Corp, etc.)
    /// </summary>
    Task<bool> IsCorporateSuffixAsync(string word);
    
    /// <summary>
    /// Analyzes multiple words and returns their business strengths
    /// </summary>
    Task<Dictionary<string, BusinessIndicatorStrength>> AnalyzeWordsAsync(string[] words);
    
    /// <summary>
    /// Determines if a phrase contains enough business indicators to classify as business
    /// </summary>
    Task<(bool isBusiness, BusinessIndicatorStrength maxStrength, string reason)> AnalyzePhraseAsync(string phrase);
}