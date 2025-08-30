using Microsoft.AspNetCore.Mvc;
using IsBus.Models;
using IsBus.Services;

namespace IsBus.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ParserController : ControllerBase
{
    private readonly IStringParserService _parserService;
    private readonly IWordLearningService _wordLearningService;
    private readonly ILogger<ParserController> _logger;
    private readonly IConfiguration _configuration;
    
    public ParserController(
        IStringParserService parserService,
        IWordLearningService wordLearningService,
        ILogger<ParserController> logger,
        IConfiguration configuration)
    {
        _parserService = parserService;
        _wordLearningService = wordLearningService;
        _logger = logger;
        _configuration = configuration;
    }
    
    /// <summary>
    /// Parse a string into name, address, and phone number components
    /// </summary>
    [HttpPost("parse")]
    public async Task<IActionResult> Parse([FromBody] ParseRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.Input))
        {
            return BadRequest(new ParseResult
            {
                Success = false,
                ErrorMessage = "Input string is required"
            });
        }
        
        try
        {
            var result = await _parserService.ParseAsync(request.Input, request.Province, request.AreaCode);
            
            if (!result.Success)
            {
                return BadRequest(result);
            }
            
            // Learn from successful parse if enabled
            var enableLearning = request.EnableLearning ?? _configuration.GetValue<bool>("WordLearning:EnabledByDefault", false);
            
            if (enableLearning)
            {
                try
                {
                    _logger.LogInformation($"Learning enabled - attempting to learn from parse result for: {request.Input}");
                    var wordsLearned = await _wordLearningService.LearnFromParseResultAsync(result);
                    _logger.LogInformation($"Word learning service returned {wordsLearned} words learned from: {request.Input}");
                }
                catch (Exception learnEx)
                {
                    // Don't fail the request if learning fails
                    _logger.LogError(learnEx, "Failed to learn from parse result for input: {Input}", request.Input);
                }
            }
            else
            {
                _logger.LogDebug($"Learning disabled for parse request: {request.Input}");
            }
            
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing input: {Input}", request.Input);
            return StatusCode(500, new ParseResult
            {
                Success = false,
                ErrorMessage = "An error occurred while parsing the input"
            });
        }
    }
    
    /// <summary>
    /// Parse multiple strings in a batch
    /// </summary>
    [HttpPost("parse/batch")]
    public async Task<IActionResult> ParseBatch([FromBody] BatchParseRequest request)
    {
        if (request?.Inputs == null || !request.Inputs.Any())
        {
            return BadRequest(new BatchParseResult
            {
                TotalProcessed = 0,
                FailureCount = 1
            });
        }
        
        if (request.Inputs.Count > 500)
        {
            return BadRequest(new BatchParseResult
            {
                TotalProcessed = 0,
                FailureCount = request.Inputs.Count,
                Results = new List<ParseResult> 
                { 
                    new ParseResult 
                    { 
                        Success = false, 
                        ErrorMessage = "Maximum 500 inputs allowed per batch" 
                    } 
                }
            });
        }
        
        try
        {
            var result = await _parserService.ParseBatchAsync(request.Inputs, request.Province, request.AreaCode);
            
            // Learn from successful parses in batch if enabled
            var enableLearning = request.EnableLearning ?? _configuration.GetValue<bool>("WordLearning:EnabledByDefault", false);
            
            if (enableLearning)
            {
                try
                {
                    int totalWordsLearned = 0;
                    foreach (var parseResult in result.Results.Where(r => r.Success))
                    {
                        var wordsLearned = await _wordLearningService.LearnFromParseResultAsync(parseResult);
                        totalWordsLearned += wordsLearned;
                    }
                    
                    if (totalWordsLearned > 0)
                    {
                        _logger.LogInformation($"Learned {totalWordsLearned} words from batch of {result.SuccessCount} successful parses");
                    }
                }
                catch (Exception learnEx)
                {
                    // Don't fail the request if learning fails
                    _logger.LogWarning(learnEx, "Failed to learn from batch parse results");
                }
            }
            else
            {
                _logger.LogDebug($"Learning disabled for batch parse request with {request.Inputs.Count} inputs");
            }
            
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in batch parsing");
            return StatusCode(500, new BatchParseResult
            {
                TotalProcessed = 0,
                FailureCount = request.Inputs.Count
            });
        }
    }
    
    /// <summary>
    /// Parse a string from query parameter (GET method)
    /// </summary>
    [HttpGet("parse")]
    public async Task<IActionResult> ParseGet([FromQuery] string input, [FromQuery] string? province = null, [FromQuery] string? areaCode = null, [FromQuery] bool? enableLearning = null)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return BadRequest(new ParseResult
            {
                Success = false,
                ErrorMessage = "Input string is required"
            });
        }
        
        try
        {
            var result = await _parserService.ParseAsync(input, province, areaCode);
            
            if (!result.Success)
            {
                return BadRequest(result);
            }
            
            // Learn from successful parse if enabled
            var shouldLearn = enableLearning ?? _configuration.GetValue<bool>("WordLearning:EnabledByDefault", false);
            
            if (shouldLearn)
            {
                try
                {
                    _logger.LogInformation($"[GET] Learning enabled - attempting to learn from parse result for: {input}");
                    var wordsLearned = await _wordLearningService.LearnFromParseResultAsync(result);
                    _logger.LogInformation($"[GET] Word learning service returned {wordsLearned} words learned from: {input}");
                }
                catch (Exception learnEx)
                {
                    // Don't fail the request if learning fails
                    _logger.LogError(learnEx, "[GET] Failed to learn from parse result for input: {Input}", input);
                }
            }
            else
            {
                _logger.LogDebug($"[GET] Learning disabled for parse request: {input}");
            }
            
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing input: {Input}", input);
            return StatusCode(500, new ParseResult
            {
                Success = false,
                ErrorMessage = "An error occurred while parsing the input"
            });
        }
    }
}