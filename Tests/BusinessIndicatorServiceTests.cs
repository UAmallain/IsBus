using FluentAssertions;
using IsBus.Models;
using IsBus.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace IsBus.Tests;

public class BusinessIndicatorServiceTests
{
    private readonly BusinessIndicatorService _service;
    private readonly IMemoryCache _cache;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly Mock<ILogger<BusinessIndicatorService>> _loggerMock;

    public BusinessIndicatorServiceTests()
    {
        _cache = new MemoryCache(new MemoryCacheOptions());
        _configurationMock = new Mock<IConfiguration>();
        _loggerMock = new Mock<ILogger<BusinessIndicatorService>>();
        
        SetupConfiguration();
        
        _service = new BusinessIndicatorService(_configurationMock.Object, _cache, _loggerMock.Object);
    }

    private void SetupConfiguration()
    {
        var primarySuffixesSection = new Mock<IConfigurationSection>();
        primarySuffixesSection.Setup(x => x.Value).Returns("LLC,Inc,Corp");
        primarySuffixesSection.Setup(x => x.GetChildren()).Returns(new[]
        {
            CreateConfigSection("LLC"),
            CreateConfigSection("Inc"),
            CreateConfigSection("Corp")
        });

        var secondaryIndicatorsSection = new Mock<IConfigurationSection>();
        secondaryIndicatorsSection.Setup(x => x.GetChildren()).Returns(new[]
        {
            CreateConfigSection("Services"),
            CreateConfigSection("Solutions"),
            CreateConfigSection("Group")
        });

        var stopWordsSection = new Mock<IConfigurationSection>();
        stopWordsSection.Setup(x => x.GetChildren()).Returns(new[]
        {
            CreateConfigSection("the"),
            CreateConfigSection("and"),
            CreateConfigSection("of")
        });

        var businessIndicatorsSection = new Mock<IConfigurationSection>();
        businessIndicatorsSection.Setup(x => x.GetSection("PrimarySuffixes")).Returns(primarySuffixesSection.Object);
        businessIndicatorsSection.Setup(x => x.GetSection("SecondaryIndicators")).Returns(secondaryIndicatorsSection.Object);
        businessIndicatorsSection.Setup(x => x.GetSection("CommonStopWords")).Returns(stopWordsSection.Object);

        _configurationMock.Setup(x => x.GetSection("BusinessIndicators")).Returns(businessIndicatorsSection.Object);
    }

    private IConfigurationSection CreateConfigSection(string value)
    {
        var section = new Mock<IConfigurationSection>();
        section.Setup(x => x.Value).Returns(value);
        return section.Object;
    }

    [Theory]
    [InlineData("LLC", true)]
    [InlineData("llc", true)]
    [InlineData("Inc", true)]
    [InlineData("Corp", true)]
    [InlineData("Services", false)]
    [InlineData("Random", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsPrimarySuffix_ShouldReturnCorrectResult(string word, bool expected)
    {
        var result = _service.IsPrimarySuffix(word);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("Services", true)]
    [InlineData("services", true)]
    [InlineData("Solutions", true)]
    [InlineData("Group", true)]
    [InlineData("LLC", false)]
    [InlineData("Random", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsSecondaryIndicator_ShouldReturnCorrectResult(string word, bool expected)
    {
        var result = _service.IsSecondaryIndicator(word);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("the", true)]
    [InlineData("The", true)]
    [InlineData("and", true)]
    [InlineData("of", true)]
    [InlineData("LLC", false)]
    [InlineData("Company", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsStopWord_ShouldReturnCorrectResult(string word, bool expected)
    {
        var result = _service.IsStopWord(word);
        result.Should().Be(expected);
    }

    [Fact]
    public void GetIndicators_ShouldCacheConfiguration()
    {
        var indicators1 = _service.GetIndicators();
        var indicators2 = _service.GetIndicators();
        
        indicators1.Should().BeSameAs(indicators2);
    }
}