using Microsoft.AspNetCore.Mvc;
using IsBus.Services;

namespace IsBus.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ClassificationController : ControllerBase
{
    private readonly IClassificationService _classificationService;
    private readonly ILogger<ClassificationController> _logger;

    public ClassificationController(
        IClassificationService classificationService,
        ILogger<ClassificationController> logger)
    {
        _classificationService = classificationService;
        _logger = logger;
    }

    /// <summary>
    /// Classify input as business or residential
    /// </summary>
    /// <param name="request">The classification request</param>
    /// <returns>Classification result with confidence score</returns>
    [HttpPost("classify")]
    public async Task<ActionResult<ClassificationResponse>> Classify([FromBody] ClassificationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Input))
        {
            return BadRequest(new ClassificationResponse
            {
                Success = false,
                Message = "Input cannot be empty"
            });
        }

        try
        {
            var result = await _classificationService.ClassifyAsync(request.Input);
            
            return Ok(new ClassificationResponse
            {
                Success = true,
                Input = request.Input,
                Classification = result.Classification,
                Confidence = result.Confidence,
                IsBusiness = result.Classification == "business",
                IsResidential = result.Classification == "residential",
                Reason = result.Reason,
                BusinessScore = result.BusinessScore,
                ResidentialScore = result.ResidentialScore,
                DetailedAnalysis = request.IncludeDetails ? new DetailedAnalysis
                {
                    Words = result.Words,
                    Scores = result.DetailedScores
                } : null
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error classifying input: {Input}", request.Input);
            return StatusCode(500, new ClassificationResponse
            {
                Success = false,
                Message = "An error occurred during classification"
            });
        }
    }

    /// <summary>
    /// Batch classify multiple inputs
    /// </summary>
    /// <param name="request">Batch classification request</param>
    /// <returns>List of classification results</returns>
    [HttpPost("classify/batch")]
    public async Task<ActionResult<BatchClassificationResponse>> ClassifyBatch([FromBody] BatchClassificationRequest request)
    {
        if (request.Inputs == null || request.Inputs.Count == 0)
        {
            return BadRequest(new BatchClassificationResponse
            {
                Success = false,
                Message = "No inputs provided"
            });
        }

        if (request.Inputs.Count > 100)
        {
            return BadRequest(new BatchClassificationResponse
            {
                Success = false,
                Message = "Maximum 100 inputs allowed per batch"
            });
        }

        try
        {
            var results = new List<ClassificationResult>();
            
            foreach (var input in request.Inputs)
            {
                if (!string.IsNullOrWhiteSpace(input))
                {
                    var result = await _classificationService.ClassifyAsync(input);
                    results.Add(new ClassificationResult
                    {
                        Input = input,
                        Classification = result.Classification,
                        Confidence = result.Confidence,
                        IsBusiness = result.Classification == "business",
                        IsResidential = result.Classification == "residential",
                        Reason = result.Reason
                    });
                }
            }

            var businessCount = results.Count(r => r.IsBusiness);
            var residentialCount = results.Count(r => r.IsResidential);
            
            return Ok(new BatchClassificationResponse
            {
                Success = true,
                TotalProcessed = results.Count,
                BusinessCount = businessCount,
                ResidentialCount = residentialCount,
                UnknownCount = results.Count - businessCount - residentialCount,
                Results = results
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in batch classification");
            return StatusCode(500, new BatchClassificationResponse
            {
                Success = false,
                Message = "An error occurred during batch classification"
            });
        }
    }

    /// <summary>
    /// Quick check if input is likely residential (simplified endpoint)
    /// </summary>
    /// <param name="input">The text to check</param>
    /// <returns>True if likely residential</returns>
    [HttpGet("is-residential")]
    public async Task<ActionResult<bool>> IsResidential([FromQuery] string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return BadRequest(false);
        }

        try
        {
            var result = await _classificationService.ClassifyAsync(input);
            return Ok(result.Classification == "residential" && result.Confidence >= 60);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if residential: {Input}", input);
            return StatusCode(500, false);
        }
    }

    /// <summary>
    /// Quick check if input is likely business (simplified endpoint)
    /// </summary>
    /// <param name="input">The text to check</param>
    /// <returns>True if likely business</returns>
    [HttpGet("is-business")]
    public async Task<ActionResult<bool>> IsBusiness([FromQuery] string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return BadRequest(false);
        }

        try
        {
            var result = await _classificationService.ClassifyAsync(input);
            return Ok(result.Classification == "business" && result.Confidence >= 60);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if business: {Input}", input);
            return StatusCode(500, false);
        }
    }
}

// Request/Response DTOs
public class ClassificationRequest
{
    public string Input { get; set; } = string.Empty;
    public bool IncludeDetails { get; set; } = false;
}

public class ClassificationResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string Input { get; set; } = string.Empty;
    public string Classification { get; set; } = string.Empty;
    public int Confidence { get; set; }
    public bool IsBusiness { get; set; }
    public bool IsResidential { get; set; }
    public string Reason { get; set; } = string.Empty;
    public int BusinessScore { get; set; }
    public int ResidentialScore { get; set; }
    public DetailedAnalysis? DetailedAnalysis { get; set; }
}

public class DetailedAnalysis
{
    public List<string> Words { get; set; } = new();
    public Dictionary<string, double> Scores { get; set; } = new();
}

public class BatchClassificationRequest
{
    public List<string> Inputs { get; set; } = new();
}

public class BatchClassificationResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public int TotalProcessed { get; set; }
    public int BusinessCount { get; set; }
    public int ResidentialCount { get; set; }
    public int UnknownCount { get; set; }
    public List<ClassificationResult> Results { get; set; } = new();
}

public class ClassificationResult
{
    public string Input { get; set; } = string.Empty;
    public string Classification { get; set; } = string.Empty;
    public int Confidence { get; set; }
    public bool IsBusiness { get; set; }
    public bool IsResidential { get; set; }
    public string Reason { get; set; } = string.Empty;
}