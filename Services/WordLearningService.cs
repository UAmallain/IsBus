using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using IsBus.Data;
using IsBus.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IsBus.Services;

public class WordLearningService : IWordLearningService
{
    private readonly PhonebookContext _context;
    private readonly ICommunityService _communityService;
    private readonly IStreetNameService _streetNameService;
    private readonly ILogger<WordLearningService> _logger;
    
    // Common words to skip
    private readonly HashSet<string> _skipWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "the", "of", "in", "on", "at", "to", "for", "by", "with", "from",
        "and", "or", "&", "et", // Connectors
        "inc", "ltd", "llc", "corp", "limited", "incorporated", "corporation", "company", "co" // Already in DB with high counts
    };
    
    public WordLearningService(
        PhonebookContext context,
        ICommunityService communityService,
        IStreetNameService streetNameService,
        ILogger<WordLearningService> logger)
    {
        _context = context;
        _communityService = communityService;
        _streetNameService = streetNameService;
        _logger = logger;
    }
    
    public async Task<int> LearnFromParseResultAsync(ParseResult parseResult)
    {
        if (parseResult == null || !parseResult.Success)
        {
            _logger.LogDebug($"Skipping learning - parse result null or unsuccessful");
            return 0;
        }
            
        _logger.LogInformation($"Learning from parse result: Input='{parseResult.Input}', IsRes={parseResult.IsResidentialName}, IsBus={parseResult.IsBusinessName}, FirstName='{parseResult.FirstName}', LastName='{parseResult.LastName}'");
            
        int updatedCount = 0;
        
        try
        {
            // Process residential names
            if (parseResult.IsResidentialName)
            {
                // Process first name
                if (!string.IsNullOrWhiteSpace(parseResult.FirstName))
                {
                    var firstNameWords = SplitIntoWords(parseResult.FirstName);
                    foreach (var word in firstNameWords)
                    {
                        if (await ShouldLearnWordAsync(word))
                        {
                            if (await UpdateWordCountAsync(word, "first"))
                            {
                                updatedCount++;
                                _logger.LogInformation($"Updated first name word: '{word}'");
                            }
                        }
                        else
                        {
                            _logger.LogDebug($"Skipped first name word: '{word}'");
                        }
                    }
                }
                
                // Process last name
                if (!string.IsNullOrWhiteSpace(parseResult.LastName))
                {
                    var lastNameWords = SplitIntoWords(parseResult.LastName);
                    foreach (var word in lastNameWords)
                    {
                        if (await ShouldLearnWordAsync(word))
                        {
                            if (await UpdateWordCountAsync(word, "last"))
                            {
                                updatedCount++;
                                _logger.LogInformation($"Updated last name word: '{word}'");
                            }
                        }
                        else
                        {
                            _logger.LogDebug($"Skipped last name word: '{word}'");
                        }
                    }
                }
                
                // If we have a name but couldn't split it, process as "both"
                if (string.IsNullOrWhiteSpace(parseResult.FirstName) && 
                    string.IsNullOrWhiteSpace(parseResult.LastName) &&
                    !string.IsNullOrWhiteSpace(parseResult.Name))
                {
                    var nameWords = SplitIntoWords(parseResult.Name);
                    foreach (var word in nameWords)
                    {
                        if (await ShouldLearnWordAsync(word))
                        {
                            if (await UpdateWordCountAsync(word, "both"))
                            {
                                updatedCount++;
                                _logger.LogDebug($"Updated name word (both): {word}");
                            }
                        }
                    }
                }
            }
            // Process business names
            else if (parseResult.IsBusinessName && !string.IsNullOrWhiteSpace(parseResult.Name))
            {
                var businessWords = SplitIntoWords(parseResult.Name);
                foreach (var word in businessWords)
                {
                    if (await ShouldLearnWordAsync(word))
                    {
                        if (await UpdateWordCountAsync(word, "business"))
                        {
                            updatedCount++;
                            _logger.LogDebug($"Updated business word: {word}");
                        }
                    }
                }
            }
            
            if (updatedCount > 0)
            {
                _logger.LogInformation($"Learned {updatedCount} words from parse result: {parseResult.Input}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error learning from parse result: {parseResult.Input}");
        }
        
        return updatedCount;
    }
    
    public async Task<bool> UpdateWordCountAsync(string word, string wordType)
    {
        if (string.IsNullOrWhiteSpace(word) || string.IsNullOrWhiteSpace(wordType))
            return false;
            
        var wordLower = word.ToLower().Trim();
        
        try
        {
            // Check if the word already exists with this type
            var existingWord = await _context.WordData
                .FirstOrDefaultAsync(w => w.WordLower == wordLower && w.WordType == wordType);
            
            if (existingWord != null)
            {
                // Increment the count
                var oldCount = existingWord.WordCount;
                existingWord.WordCount = existingWord.WordCount + 1;
                existingWord.LastSeen = DateTime.UtcNow;
                _logger.LogInformation($"Incremented count for '{wordLower}' ({wordType}): {oldCount} -> {existingWord.WordCount}");
            }
            else
            {
                // Add new word
                var newWord = new WordData
                {
                    WordLower = wordLower,
                    WordType = wordType,
                    WordCount = 1,
                    LastSeen = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow
                };
                
                _context.WordData.Add(newWord);
                _logger.LogInformation($"Added new word '{wordLower}' ({wordType}) with initial count 1");
            }
            
            var changes = await _context.SaveChangesAsync();
            _logger.LogInformation($"SaveChangesAsync returned {changes} changes for word '{wordLower}' ({wordType})");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error updating word count for '{wordLower}' ({wordType})");
            return false;
        }
    }
    
    public async Task<bool> ShouldLearnWordAsync(string word, bool isFromAddress = false)
    {
        if (string.IsNullOrWhiteSpace(word))
        {
            _logger.LogDebug($"Skipping empty/null word");
            return false;
        }
            
        var wordLower = word.ToLower().Trim();
        
        // Skip single character words
        if (wordLower.Length <= 1)
        {
            _logger.LogDebug($"Skipping single char word: '{wordLower}'");
            return false;
        }
            
        // Skip numbers
        if (Regex.IsMatch(wordLower, @"^\d+$"))
        {
            _logger.LogDebug($"Skipping number: '{wordLower}'");
            return false;
        }
            
        // Skip common skip words
        if (_skipWords.Contains(wordLower))
        {
            _logger.LogDebug($"Skipping common word: '{wordLower}'");
            return false;
        }
            
        // Only skip location words if they're from an address
        // Names can coincidentally match community/street names
        if (isFromAddress)
        {
            // Skip if it's a known community
            var community = await _communityService.FindCommunityAsync(wordLower, null);
            if (community != null)
            {
                _logger.LogDebug($"Skipping '{wordLower}' - it's a known community: {community.CommunityName}");
                return false;
            }
            
            // Skip if it's a known street name
            if (await _streetNameService.IsKnownStreetNameAsync(wordLower))
            {
                _logger.LogDebug($"Skipping '{wordLower}' - it's a known street name");
                return false;
            }
        }
        
        // Skip words that are just punctuation or special characters
        if (!Regex.IsMatch(wordLower, @"[a-z]"))
            return false;
            
        return true;
    }
    
    private List<string> SplitIntoWords(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new List<string>();
            
        // Split on spaces and common punctuation, but preserve apostrophes in contractions
        var words = Regex.Split(text, @"[\s,;.!?()[\]{}""]+")
            .Where(w => !string.IsNullOrWhiteSpace(w))
            .Select(w => w.Trim('\'', '-', '_'))
            .Where(w => !string.IsNullOrWhiteSpace(w))
            .ToList();
            
        return words;
    }
}