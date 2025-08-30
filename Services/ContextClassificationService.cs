using IsBus.Data;
using IsBus.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace IsBus.Services;

public class ContextClassificationService : IClassificationService
{
    private readonly PhonebookContext _context;
    private readonly ILogger<ContextClassificationService> _logger;
    private readonly IBusinessWordService _businessWordService;
    
    public ContextClassificationService(
        PhonebookContext context,
        ILogger<ContextClassificationService> logger,
        IBusinessWordService businessWordService)
    {
        _context = context;
        _logger = logger;
        _businessWordService = businessWordService;
    }
    
    public async Task<ClassificationResult> ClassifyAsync(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return new ClassificationResult
            {
                Input = input,
                Classification = "unknown",
                Confidence = 0,
                Reason = "Empty input"
            };
        }
        
        var normalizedInput = input.Trim().ToLowerInvariant();
        var words = normalizedInput.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        // Check for corporate suffixes using BusinessWordService
        foreach (var word in words)
        {
            if (await _businessWordService.IsCorporateSuffixAsync(word.Trim('.')))
            {
                return new ClassificationResult
                {
                    Input = input,
                    Classification = "business",
                    Confidence = 100,
                    IsBusiness = true,
                    IsResidential = false,
                    Reason = $"Contains corporate identifier: {word.ToUpper()}",
                    BusinessScore = 100,
                    ResidentialScore = 0
                };
            }
        }
        
        // Build context map
        var contextMap = await BuildContextMap(words);
        
        // Analyze the context pattern
        var classification = AnalyzeContextPattern(contextMap);
        
        classification.Input = input;
        classification.Words = words.ToList();
        
        _logger.LogInformation($"Classification: {input} -> {classification.Classification} ({classification.Confidence}%)");
        _logger.LogDebug($"Context Map: {string.Join(", ", contextMap.Select(c => c.PrimaryType))}");
        
        return classification;
    }
    
    private async Task<List<WordContext>> BuildContextMap(string[] words)
    {
        var contextMap = new List<WordContext>();
        
        foreach (var word in words)
        {
            var wordLower = word.ToLower().Trim().Trim('.');
            
            var context = new WordContext
            {
                Word = wordLower
            };
            
            // FIRST: Check for special cases (initials and connectors)
            
            // Check if it's a connector (& or "and")
            if (IsConnector(wordLower))
            {
                context.PrimaryType = WordTypeEnum.Connector;
                contextMap.Add(context);
                _logger.LogDebug($"{wordLower} identified as connector");
                continue;
            }
            
            // Check if it's a single letter (initial)
            if (IsInitial(wordLower))
            {
                context.PrimaryType = WordTypeEnum.Initial;
                contextMap.Add(context);
                _logger.LogDebug($"{wordLower} identified as initial");
                continue;
            }
            
            // Get all word_data entries for this word
            var wordEntries = await _context.Set<WordData>()
                .Where(w => w.WordLower == wordLower)
                .ToListAsync();
            
            if (wordEntries.Any())
            {
                context.FirstCount = wordEntries.FirstOrDefault(w => w.WordType == "first")?.WordCount ?? 0;
                context.LastCount = wordEntries.FirstOrDefault(w => w.WordType == "last")?.WordCount ?? 0;
                context.BothCount = wordEntries.FirstOrDefault(w => w.WordType == "both")?.WordCount ?? 0;
                context.BusinessCount = wordEntries.FirstOrDefault(w => w.WordType == "business")?.WordCount ?? 0;
                context.IndeterminateCount = wordEntries.FirstOrDefault(w => w.WordType == "indeterminate")?.WordCount ?? 0;
                
                // Determine primary type based on highest count
                context.PrimaryType = DeterminePrimaryType(context);
                context.MaxCount = Math.Max(
                    Math.Max(context.FirstCount, context.LastCount),
                    Math.Max(context.BothCount, context.BusinessCount)
                );
            }
            else
            {
                // Word not in database - check if it's a common determiner
                if (IsCommonDeterminer(wordLower))
                {
                    context.PrimaryType = WordTypeEnum.Indeterminate;
                }
                else
                {
                    context.PrimaryType = WordTypeEnum.Unknown;
                }
            }
            
            contextMap.Add(context);
            
            _logger.LogDebug(context.GetContextString());
        }
        
        return contextMap;
    }
    
    private bool IsInitial(string word)
    {
        // Single letter (with or without period) is an initial
        // Examples: "j", "j.", "m", "m."
        if (word.Length == 1 && char.IsLetter(word[0]))
            return true;
        
        if (word.Length == 2 && word[1] == '.' && char.IsLetter(word[0]))
            return true;
        
        return false;
    }
    
    private bool IsConnector(string word)
    {
        // Common connectors in names
        return word == "&" || word == "and" || word == "or";
    }
    
    private WordTypeEnum DeterminePrimaryType(WordContext context)
    {
        // If business count is significantly higher (2x) than any name count, it's business
        var maxNameCount = Math.Max(Math.Max(context.FirstCount, context.LastCount), context.BothCount);
        
        if (context.BusinessCount > maxNameCount * 2 && context.BusinessCount >= 10)
        {
            return WordTypeEnum.Business;
        }
        
        // Find the highest count
        var counts = new Dictionary<WordTypeEnum, int>
        {
            { WordTypeEnum.First, context.FirstCount },
            { WordTypeEnum.Last, context.LastCount },
            { WordTypeEnum.Both, context.BothCount },
            { WordTypeEnum.Business, context.BusinessCount }
        };
        
        var maxEntry = counts.OrderByDescending(kvp => kvp.Value).First();
        
        // If the max count is very low (< 5), consider it indeterminate
        if (maxEntry.Value < 5)
        {
            return WordTypeEnum.Indeterminate;
        }
        
        return maxEntry.Key;
    }
    
    private ClassificationResult AnalyzeContextPattern(List<WordContext> contextMap)
    {
        var result = new ClassificationResult();
        
        // Count word types in the context
        var typeCount = new Dictionary<WordTypeEnum, int>();
        foreach (WordTypeEnum type in Enum.GetValues(typeof(WordTypeEnum)))
        {
            typeCount[type] = 0;
        }
        
        foreach (var context in contextMap)
        {
            typeCount[context.PrimaryType]++;
        }
        
        // Analyze patterns
        int businessWords = typeCount[WordTypeEnum.Business];
        int nameWords = typeCount[WordTypeEnum.First] + typeCount[WordTypeEnum.Last] + typeCount[WordTypeEnum.Both];
        int initialWords = typeCount[WordTypeEnum.Initial];
        int connectorWords = typeCount[WordTypeEnum.Connector];
        int indeterminateWords = typeCount[WordTypeEnum.Indeterminate];
        int unknownWords = typeCount[WordTypeEnum.Unknown];
        
        // Pattern analysis
        var patterns = AnalyzePatterns(contextMap);
        
        // Calculate scores
        double businessScore = 0;
        double residentialScore = 0;
        
        // Business indicators
        businessScore += businessWords * 30;
        businessScore += patterns.HasBusinessPattern ? 40 : 0;
        businessScore += patterns.HasPossessiveWithBusiness ? 50 : 0;
        
        // Initials followed by business words is a strong business indicator
        if (initialWords > 0 && businessWords > 0 && nameWords == 0)
        {
            businessScore += 60; // Strong business pattern
        }
        
        // Residential indicators
        residentialScore += patterns.HasValidNamePattern ? 60 : -20;
        residentialScore += patterns.HasFirstLastPattern ? 40 : 0;
        residentialScore += patterns.HasInitialPattern ? 50 : 0;  // Strong indicator of residential
        residentialScore += nameWords * 15;
        
        // Single word penalty for residential
        if (contextMap.Count == 1)
        {
            businessScore += 50;
            residentialScore -= 30;
        }
        
        // Normalize scores
        var total = businessScore + residentialScore;
        if (total > 0)
        {
            businessScore = (businessScore / total) * 100;
            residentialScore = (residentialScore / total) * 100;
        }
        
        // Determine classification
        if (businessScore > residentialScore)
        {
            result.Classification = "business";
            result.Confidence = Math.Min(100, (int)businessScore);
            result.IsBusiness = true;
            result.Reason = DetermineBusinessReason(contextMap, patterns, businessWords);
        }
        else
        {
            result.Classification = "residential";
            result.Confidence = Math.Min(100, (int)residentialScore);
            result.IsResidential = true;
            result.Reason = DetermineResidentialReason(contextMap, patterns, nameWords);
        }
        
        result.BusinessScore = (int)businessScore;
        result.ResidentialScore = (int)residentialScore;
        
        // Add detailed context for debugging
        result.DetailedScores = new Dictionary<string, double>
        {
            ["business_words"] = businessWords,
            ["name_words"] = nameWords,
            ["initial_words"] = initialWords,
            ["connector_words"] = connectorWords,
            ["valid_name_pattern"] = patterns.HasValidNamePattern ? 1 : 0,
            ["initial_pattern"] = patterns.HasInitialPattern ? 1 : 0,
            ["business_pattern"] = patterns.HasBusinessPattern ? 1 : 0,
            ["possessive_business"] = patterns.HasPossessiveWithBusiness ? 1 : 0,
            ["context_pattern"] = contextMap.Count
        };
        
        return result;
    }
    
    private PatternAnalysis AnalyzePatterns(List<WordContext> contextMap)
    {
        var analysis = new PatternAnalysis();
        
        if (contextMap.Count == 0)
            return analysis;
        
        // Check for initial patterns first
        analysis = CheckForInitialPatterns(contextMap, analysis);
        
        // If we found an initial pattern, it's likely residential
        if (analysis.HasInitialPattern)
        {
            analysis.HasValidNamePattern = true;
            return analysis; // Early return for clear residential patterns
        }
        
        // Check for standard name patterns
        if (contextMap.Count >= 2)
        {
            var first = contextMap[0];
            var last = contextMap[contextMap.Count - 1];
            
            // FirstName LastName pattern
            if ((first.PrimaryType == WordTypeEnum.First || first.PrimaryType == WordTypeEnum.Both) &&
                (last.PrimaryType == WordTypeEnum.Last || last.PrimaryType == WordTypeEnum.Both))
            {
                analysis.HasFirstLastPattern = true;
                analysis.HasValidNamePattern = true;
            }
            // LastName FirstName pattern
            else if ((first.PrimaryType == WordTypeEnum.Last || first.PrimaryType == WordTypeEnum.Both) &&
                     (last.PrimaryType == WordTypeEnum.First || last.PrimaryType == WordTypeEnum.Both))
            {
                analysis.HasFirstLastPattern = true;
                analysis.HasValidNamePattern = true;
            }
            // Both type names together
            else if (first.PrimaryType == WordTypeEnum.Both && last.PrimaryType == WordTypeEnum.Both)
            {
                // Check if they have substantial name counts
                if (first.BothCount >= 10 && last.BothCount >= 10)
                {
                    analysis.HasValidNamePattern = true;
                }
            }
        }
        
        // Check for business patterns
        int consecutiveBusinessWords = 0;
        int maxConsecutiveBusiness = 0;
        bool hasInitialsBeforeBusiness = false;
        
        for (int i = 0; i < contextMap.Count; i++)
        {
            if (contextMap[i].PrimaryType == WordTypeEnum.Business)
            {
                consecutiveBusinessWords++;
                maxConsecutiveBusiness = Math.Max(maxConsecutiveBusiness, consecutiveBusinessWords);
                
                // Check if business word follows initials pattern
                if (i > 0)
                {
                    // Check if previous words were initials/connectors
                    bool precedingAreInitialsOrConnectors = true;
                    for (int j = 0; j < i; j++)
                    {
                        if (contextMap[j].PrimaryType != WordTypeEnum.Initial && 
                            contextMap[j].PrimaryType != WordTypeEnum.Connector)
                        {
                            precedingAreInitialsOrConnectors = false;
                            break;
                        }
                    }
                    if (precedingAreInitialsOrConnectors && i > 0)
                    {
                        hasInitialsBeforeBusiness = true;
                    }
                }
            }
            else
            {
                consecutiveBusinessWords = 0;
            }
            
            // Check for possessive patterns
            if (i < contextMap.Count - 1 && contextMap[i].Word.EndsWith("'s"))
            {
                if (contextMap[i + 1].PrimaryType == WordTypeEnum.Business)
                {
                    analysis.HasPossessiveWithBusiness = true;
                }
            }
        }
        
        // Mark initials before business as a business pattern
        if (hasInitialsBeforeBusiness)
        {
            analysis.HasBusinessPattern = true;
        }
        
        // Multiple business words in sequence indicate business
        if (maxConsecutiveBusiness >= 2)
        {
            analysis.HasBusinessPattern = true;
        }
        
        // Majority business words indicate business
        var businessCount = contextMap.Count(c => c.PrimaryType == WordTypeEnum.Business);
        if (businessCount > contextMap.Count / 2)
        {
            analysis.HasBusinessPattern = true;
        }
        
        return analysis;
    }
    
    private bool IsCommonDeterminer(string word)
    {
        var determiners = new HashSet<string> { "a", "an", "the", "of", "in", "on", "at", "to", "for", "by", "with", "from" };
        return determiners.Contains(word);
    }
    
    private PatternAnalysis CheckForInitialPatterns(List<WordContext> contextMap, PatternAnalysis analysis)
    {
        // Pattern: Name Initial [Connector Initial]*
        // Examples: "Smith J", "Smith J & M", "Smith J M", "J Smith", "J & M Smith"
        
        // Count initials and connectors
        int initialCount = contextMap.Count(c => c.PrimaryType == WordTypeEnum.Initial);
        int connectorCount = contextMap.Count(c => c.PrimaryType == WordTypeEnum.Connector);
        int nameCount = contextMap.Count(c => 
            c.PrimaryType == WordTypeEnum.First || 
            c.PrimaryType == WordTypeEnum.Last || 
            c.PrimaryType == WordTypeEnum.Both);
        
        // If we have initials with at least one name, it's a name pattern
        if (initialCount > 0 && nameCount > 0)
        {
            analysis.HasInitialPattern = true;
            
            // Check specific patterns
            // Pattern 1: Name Initial(s) - "Smith J" or "Smith J M"
            if (contextMap[0].PrimaryType == WordTypeEnum.Last || contextMap[0].PrimaryType == WordTypeEnum.Both)
            {
                bool allRemainingAreInitialsOrConnectors = true;
                for (int i = 1; i < contextMap.Count; i++)
                {
                    if (contextMap[i].PrimaryType != WordTypeEnum.Initial && 
                        contextMap[i].PrimaryType != WordTypeEnum.Connector)
                    {
                        allRemainingAreInitialsOrConnectors = false;
                        break;
                    }
                }
                if (allRemainingAreInitialsOrConnectors)
                {
                    analysis.InitialPatternType = "LastName + Initials";
                    return analysis;
                }
            }
            
            // Pattern 2: Initial(s) Name - "J Smith" or "J M Smith"
            if (contextMap[contextMap.Count - 1].PrimaryType == WordTypeEnum.Last || 
                contextMap[contextMap.Count - 1].PrimaryType == WordTypeEnum.Both)
            {
                bool allPrecedingAreInitialsOrConnectors = true;
                for (int i = 0; i < contextMap.Count - 1; i++)
                {
                    if (contextMap[i].PrimaryType != WordTypeEnum.Initial && 
                        contextMap[i].PrimaryType != WordTypeEnum.Connector)
                    {
                        allPrecedingAreInitialsOrConnectors = false;
                        break;
                    }
                }
                if (allPrecedingAreInitialsOrConnectors)
                {
                    analysis.InitialPatternType = "Initials + LastName";
                    return analysis;
                }
            }
            
            // Pattern 3: Name Initial & Initial - "Smith J & M"
            if (connectorCount > 0 && initialCount >= 2)
            {
                analysis.InitialPatternType = "Name + Multiple Initials";
            }
        }
        
        // Pattern: Just initials with connectors (J & M) - NOT residential without a name
        // This is typically a business pattern (e.g., "J & M Contracting")
        if (initialCount >= 2 && connectorCount > 0 && nameCount == 0)
        {
            // Don't mark this as a valid initial pattern for residential
            // analysis.HasInitialPattern = false; // Already false by default
            analysis.InitialPatternType = "Multiple Initials Only (No Name)";
        }
        
        return analysis;
    }
    
    private string DetermineBusinessReason(List<WordContext> contextMap, PatternAnalysis patterns, int businessWords)
    {
        var reasons = new List<string>();
        
        if (patterns.HasPossessiveWithBusiness)
            reasons.Add("possessive followed by business word");
        
        if (patterns.HasBusinessPattern)
            reasons.Add("business word pattern detected");
        
        if (businessWords > 0)
            reasons.Add($"{businessWords} business words");
        
        // Show context pattern
        var pattern = string.Join("-", contextMap.Select(c => c.PrimaryType.ToString().ToLower()));
        reasons.Add($"pattern: {pattern}");
        
        return string.Join("; ", reasons);
    }
    
    private string DetermineResidentialReason(List<WordContext> contextMap, PatternAnalysis patterns, int nameWords)
    {
        var reasons = new List<string>();
        
        if (patterns.HasValidNamePattern)
            reasons.Add("valid name pattern");
        
        if (patterns.HasFirstLastPattern)
            reasons.Add("first+last name structure");
        
        if (patterns.HasInitialPattern)
            reasons.Add($"name with initials ({patterns.InitialPatternType})");
        
        if (nameWords > 0)
            reasons.Add($"{nameWords} name words");
        
        // Show context pattern
        var pattern = string.Join("-", contextMap.Select(c => c.PrimaryType.ToString().ToLower()));
        reasons.Add($"pattern: {pattern}");
        
        return string.Join("; ", reasons);
    }
}

public class PatternAnalysis
{
    public bool HasValidNamePattern { get; set; }
    public bool HasFirstLastPattern { get; set; }
    public bool HasBusinessPattern { get; set; }
    public bool HasPossessiveWithBusiness { get; set; }
    public bool HasInitialPattern { get; set; }
    public string InitialPatternType { get; set; } = string.Empty;
}