using FluentAssertions;
using IsBus.Controllers;
using IsBus.Models;
using IsBus.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace IsBus.Tests;

public class BusinessNameControllerTests
{
    private readonly BusinessNameController _controller;
    private readonly Mock<IBusinessNameDetectionService> _detectionServiceMock;
    private readonly Mock<ILogger<BusinessNameController>> _loggerMock;

    public BusinessNameControllerTests()
    {
        _detectionServiceMock = new Mock<IBusinessNameDetectionService>();
        _loggerMock = new Mock<ILogger<BusinessNameController>>();
        _controller = new BusinessNameController(_detectionServiceMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task CheckBusinessName_WithValidRequest_ShouldReturnOk()
    {
        var request = new BusinessNameCheckRequest { Input = "Test Company LLC" };
        var expectedResponse = new BusinessNameCheckResponse
        {
            Input = "Test Company LLC",
            IsBusinessName = true,
            Confidence = 85.5,
            MatchedIndicators = new List<string> { "LLC" },
            WordsProcessed = 2
        };
        
        _detectionServiceMock.Setup(x => x.CheckBusinessNameAsync(request.Input))
            .ReturnsAsync(expectedResponse);
        
        var result = await _controller.CheckBusinessName(request);
        
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().BeEquivalentTo(expectedResponse);
    }

    [Fact]
    public async Task CheckBusinessName_WithInvalidModelState_ShouldReturnBadRequest()
    {
        var request = new BusinessNameCheckRequest { Input = "" };
        _controller.ModelState.AddModelError("Input", "Input is required");
        
        var result = await _controller.CheckBusinessName(request);
        
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CheckBusinessName_WhenServiceThrowsException_ShouldReturn500()
    {
        var request = new BusinessNameCheckRequest { Input = "Test Company" };
        
        _detectionServiceMock.Setup(x => x.CheckBusinessNameAsync(It.IsAny<string>()))
            .ThrowsAsync(new Exception("Service error"));
        
        var result = await _controller.CheckBusinessName(request);
        
        result.Should().BeOfType<ObjectResult>();
        var objectResult = result as ObjectResult;
        objectResult!.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }

    [Fact]
    public async Task CheckBusinessName_ShouldLogInformation()
    {
        var request = new BusinessNameCheckRequest { Input = "Test Company" };
        var response = new BusinessNameCheckResponse
        {
            Input = "Test Company",
            IsBusinessName = false,
            Confidence = 45,
            MatchedIndicators = new List<string>(),
            WordsProcessed = 0
        };
        
        _detectionServiceMock.Setup(x => x.CheckBusinessNameAsync(request.Input))
            .ReturnsAsync(response);
        
        await _controller.CheckBusinessName(request);
        
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Test Company")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void Health_ShouldReturnOkWithStatus()
    {
        var result = _controller.Health();
        
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().NotBeNull();
        
        var value = okResult.Value as dynamic;
        ((string)value!.status).Should().Be("healthy");
    }

    [Theory]
    [InlineData("ABC Corporation", true, 85.0)]
    [InlineData("random text", false, 20.0)]
    [InlineData("Global Services LLC", true, 90.0)]
    public async Task CheckBusinessName_WithVariousInputs_ShouldReturnExpectedResults(
        string input, 
        bool expectedIsBusinessName, 
        double expectedConfidence)
    {
        var request = new BusinessNameCheckRequest { Input = input };
        var response = new BusinessNameCheckResponse
        {
            Input = input,
            IsBusinessName = expectedIsBusinessName,
            Confidence = expectedConfidence,
            MatchedIndicators = new List<string>(),
            WordsProcessed = expectedIsBusinessName ? 2 : 0
        };
        
        _detectionServiceMock.Setup(x => x.CheckBusinessNameAsync(input))
            .ReturnsAsync(response);
        
        var result = await _controller.CheckBusinessName(request);
        
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        var actualResponse = okResult!.Value as BusinessNameCheckResponse;
        
        actualResponse!.IsBusinessName.Should().Be(expectedIsBusinessName);
        actualResponse.Confidence.Should().Be(expectedConfidence);
    }
}