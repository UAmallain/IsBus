using System.Threading.Tasks;
using IsBus.Models;

namespace IsBus.Services;

/// <summary>
/// Service responsible for learning and updating word patterns in the database
/// based on successful classifications
/// </summary>
public interface IWordLearningService
{
    /// <summary>
    /// Updates word counts in the database based on a successfully parsed result
    /// </summary>
    /// <param name="parseResult">The parsed result containing classification information</param>
    /// <returns>Number of words updated</returns>
    Task<int> LearnFromParseResultAsync(ParseResult parseResult);
    
    /// <summary>
    /// Updates or inserts a word with the specified type and increments its count
    /// </summary>
    /// <param name="word">The word to update</param>
    /// <param name="wordType">The type of word (first, last, both, business)</param>
    /// <returns>True if successful</returns>
    Task<bool> UpdateWordCountAsync(string word, string wordType);
    
    /// <summary>
    /// Checks if a word should be learned (not a location, not single char, etc.)
    /// </summary>
    /// <param name="word">The word to check</param>
    /// <param name="isFromAddress">Whether the word is from an address (location filtering applies)</param>
    /// <returns>True if the word should be learned</returns>
    Task<bool> ShouldLearnWordAsync(string word, bool isFromAddress = false);
}