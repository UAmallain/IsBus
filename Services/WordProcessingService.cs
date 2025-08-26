using System.Text.RegularExpressions;
using IsBus.Data;
using IsBus.Models;
using Microsoft.EntityFrameworkCore;

namespace IsBus.Services;

public class WordProcessingService : IWordProcessingService
{
    private readonly PhonebookContext _context;
    private readonly IBusinessIndicatorService _indicatorService;
    private readonly ILogger<WordProcessingService> _logger;

    public WordProcessingService(
        PhonebookContext context,
        IBusinessIndicatorService indicatorService,
        ILogger<WordProcessingService> logger)
    {
        _context = context;
        _indicatorService = indicatorService;
        _logger = logger;
    }

    public async Task<int> ProcessBusinessNameWordsAsync(string businessName)
    {
        if (string.IsNullOrWhiteSpace(businessName))
            return 0;

        var words = ExtractAndFilterWords(businessName);
        
        if (words.Count == 0)
            return 0;

        var wordsToProcess = new Dictionary<string, int>();
        
        foreach (var word in words)
        {
            var lowerWord = word.ToLowerInvariant();
            if (!wordsToProcess.ContainsKey(lowerWord))
            {
                wordsToProcess[lowerWord] = 1;
            }
            else
            {
                wordsToProcess[lowerWord]++;
            }
        }

        await ProcessWordsInDatabaseAsync(wordsToProcess);
        
        _logger.LogInformation("Processed {WordCount} unique words from business name: {BusinessName}", 
            wordsToProcess.Count, businessName);

        return wordsToProcess.Count;
    }

    private async Task ProcessWordsInDatabaseAsync(Dictionary<string, int> wordsToProcess)
    {
        // Use execution strategy to handle retries instead of manual transaction
        var strategy = _context.Database.CreateExecutionStrategy();
        
        await strategy.ExecuteAsync(async () =>
        {
            // Transaction is now inside the retry strategy
            using var transaction = await _context.Database.BeginTransactionAsync();
            
            try
            {
                var wordsList = wordsToProcess.Keys.ToList();
                
                var existingWords = await _context.WordData
                    .Where(w => wordsList.Contains(w.WordLower) && w.WordType == "business")
                    .ToListAsync();

                foreach (var existingWord in existingWords)
                {
                    if (wordsToProcess.TryGetValue(existingWord.WordLower, out var count))
                    {
                        existingWord.WordCount += count;
                        existingWord.LastSeen = DateTime.Now;
                        existingWord.UpdatedAt = DateTime.Now;
                        wordsToProcess.Remove(existingWord.WordLower);
                    }
                }

                var newWords = wordsToProcess.Select(kvp => new WordData
                {
                    WordLower = kvp.Key,
                    WordType = "business",
                    WordCount = kvp.Value,
                    LastSeen = DateTime.Now,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                }).ToList();

                if (newWords.Any())
                {
                    await _context.WordData.AddRangeAsync(newWords);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                
                _logger.LogInformation("Database update completed: {UpdatedCount} words updated, {NewCount} words added",
                    existingWords.Count, newWords.Count);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error processing words in database");
                throw;
            }
        });
    }

    private List<string> ExtractAndFilterWords(string input)
    {
        // First, identify and remove possessive words/names
        var possessivePattern = @"\b\w+'s?\b";
        var possessiveWords = Regex.Matches(input, possessivePattern, RegexOptions.IgnoreCase)
            .Select(m => m.Value.ToLowerInvariant())
            .ToHashSet();
        
        // Remove possessive forms from the input for word extraction
        var cleanedInput = Regex.Replace(input, possessivePattern, " ", RegexOptions.IgnoreCase);
        
        var words = Regex.Split(cleanedInput, @"[\s,\.\-&/\\:;'""()\[\]{}]+")
            .Where(w => !string.IsNullOrWhiteSpace(w))
            .Select(w => w.Trim())
            .Where(w => w.Length > 1)
            .Where(w => !_indicatorService.IsStopWord(w))
            .Where(w => !Regex.IsMatch(w, @"^\d+$"))
            .ToList();

        // Log what we're excluding
        if (possessiveWords.Any())
        {
            _logger.LogInformation("Excluding possessive words from storage: {PossessiveWords}", 
                string.Join(", ", possessiveWords));
        }

        return words;
    }
}