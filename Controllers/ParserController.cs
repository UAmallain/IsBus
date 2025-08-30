using Microsoft.AspNetCore.Mvc;
using IsBus.Models;
using IsBus.Services;

namespace IsBus.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ParserController : ControllerBase
{
    private readonly IStringParserService _parserService;
    private readonly ILogger<ParserController> _logger;
    
    public ParserController(
        IStringParserService parserService,
        ILogger<ParserController> logger)
    {
        _parserService = parserService;
        _logger = logger;
    }
    
    /// <summary>
    /// Debug endpoint for Abraham Kaine case
    /// </summary>
    [HttpGet("debug/abraham")]
    public async Task<IActionResult> DebugAbraham()
    {
        var result = await _parserService.ParseAsync("Abraham Kaine 17 Kingsmere 388-3515");
        return Ok(new
        {
            ParseResult = result,
            Message = "Check logs for detailed analysis"
        });
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
    public async Task<IActionResult> ParseGet([FromQuery] string input, [FromQuery] string? province = null, [FromQuery] string? areaCode = null)
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