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
    
    public async Task<int> LearnFromParseResultAsync(ParseResult parseResult, int minimumConfidence = 95)
    {
        // SPECIAL DEBUG: Log when learning is attempted
        _logger.LogWarning($"[WORD_LEARNING START] Attempting to learn from: '{parseResult?.Input}' (Success={parseResult?.Success}, MinConfidence={minimumConfidence})");
        
        if (parseResult == null || !parseResult.Success)
        {
            _logger.LogWarning($"[WORD_LEARNING SKIP] Parse result null or unsuccessful");
            return 0;
        }
        
        // Check confidence threshold
        var nameConfidence = parseResult.Confidence?.NameConfidence ?? 0;
        if (nameConfidence < minimumConfidence)
        {
            _logger.LogWarning($"[WORD_LEARNING SKIP] Confidence {nameConfidence}% is below threshold {minimumConfidence}% for: {parseResult.Input}");
            return 0;
        }
        
        // Additional safety check: Don't learn if classification is uncertain
        if (!parseResult.IsBusinessName && !parseResult.IsResidentialName)
        {
            _logger.LogWarning($"[WORD_LEARNING SKIP] Unclear classification (neither business nor residential) for: {parseResult.Input}");
            return 0;
        }
        
        // Don't learn from two-word entries where classification might be ambiguous
        // These often have one name word and one business word, making them unreliable for learning
        if (!string.IsNullOrWhiteSpace(parseResult.Name))
        {
            var words = parseResult.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 2 && parseResult.IsBusinessName && string.IsNullOrWhiteSpace(parseResult.FirstName) && string.IsNullOrWhiteSpace(parseResult.LastName))
            {
                _logger.LogWarning($"[WORD_LEARNING SKIP] Two-word business name might be residential: {parseResult.Input}");
                return 0;
            }
        }
            
        _logger.LogWarning($"[WORD_LEARNING PROCEED] Learning from parse result (confidence: {nameConfidence}%): Input='{parseResult.Input}', IsRes={parseResult.IsResidentialName}, IsBus={parseResult.IsBusinessName}, FirstName='{parseResult.FirstName}', LastName='{parseResult.LastName}'");
            
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
                            _logger.LogWarning($"[WORD_LEARNING] Processing FIRST name word: '{word}'");
                            if (await UpdateWordCountAsync(word, "first"))
                            {
                                updatedCount++;
                                _logger.LogWarning($"[WORD_LEARNING SUCCESS] Updated FIRST name word: '{word}'");
                            }
                        }
                        else
                        {
                            _logger.LogDebug($"[WORD_LEARNING] Skipped FIRST name word: '{word}'");
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
                            _logger.LogWarning($"[WORD_LEARNING] Processing LAST name word: '{word}'");
                            if (await UpdateWordCountAsync(word, "last"))
                            {
                                updatedCount++;
                                _logger.LogWarning($"[WORD_LEARNING SUCCESS] Updated LAST name word: '{word}'");
                            }
                        }
                        else
                        {
                            _logger.LogDebug($"[WORD_LEARNING] Skipped LAST name word: '{word}'");
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
                            _logger.LogWarning($"[WORD_LEARNING] Processing BOTH name word: '{word}'");
                            if (await UpdateWordCountAsync(word, "both"))
                            {
                                updatedCount++;
                                _logger.LogWarning($"[WORD_LEARNING SUCCESS] Updated BOTH name word: '{word}'");
                            }
                        }
                    }
                }
            }
            // Process business names
            else if (parseResult.IsBusinessName && !string.IsNullOrWhiteSpace(parseResult.Name))
            {
                var businessWords = SplitIntoWords(parseResult.Name);
                
                // Count learnable words (non-initials, non-skip words)
                var learnableWords = new List<string>();
                foreach (var word in businessWords)
                {
                    if (await ShouldLearnWordAsync(word))
                    {
                        learnableWords.Add(word);
                    }
                }
                
                // Only learn if we have at least 2 meaningful words
                // This prevents learning from entries like "A Dizon" which only has 1 meaningful word
                if (learnableWords.Count >= 2)
                {
                    foreach (var word in learnableWords)
                    {
                        _logger.LogWarning($"[WORD_LEARNING] Processing BUSINESS word: '{word}'");
                        if (await UpdateWordCountAsync(word, "business"))
                        {
                            updatedCount++;
                            _logger.LogWarning($"[WORD_LEARNING SUCCESS] Updated BUSINESS word: '{word}'");
                        }
                    }
                }
                else
                {
                    _logger.LogInformation($"Skipping business learning - only {learnableWords.Count} learnable words in: {parseResult.Name}");
                }
            }
            
            if (updatedCount > 0)
            {
                _logger.LogWarning($"[WORD_LEARNING COMPLETE] Successfully learned {updatedCount} words from: {parseResult.Input}");
            }
            else
            {
                _logger.LogWarning($"[WORD_LEARNING COMPLETE] No words learned from: {parseResult.Input}");
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
                
                // SPECIAL DEBUG: Log word_data table UPDATE
                _logger.LogWarning($"[WORD_DATA UPDATE] Incrementing '{wordLower}' type='{wordType}' count: {oldCount} -> {existingWord.WordCount} (WordId={existingWord.WordId})");
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
                
                // SPECIAL DEBUG: Log word_data table INSERT
                _logger.LogWarning($"[WORD_DATA INSERT] Adding new word '{wordLower}' type='{wordType}' with initial count=1");
            }
            
            var changes = await _context.SaveChangesAsync();
            
            // SPECIAL DEBUG: Confirm database commit
            _logger.LogWarning($"[WORD_DATA COMMIT] SaveChangesAsync committed {changes} changes for '{wordLower}' type='{wordType}'");
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
            
        // Skip pure numbers
        if (Regex.IsMatch(wordLower, @"^\d+$"))
        {
            _logger.LogDebug($"Skipping pure number: '{wordLower}'");
            return false;
        }
        
        // Skip ANY word containing numbers (prevents learning gibberish like "28-20-3w", "i2s", "0z7", "a316", etc.)
        if (Regex.IsMatch(wordLower, @"\d"))
        {
            _logger.LogWarning($"[WORD_LEARNING SKIP] Skipping word with numbers: '{wordLower}'");
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