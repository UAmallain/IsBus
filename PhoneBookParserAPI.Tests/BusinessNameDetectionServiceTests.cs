using FluentAssertions;
using IsBus.Models;
using IsBus.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace IsBus.Tests;

public class BusinessNameDetectionServiceTests
{
    private readonly BusinessNameDetectionService _service;
    private readonly Mock<IBusinessIndicatorService> _indicatorServiceMock;
    private readonly Mock<IWordProcessingService> _wordProcessingServiceMock;
    private readonly Mock<IWordFrequencyService> _wordFrequencyServiceMock;
    private readonly Mock<ILogger<BusinessNameDetectionService>> _loggerMock;

    public BusinessNameDetectionServiceTests()
    {
        _indicatorServiceMock = new Mock<IBusinessIndicatorService>();
        _wordProcessingServiceMock = new Mock<IWordProcessingService>();
        _wordFrequencyServiceMock = new Mock<IWordFrequencyService>();
        _loggerMock = new Mock<ILogger<BusinessNameDetectionService>>();
        
        SetupMocks();
        
        _service = new BusinessNameDetectionService(
            _indicatorServiceMock.Object,
            _wordProcessingServiceMock.Object,
            _wordFrequencyServiceMock.Object,
            _loggerMock.Object);
    }

    private void SetupMocks()
    {
        _indicatorServiceMock.Setup(x => x.IsPrimarySuffix(It.IsIn("LLC", "Inc", "Corp", "Ltd")))
            .Returns(true);
        
        _indicatorServiceMock.Setup(x => x.IsSecondaryIndicator(It.IsIn("Services", "Solutions", "Group", "Technologies")))
            .Returns(true);
        
        _indicatorServiceMock.Setup(x => x.IsStopWord(It.IsIn("the", "and", "of", "in", "for")))
            .Returns(true);
        
        _wordProcessingServiceMock.Setup(x => x.ProcessBusinessNameWordsAsync(It.IsAny<string>()))
            .ReturnsAsync(3);
    }

    [Theory]
    [InlineData("Microsoft Corporation", true, 70, 100)]
    [InlineData("Apple Inc", true, 70, 100)]
    [InlineData("ABC Services LLC", true, 80, 100)]
    [InlineData("Smith & Associates", true, 70, 90)]
    [InlineData("Global Technologies Group", true, 70, 100)]
    [InlineData("john doe", false, 0, 50)]
    [InlineData("hello world", false, 0, 50)]
    [InlineData("", false, 0, 0)]
    [InlineData(null, false, 0, 0)]
    public async Task CheckBusinessNameAsync_ShouldReturnExpectedConfidence(
        string input, 
        bool expectedIsBusinessName, 
        double minConfidence, 
        double maxConfidence)
    {
        var result = await _service.CheckBusinessNameAsync(input);
        
        result.Should().NotBeNull();
        result.Input.Should().Be(input ?? string.Empty);
        result.IsBusinessName.Should().Be(expectedIsBusinessName);
        result.Confidence.Should().BeInRange(minConfidence, maxConfidence);
        
        if (expectedIsBusinessName)
        {
            result.WordsProcessed.Should().Be(3);
        }
        else
        {
            result.WordsProcessed.Should().Be(0);
        }
    }

    [Fact]
    public async Task CheckBusinessNameAsync_WithPrimarySuffix_ShouldHaveHighConfidence()
    {
        var result = await _service.CheckBusinessNameAsync("Acme Corporation");
        
        result.IsBusinessName.Should().BeTrue();
        result.Confidence.Should().BeGreaterThan(70);
        result.MatchedIndicators.Should().Contain("Corporation");
    }

    [Fact]
    public async Task CheckBusinessNameAsync_WithMultipleIndicators_ShouldHaveHigherConfidence()
    {
        var result = await _service.CheckBusinessNameAsync("Global Solutions Services LLC");
        
        result.IsBusinessName.Should().BeTrue();
        result.Confidence.Should().BeGreaterThan(80);
        result.MatchedIndicators.Should().Contain(new[] { "Solutions", "Services", "LLC" });
    }

    [Fact]
    public async Task CheckBusinessNameAsync_WithAmpersand_ShouldIncreaseConfidence()
    {
        var result = await _service.CheckBusinessNameAsync("Johnson & Johnson");
        
        result.Confidence.Should().BeGreaterThan(60);
    }

    [Fact]
    public async Task CheckBusinessNameAsync_AllCapitalized_ShouldIncreaseConfidence()
    {
        var result = await _service.CheckBusinessNameAsync("ABC DEF GHI");
        
        result.Confidence.Should().BeGreaterThan(20);
    }

    [Fact]
    public async Task CheckBusinessNameAsync_WhenWordProcessingFails_ShouldStillReturnResult()
    {
        _wordProcessingServiceMock
            .Setup(x => x.ProcessBusinessNameWordsAsync(It.IsAny<string>()))
            .ThrowsAsync(new Exception("Database error"));
        
        var result = await _service.CheckBusinessNameAsync("Test Company LLC");
        
        result.Should().NotBeNull();
        result.IsBusinessName.Should().BeTrue();
        result.WordsProcessed.Should().Be(0);
    }

    [Theory]
    [InlineData("The Best Company", "Company")]
    [InlineData("Services for All", "Services")]
    [InlineData("Inc. and Co.", "Inc")]
    public async Task CheckBusinessNameAsync_ShouldFilterStopWords(string input, string expectedIndicator)
    {
        var result = await _service.CheckBusinessNameAsync(input);
        
        result.MatchedIndicators.Should().Contain(expectedIndicator);
    }
}