using IsBus.Models;
using IsBus.Services;
using Microsoft.AspNetCore.Mvc;

namespace IsBus.Controllers;

[ApiController]
[Route("api/business-name")]
public class BusinessNameController : ControllerBase
{
    private readonly IBusinessNameDetectionService _detectionService;
    private readonly ILogger<BusinessNameController> _logger;

    public BusinessNameController(
        IBusinessNameDetectionService detectionService,
        ILogger<BusinessNameController> logger)
    {
        _detectionService = detectionService;
        _logger = logger;
    }

    [HttpPost("check")]
    [ProducesResponseType(typeof(BusinessNameCheckResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CheckBusinessName([FromBody] BusinessNameCheckRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            _logger.LogInformation("Received business name check request for: {Input}", request.Input);

            var response = await _detectionService.CheckBusinessNameAsync(request.Input);
            
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing business name check for input: {Input}", request.Input);
            
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                error = "An error occurred while processing your request",
                timestamp = DateTime.UtcNow
            });
        }
    }

    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
    }
}