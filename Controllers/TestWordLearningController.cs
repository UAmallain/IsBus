using Microsoft.AspNetCore.Mvc;
using IsBus.Models;
using IsBus.Services;

namespace IsBus.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TestWordLearningController : ControllerBase
{
    private readonly IWordLearningService _wordLearningService;
    private readonly ILogger<TestWordLearningController> _logger;
    
    public TestWordLearningController(
        IWordLearningService wordLearningService,
        ILogger<TestWordLearningController> logger)
    {
        _wordLearningService = wordLearningService;
        _logger = logger;
    }
    
    /// <summary>
    /// Test word learning with a simple example
    /// </summary>
    [HttpPost("test")]
    public async Task<IActionResult> TestWordLearning()
    {
        var testResult = new ParseResult
        {
            Success = true,
            Input = "Test Abraham Kaine",
            IsResidentialName = true,
            FirstName = "Kaine",
            LastName = "Abraham",
            Name = "Abraham Kaine",
            Phone = "5555555",
            Address = "123 Test St"
        };
        
        _logger.LogInformation("Starting test word learning");
        
        try
        {
            var wordsLearned = await _wordLearningService.LearnFromParseResultAsync(testResult);
            
            return Ok(new
            {
                Success = true,
                WordsLearned = wordsLearned,
                TestResult = testResult,
                Message = $"Learned {wordsLearned} words. Check logs for details."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Test word learning failed");
            return StatusCode(500, new
            {
                Success = false,
                Error = ex.Message,
                StackTrace = ex.StackTrace
            });
        }
    }
    
    /// <summary>
    /// Test updating a specific word
    /// </summary>
    [HttpPost("test-word")]
    public async Task<IActionResult> TestUpdateWord([FromBody] TestWordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.Word) || string.IsNullOrWhiteSpace(request?.WordType))
        {
            return BadRequest("Word and WordType are required");
        }
        
        _logger.LogInformation($"Testing word update: '{request.Word}' as '{request.WordType}'");
        
        try
        {
            var shouldLearn = await _wordLearningService.ShouldLearnWordAsync(request.Word, false);
            if (!shouldLearn)
            {
                return Ok(new
                {
                    Success = false,
                    Message = $"Word '{request.Word}' should not be learned (filtered out)"
                });
            }
            
            var updated = await _wordLearningService.UpdateWordCountAsync(request.Word, request.WordType);
            
            return Ok(new
            {
                Success = updated,
                Word = request.Word,
                WordType = request.WordType,
                Message = updated ? "Word updated successfully" : "Failed to update word"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Test word update failed for '{request.Word}'");
            return StatusCode(500, new
            {
                Success = false,
                Error = ex.Message,
                StackTrace = ex.StackTrace
            });
        }
    }
}

public class TestWordRequest
{
    public string Word { get; set; } = string.Empty;
    public string WordType { get; set; } = string.Empty;
}