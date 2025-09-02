using Microsoft.AspNetCore.Mvc;
using IsBus.Models;
using IsBus.Services;
using System.Text.RegularExpressions;

namespace IsBus.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DebugParsingController : ControllerBase
{
    private readonly IStringParserService _parserService;
    private readonly IBusinessWordService _businessWordService;
    private readonly ILogger<DebugParsingController> _logger;
    
    public DebugParsingController(
        IStringParserService parserService,
        IBusinessWordService businessWordService,
        ILogger<DebugParsingController> logger)
    {
        _parserService = parserService;
        _businessWordService = businessWordService;
        _logger = logger;
    }
    
    /// <summary>
    /// Debug parsing to show step-by-step how the address detection works
    /// </summary>
    [HttpGet("trace")]
    public async Task<IActionResult> TraceAddressDetection([FromQuery] string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return BadRequest("Input string is required");
        }
        
        var steps = new List<object>();
        ParseResult? parseResult = null;
        
        try
        {
            // Split into words
            var words = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            steps.Add(new { Step = "Split into words", Words = words });
            
            // Track where address detection happens
            int addressStartIndex = -1;
            string detectionReason = "";
            
            // Check each word
            for (int i = 0; i < words.Length; i++)
            {
                var word = words[i];
                
                // Get business word info from database
                var businessWordInfo = await _businessWordService.GetBusinessWordInfoAsync(word);
                
                // Check if number is bounded
                bool isNumber = Regex.IsMatch(word, @"^\d+$");
                bool isBounded = false;
                if (isNumber && i > 0)
                {
                    var prevWord = words[i - 1];
                    var nextWord = (i + 1 < words.Length) ? words[i + 1] : "";
                    isBounded = (prevWord == "(" || prevWord == "[" || prevWord == "{" || prevWord == "\"" || prevWord == "'") ||
                               (nextWord == ")" || nextWord == "]" || nextWord == "}" || nextWord == "\"" || nextWord == "'") ||
                               word.StartsWith("(") || word.EndsWith(")");
                }
                
                var wordAnalysis = new
                {
                    Position = i,
                    Word = word,
                    IsNumber = isNumber,
                    IsBoundedNumber = isBounded,
                    IsUnit = IsUnitNumber(word),
                    IsStreetType = IsStreetType(word),
                    IsCardinal = Regex.IsMatch(word, @"^(N|S|E|W|NE|NW|SE|SW|North|South|East|West)$", RegexOptions.IgnoreCase),
                    ContainsApostrophe = word.Contains("'"),
                    BusinessWordInfo = new
                    {
                        IsBusinessWord = businessWordInfo.IsBusinessWord,
                        Strength = businessWordInfo.Strength.ToString(),
                        Source = businessWordInfo.Source,
                        Count = businessWordInfo.Count
                    }
                };
                
                steps.Add(new { Step = $"Analyzing word {i}", Analysis = wordAnalysis });
                
                // Apply detection logic
                if (wordAnalysis.IsNumber && !wordAnalysis.IsBoundedNumber && addressStartIndex == -1)
                {
                    // Check if there are strong business indicators before this
                    bool hasStrongBusinessBefore = false;
                    for (int j = 0; j < i; j++)
                    {
                        var bizInfo = await _businessWordService.GetBusinessWordInfoAsync(words[j]);
                        if (bizInfo.IsBusinessWord && (bizInfo.Strength == BusinessIndicatorStrength.Strong || 
                                                       bizInfo.Strength == BusinessIndicatorStrength.Absolute))
                        {
                            hasStrongBusinessBefore = true;
                            steps.Add(new { 
                                Step = $"Found strong business word '{words[j]}' before number",
                                Strength = bizInfo.Strength.ToString(),
                                Source = bizInfo.Source,
                                Decision = "Continue looking - business name likely continues"
                            });
                            break;
                        }
                    }
                    
                    if (!hasStrongBusinessBefore)
                    {
                        addressStartIndex = i;
                        detectionReason = $"First number at position {i}";
                        steps.Add(new { 
                            Step = "Address detection",
                            Reason = detectionReason,
                            AddressStartIndex = addressStartIndex
                        });
                        break;
                    }
                }
                else if (wordAnalysis.IsUnit && addressStartIndex == -1)
                {
                    addressStartIndex = i;
                    detectionReason = $"Unit indicator at position {i}";
                    steps.Add(new { 
                        Step = "Address detection",
                        Reason = detectionReason,
                        AddressStartIndex = addressStartIndex
                    });
                    break;
                }
                else if (wordAnalysis.IsCardinal && addressStartIndex == -1)
                {
                    // Check how many name parts before cardinal
                    int namePartsBefore = 0;
                    for (int j = 0; j < i; j++)
                    {
                        if (!IsBusinessKeyword(words[j]) && !words[j].Contains("'s"))
                        {
                            namePartsBefore++;
                        }
                    }
                    
                    steps.Add(new { 
                        Step = $"Cardinal direction found",
                        NamePartsBefore = namePartsBefore,
                        Decision = namePartsBefore >= 2 ? "Start address here" : "Continue - not enough name parts"
                    });
                    
                    if (namePartsBefore >= 2)
                    {
                        addressStartIndex = i;
                        detectionReason = $"Cardinal direction with {namePartsBefore} name parts before";
                        steps.Add(new { 
                            Step = "Address detection",
                            Reason = detectionReason,
                            AddressStartIndex = addressStartIndex
                        });
                        break;
                    }
                }
                else if (wordAnalysis.IsStreetType && addressStartIndex == -1 && i > 0)
                {
                    // Check for possessive apostrophe in previous word
                    var prevWord = words[i - 1];
                    if (prevWord.Contains("'"))
                    {
                        steps.Add(new { 
                            Step = $"Street type '{word}' found but skipped",
                            Reason = $"Previous word '{prevWord}' has possessive apostrophe"
                        });
                    }
                    else
                    {
                        // Check for business suffix pattern
                        if (i + 1 < words.Length && IsBusinessSuffix(words[i + 1]))
                        {
                            steps.Add(new { 
                                Step = $"Street type '{word}' found but skipped",
                                Reason = $"Next word '{words[i + 1]}' is business suffix"
                            });
                        }
                        else
                        {
                            addressStartIndex = i - 1; // Include the word before street type
                            detectionReason = $"Street type '{word}' at position {i}";
                            steps.Add(new { 
                                Step = "Address detection",
                                Reason = detectionReason,
                                AddressStartIndex = addressStartIndex
                            });
                            break;
                        }
                    }
                }
            }
            
            // Determine name and address split
            string name = "";
            string address = "";
            
            if (addressStartIndex > 0)
            {
                name = string.Join(" ", words.Take(addressStartIndex));
                address = string.Join(" ", words.Skip(addressStartIndex));
            }
            else if (addressStartIndex == 0)
            {
                address = input;
            }
            else
            {
                name = input;
            }
            
            steps.Add(new { 
                Step = "Final split",
                Name = name,
                Address = address,
                AddressStartIndex = addressStartIndex
            });
            
            // Get actual parse result
            parseResult = await _parserService.ParseAsync(input);
            
            return Ok(new
            {
                Input = input,
                Steps = steps,
                ParseResult = parseResult
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error tracing parse for: {Input}", input);
            return StatusCode(500, "An error occurred while tracing the parse");
        }
    }
    
    private bool IsUnitNumber(string word)
    {
        return Regex.IsMatch(word, @"^\d+[A-Za-z]$") ||  // 123A
               Regex.IsMatch(word, @"^[A-Za-z]\d+$") ||   // A123
               Regex.IsMatch(word, @"^#\d+$");            // #123
    }
    
    private bool IsStreetType(string word)
    {
        var streetTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Street", "St", "Avenue", "Ave", "Road", "Rd", "Drive", "Dr",
            "Court", "Ct", "Place", "Pl", "Boulevard", "Blvd", "Lane", "Ln",
            "Way", "Trail", "Tr", "Circle", "Cir", "Crescent", "Cres",
            "Gate", "Gates", "Park", "Parkway", "Pkwy", "Highway", "Hwy"
        };
        
        return streetTypes.Contains(word.TrimEnd('.', ','));
    }
    
    private bool IsBusinessKeyword(string word)
    {
        var businessWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Apartments", "Apartment", "Apts", "Apt",
            "Gardens", "Garden", "Gate", "Gates",
            "Plaza", "Centre", "Center", "Mall",
            "Tower", "Towers", "Building", "Complex"
        };
        
        return businessWords.Contains(word.TrimEnd('.', ','));
    }
    
    private bool IsBusinessSuffix(string word)
    {
        var suffixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Apartments", "Apartment", "Apts", "Apt",
            "Gardens", "Garden", "Plaza", "Mall",
            "Centre", "Center", "Complex"
        };
        
        return suffixes.Contains(word.TrimEnd('.', ','));
    }
    
    private bool IsStrongBusinessWord(string word)
    {
        var strongWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Apartments", "Gardens", "Plaza", "Centre", "Center", 
            "Mall", "Tower", "Towers", "Complex", "Building"
        };
        
        return strongWords.Contains(word.TrimEnd('.', ','));
    }
}