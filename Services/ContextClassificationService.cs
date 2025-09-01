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
        
        // Special debug logging for problem records
        bool enableDebug = IsDebugRecord(input);
        if (enableDebug)
        {
            _logger.LogWarning($"\n{new string('=', 80)}");
            _logger.LogWarning($"DEBUG CLASSIFICATION START: {input}");
            _logger.LogWarning($"{new string('=', 80)}");
        }
        
        var normalizedInput = input.Trim().ToLowerInvariant();
        var words = normalizedInput.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        if (enableDebug)
        {
            _logger.LogWarning($"DEBUG: Input split into {words.Length} words: [{string.Join(", ", words.Select(w => $"'{w}'"))}]");
        }
        
        // Check for corporate suffixes using BusinessWordService
        foreach (var word in words)
        {
            var cleanWord = word.Trim('.');
            if (enableDebug)
            {
                _logger.LogWarning($"DEBUG: Checking if '{cleanWord}' is a corporate suffix...");
            }
            
            if (await _businessWordService.IsCorporateSuffixAsync(cleanWord))
            {
                if (enableDebug)
                {
                    _logger.LogWarning($"DEBUG: YES - '{cleanWord}' IS a corporate suffix! Classifying as BUSINESS with 100% confidence");
                }
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
            
            if (enableDebug)
            {
                _logger.LogWarning($"DEBUG: '{cleanWord}' is NOT a corporate suffix");
            }
        }
        
        // Build context map
        if (enableDebug)
        {
            _logger.LogWarning($"\nDEBUG: Building context map for each word...");
        }
        
        var contextMap = await BuildContextMap(words, enableDebug);
        
        if (enableDebug)
        {
            _logger.LogWarning($"\nDEBUG WORD CONTEXT MAP SUMMARY:");
            _logger.LogWarning($"Total words analyzed: {contextMap.Count}");
            foreach (var ctx in contextMap)
            {
                _logger.LogWarning($"  Word: '{ctx.Word}' -> Primary Type: {ctx.PrimaryType}");
                _logger.LogWarning($"    Database counts: First={ctx.FirstCount}, Last={ctx.LastCount}, Both={ctx.BothCount}, Business={ctx.BusinessCount}, Indeterminate={ctx.IndeterminateCount}");
                if (ctx.MaxCount > 0)
                {
                    _logger.LogWarning($"    Max count: {ctx.MaxCount} (from {ctx.PrimaryType} category)");
                }
            }
        }
        
        // Analyze the context pattern
        if (enableDebug)
        {
            _logger.LogWarning($"\nDEBUG: Analyzing context pattern...");
        }
        
        var classification = AnalyzeContextPattern(contextMap, enableDebug);
        
        if (enableDebug)
        {
            _logger.LogWarning($"\nDEBUG FINAL CLASSIFICATION RESULT:");
            _logger.LogWarning($"  Input: '{input}'");
            _logger.LogWarning($"  Classification: {classification.Classification.ToUpper()}");
            _logger.LogWarning($"  Confidence: {classification.Confidence}%");
            _logger.LogWarning($"  Business Score: {classification.BusinessScore}");
            _logger.LogWarning($"  Residential Score: {classification.ResidentialScore}");
            _logger.LogWarning($"  IsBusiness: {classification.IsBusiness}");
            _logger.LogWarning($"  IsResidential: {classification.IsResidential}");
            _logger.LogWarning($"  Reason: {classification.Reason}");
            
            if (classification.DetailedScores != null && classification.DetailedScores.Any())
            {
                _logger.LogWarning($"\n  Detailed Score Breakdown:");
                foreach (var score in classification.DetailedScores)
                {
                    _logger.LogWarning($"    {score.Key}: {score.Value}");
                }
            }
            _logger.LogWarning($"{new string('=', 80)}\n");
        }
        
        classification.Input = input;
        classification.Words = words.ToList();
        
        _logger.LogInformation($"Classification: {input} -> {classification.Classification} ({classification.Confidence}%)");
        _logger.LogDebug($"Context Map: {string.Join(", ", contextMap.Select(c => c.PrimaryType))}");
        
        return classification;
    }
    
    private bool IsDebugRecord(string input)
    {
        if (string.IsNullOrEmpty(input)) return false;
        
        // Debug records from the user's test
        var debugRecords = new[] { 
            "Aguayo Jesus",
            "Albert Luc",
            "Allain Arthur",
            "Allain Bernard",
            "Allain C",
            "Allain D",
            "Allain E",
            "Allain Eric",
            "Allain Gerald",
            "Allain Gisele",
            "Allain Herve",
            "Allain Jacques"
        };
        
        // Check if input starts with any debug record (case insensitive)
        return debugRecords.Any(record => input.StartsWith(record, StringComparison.OrdinalIgnoreCase));
    }
    
    private async Task<List<WordContext>> BuildContextMap(string[] words, bool enableDebug = false)
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
                if (enableDebug)
                {
                    _logger.LogWarning($"  DEBUG: '{wordLower}' identified as CONNECTOR (special case)");
                }
                continue;
            }
            
            // Check if it's a single letter (initial)
            if (IsInitial(wordLower))
            {
                context.PrimaryType = WordTypeEnum.Initial;
                contextMap.Add(context);
                if (enableDebug)
                {
                    _logger.LogWarning($"  DEBUG: '{wordLower}' identified as INITIAL (single letter)");
                }
                continue;
            }
            
            // Get all word_data entries for this word
            if (enableDebug)
            {
                _logger.LogWarning($"  DEBUG: Looking up '{wordLower}' in database...");
            }
            
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
                
                if (enableDebug)
                {
                    _logger.LogWarning($"    Found in database with counts:");
                    _logger.LogWarning($"      First: {context.FirstCount}");
                    _logger.LogWarning($"      Last: {context.LastCount}");
                    _logger.LogWarning($"      Both: {context.BothCount}");
                    _logger.LogWarning($"      Business: {context.BusinessCount}");
                    _logger.LogWarning($"      Indeterminate: {context.IndeterminateCount}");
                }
                
                // Determine primary type based on highest count
                context.PrimaryType = DeterminePrimaryType(context, enableDebug);
                context.MaxCount = Math.Max(
                    Math.Max(context.FirstCount, context.LastCount),
                    Math.Max(context.BothCount, context.BusinessCount)
                );
                
                if (enableDebug)
                {
                    _logger.LogWarning($"    Primary type determined: {context.PrimaryType} (max count: {context.MaxCount})");
                }
            }
            else
            {
                if (enableDebug)
                {
                    _logger.LogWarning($"    NOT found in database");
                }
                
                // Word not in database - check if it's a common determiner
                if (IsCommonDeterminer(wordLower))
                {
                    context.PrimaryType = WordTypeEnum.Indeterminate;
                    if (enableDebug)
                    {
                        _logger.LogWarning($"    Identified as common determiner -> INDETERMINATE");
                    }
                }
                else
                {
                    context.PrimaryType = WordTypeEnum.Unknown;
                    if (enableDebug)
                    {
                        _logger.LogWarning($"    Not a common determiner -> UNKNOWN");
                    }
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
    
    private WordTypeEnum DeterminePrimaryType(WordContext context, bool enableDebug = false)
    {
        // The word type with the HIGHEST count determines the primary type
        // This is the key to using our learned data effectively
        
        var counts = new Dictionary<WordTypeEnum, int>
        {
            { WordTypeEnum.First, context.FirstCount },
            { WordTypeEnum.Last, context.LastCount },
            { WordTypeEnum.Both, context.BothCount },
            { WordTypeEnum.Business, context.BusinessCount }
        };
        
        // Find the type with the highest count
        var maxEntry = counts.OrderByDescending(kvp => kvp.Value).First();
        
        if (enableDebug)
        {
            _logger.LogWarning($"      Highest count: {maxEntry.Key} with {maxEntry.Value}");
        }
        
        // If all counts are very low (< 3), consider it indeterminate/unknown
        if (maxEntry.Value < 3)
        {
            if (enableDebug)
            {
                _logger.LogWarning($"      All counts < 3, returning INDETERMINATE");
            }
            return WordTypeEnum.Indeterminate;
        }
        
        // Special case: If name counts (first/last/both) are significantly higher than business
        // then it's definitely a name, not business
        var maxNameCount = Math.Max(Math.Max(context.FirstCount, context.LastCount), context.BothCount);
        if (maxNameCount > context.BusinessCount * 2 && maxEntry.Key == WordTypeEnum.Business)
        {
            if (enableDebug)
            {
                _logger.LogWarning($"      Name count ({maxNameCount}) is > 2x business count ({context.BusinessCount})");
                _logger.LogWarning($"      Overriding business classification");
            }
            
            // Return the highest name type instead
            if (context.BothCount >= context.FirstCount && context.BothCount >= context.LastCount)
            {
                if (enableDebug) _logger.LogWarning($"      Returning BOTH instead");
                return WordTypeEnum.Both;
            }
            else if (context.LastCount >= context.FirstCount)
            {
                if (enableDebug) _logger.LogWarning($"      Returning LAST instead");
                return WordTypeEnum.Last;
            }
            else
            {
                if (enableDebug) _logger.LogWarning($"      Returning FIRST instead");
                return WordTypeEnum.First;
            }
        }
        
        return maxEntry.Key;
    }
    
    private ClassificationResult AnalyzeContextPattern(List<WordContext> contextMap, bool enableDebug = false)
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
        
        if (enableDebug)
        {
            _logger.LogWarning($"\n  Word type counts:");
            _logger.LogWarning($"    Business words: {businessWords}");
            _logger.LogWarning($"    Name words: {nameWords} (First: {typeCount[WordTypeEnum.First]}, Last: {typeCount[WordTypeEnum.Last]}, Both: {typeCount[WordTypeEnum.Both]})");
            _logger.LogWarning($"    Initial words: {initialWords}");
            _logger.LogWarning($"    Connector words: {connectorWords}");
            _logger.LogWarning($"    Indeterminate words: {indeterminateWords}");
            _logger.LogWarning($"    Unknown words: {unknownWords}");
        }
        
        // Pattern analysis
        if (enableDebug)
        {
            _logger.LogWarning($"\n  Analyzing patterns...");
        }
        var patterns = AnalyzePatterns(contextMap, enableDebug);
        
        // Calculate scores
        double businessScore = 0;
        double residentialScore = 0;
        
        if (enableDebug)
        {
            _logger.LogWarning($"\n  Calculating scores...");
        }
        
        // Business indicators - reduced weight for single business words
        // Don't let a single business word dominate when we have name words
        if (businessWords == 1 && nameWords >= 1)
        {
            // Single business word with name words - reduce weight significantly
            businessScore += 10;
            if (enableDebug) _logger.LogWarning($"    Business: +10 for single business word (reduced due to name words present)");
        }
        else
        {
            businessScore += businessWords * 30;
            if (enableDebug && businessWords > 0)
            {
                _logger.LogWarning($"    Business: +{businessWords * 30} for {businessWords} business words");
            }
        }
        
        if (patterns.HasBusinessPattern)
        {
            businessScore += 40;
            if (enableDebug) _logger.LogWarning($"    Business: +40 for business pattern");
        }
        
        if (patterns.HasPossessiveWithBusiness)
        {
            businessScore += 50;
            if (enableDebug) _logger.LogWarning($"    Business: +50 for possessive with business");
        }
        
        // Initials followed by business words is a strong business indicator
        // BUT: Don't apply this if we have unknown words that could be names
        if (initialWords > 0 && businessWords > 0 && nameWords == 0 && unknownWords == 0)
        {
            businessScore += 60; // Strong business pattern
        }
        
        // Special case: Initial + Unknown word pattern (like "A Dizon")
        // This is likely a residential name where the surname isn't in our database
        if (initialWords > 0 && unknownWords > 0 && businessWords == 0)
        {
            residentialScore += 40; // Likely residential pattern
        }
        
        // Special boost for names with very high "both" counts - strong residential indicator
        // Example: "A Kevin" where Kevin has both count > 7000
        bool hasHighBothCount = false;
        foreach (var context in contextMap)
        {
            if (context.BothCount > 500) // High threshold for "both" names
            {
                hasHighBothCount = true;
                residentialScore += Math.Min(50, context.BothCount / 100); // Scale bonus based on count
                break;
            }
        }
        
        // Residential indicators
        if (patterns.HasValidNamePattern)
        {
            residentialScore += 60;
            if (enableDebug) _logger.LogWarning($"    Residential: +60 for valid name pattern");
        }
        else if (contextMap.Count == 2)
        {
            // Special handling for two-word entries
            // Many residential names are "LastName FirstName" or "FirstName LastName"
            if (nameWords == 2)
            {
                // Both words are names - STRONG residential indicator
                residentialScore += 80;
                if (enableDebug) _logger.LogWarning($"    Residential: +80 for two name words");
            }
            else if (nameWords == 1 && businessWords == 1)
            {
                // One name word and one business word - could be either
                residentialScore += 20;
                if (enableDebug) _logger.LogWarning($"    Residential: +20 for two-word pattern with one name");
            }
            else if (nameWords == 1 && unknownWords == 1)
            {
                // One name word and one unknown - likely residential with uncommon name
                residentialScore += 40;
                if (enableDebug) _logger.LogWarning($"    Residential: +40 for name + unknown word");
            }
            else
            {
                // No name words at all in a two-word entry - likely business
                residentialScore -= 20;
                businessScore += 30;
                if (enableDebug) 
                {
                    _logger.LogWarning($"    Residential: -20 for no name words in two-word entry");
                    _logger.LogWarning($"    Business: +30 for no name words in two-word entry");
                }
            }
        }
        else
        {
            // For 3+ word entries without a detected pattern
            // Check if we have multiple name words - if so, it's likely still residential
            if (contextMap.Count >= 3 && nameWords >= 2)
            {
                residentialScore += 30;  // Multiple names suggest residential even without perfect pattern
                if (enableDebug) _logger.LogWarning($"    Residential: +30 for {nameWords} name words in multi-word entry");
            }
            else
            {
                residentialScore -= 20;
                if (enableDebug) _logger.LogWarning($"    Residential: -20 for no valid name pattern");
            }
        }
        
        if (patterns.HasFirstLastPattern)
        {
            residentialScore += 40;
            if (enableDebug) _logger.LogWarning($"    Residential: +40 for first+last pattern");
        }
        
        if (patterns.HasInitialPattern)
        {
            residentialScore += 50;
            if (enableDebug) _logger.LogWarning($"    Residential: +50 for initial pattern ({patterns.InitialPatternType})");
        }
        
        if (patterns.HasCouplePattern)
        {
            residentialScore += 70;  // Strong residential indicator - couples
            if (enableDebug) _logger.LogWarning($"    Residential: +70 for couple pattern (name & name)");
        }
        
        if (nameWords > 0)
        {
            residentialScore += nameWords * 15;
            if (enableDebug) _logger.LogWarning($"    Residential: +{nameWords * 15} for {nameWords} name words");
        }
        
        // Single word penalty for residential
        if (contextMap.Count == 1)
        {
            businessScore += 50;
            residentialScore -= 30;
            if (enableDebug)
            {
                _logger.LogWarning($"    Business: +50 for single word");
                _logger.LogWarning($"    Residential: -30 for single word");
            }
        }
        
        // Normalize scores
        if (enableDebug)
        {
            _logger.LogWarning($"\n  Raw scores before normalization:");
            _logger.LogWarning($"    Business: {businessScore}");
            _logger.LogWarning($"    Residential: {residentialScore}");
        }
        
        var total = businessScore + residentialScore;
        if (total > 0)
        {
            businessScore = (businessScore / total) * 100;
            residentialScore = (residentialScore / total) * 100;
        }
        
        if (enableDebug)
        {
            _logger.LogWarning($"\n  Normalized scores:");
            _logger.LogWarning($"    Business: {businessScore:F1}%");
            _logger.LogWarning($"    Residential: {residentialScore:F1}%");
        }
        
        // Special handling for entries with mostly unknown words
        int totalWords = contextMap.Count;
        
        // If most words are unknown and we have no strong indicators
        if (unknownWords >= totalWords / 2 && businessWords == 0 && nameWords <= 1)
        {
            // Count non-hyphenated words (split hyphenated words)
            int effectiveWordCount = 0;
            foreach (var context in contextMap)
            {
                if (context.Word.Contains('-'))
                {
                    effectiveWordCount += context.Word.Split('-').Length;
                }
                else
                {
                    effectiveWordCount++;
                }
            }
            
            // Default: 5 or fewer words = residential, more than 5 = business
            // Use lowest confidence (50%) since we're guessing
            if (effectiveWordCount <= 5)
            {
                result.Classification = "residential";
                result.Confidence = 50;
                result.IsResidential = true;
                result.Reason = "Unknown words - defaulting to residential (5 or fewer words)";
            }
            else
            {
                result.Classification = "business";
                result.Confidence = 50;
                result.IsBusiness = true;
                result.Reason = "Unknown words - defaulting to business (more than 5 words)";
            }
        }
        // Otherwise use the normal scoring
        else if (businessScore > residentialScore)
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
    
    private PatternAnalysis AnalyzePatterns(List<WordContext> contextMap, bool enableDebug = false)
    {
        var analysis = new PatternAnalysis();
        
        if (contextMap.Count == 0)
            return analysis;
        
        // Check for initial patterns first
        analysis = CheckForInitialPatterns(contextMap, analysis, enableDebug);
        
        // If we found an initial pattern, it's likely residential
        if (analysis.HasInitialPattern)
        {
            analysis.HasValidNamePattern = true;
            if (enableDebug)
            {
                _logger.LogWarning($"    Pattern: Has initial pattern ({analysis.InitialPatternType}) - marking as valid name pattern");
            }
            return analysis; // Early return for clear residential patterns
        }
        
        // Check for couple patterns (Name & Name) - STRONG residential indicator
        int connectorCount = contextMap.Count(c => c.PrimaryType == WordTypeEnum.Connector);
        if (connectorCount > 0)
        {
            // Count names around connectors
            int namesAroundConnectors = 0;
            for (int i = 0; i < contextMap.Count; i++)
            {
                if (contextMap[i].PrimaryType == WordTypeEnum.Connector)
                {
                    // Check if there's a name/initial before and after
                    bool hasNameBefore = i > 0 && (
                        contextMap[i-1].PrimaryType == WordTypeEnum.First ||
                        contextMap[i-1].PrimaryType == WordTypeEnum.Last ||
                        contextMap[i-1].PrimaryType == WordTypeEnum.Both ||
                        contextMap[i-1].PrimaryType == WordTypeEnum.Initial ||
                        contextMap[i-1].PrimaryType == WordTypeEnum.Unknown);
                    
                    bool hasNameAfter = i < contextMap.Count - 1 && (
                        contextMap[i+1].PrimaryType == WordTypeEnum.First ||
                        contextMap[i+1].PrimaryType == WordTypeEnum.Last ||
                        contextMap[i+1].PrimaryType == WordTypeEnum.Both ||
                        contextMap[i+1].PrimaryType == WordTypeEnum.Initial ||
                        contextMap[i+1].PrimaryType == WordTypeEnum.Unknown);
                    
                    if (hasNameBefore && hasNameAfter)
                    {
                        namesAroundConnectors++;
                        analysis.HasCouplePattern = true;
                        analysis.HasValidNamePattern = true;
                        if (enableDebug)
                        {
                            _logger.LogWarning($"    Pattern: Found couple pattern (name & name)");
                        }
                    }
                }
            }
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
    
    private PatternAnalysis CheckForInitialPatterns(List<WordContext> contextMap, PatternAnalysis analysis, bool enableDebug = false)
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
        int unknownCount = contextMap.Count(c => c.PrimaryType == WordTypeEnum.Unknown);
        
        // If we have initials with at least one name OR unknown word (potential name), it's likely a name pattern
        // Examples: "A Dizon" where Dizon is unknown, "J Smith" where Smith is known
        if (initialCount > 0 && (nameCount > 0 || unknownCount > 0))
        {
            analysis.HasInitialPattern = true;
            
            // Check specific patterns
            // Pattern 1: Name Initial(s) - "Smith J" or "Smith J M" or "Dizon A" (where Dizon is unknown)
            if (contextMap[0].PrimaryType == WordTypeEnum.Last || 
                contextMap[0].PrimaryType == WordTypeEnum.Both ||
                contextMap[0].PrimaryType == WordTypeEnum.Unknown)
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
            
            // Pattern 2: Initial(s) Name - "J Smith" or "J M Smith" or "A Dizon"
            if (contextMap[contextMap.Count - 1].PrimaryType == WordTypeEnum.Last || 
                contextMap[contextMap.Count - 1].PrimaryType == WordTypeEnum.Both ||
                contextMap[contextMap.Count - 1].PrimaryType == WordTypeEnum.Unknown)
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
    public bool HasCouplePattern { get; set; }
}