using FluentAssertions;
using IsBus.Data;
using IsBus.Models;
using IsBus.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace IsBus.Tests;

public class WordProcessingServiceTests : IDisposable
{
    private readonly PhonebookContext _context;
    private readonly WordProcessingService _service;
    private readonly Mock<IBusinessIndicatorService> _indicatorServiceMock;
    private readonly Mock<ILogger<WordProcessingService>> _loggerMock;

    public WordProcessingServiceTests()
    {
        var options = new DbContextOptionsBuilder<PhonebookContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        _context = new PhonebookContext(options);
        _indicatorServiceMock = new Mock<IBusinessIndicatorService>();
        _loggerMock = new Mock<ILogger<WordProcessingService>>();
        
        SetupMocks();
        
        _service = new WordProcessingService(_context, _indicatorServiceMock.Object, _loggerMock.Object);
    }

    private void SetupMocks()
    {
        _indicatorServiceMock.Setup(x => x.IsStopWord(It.IsIn("the", "and", "of", "in", "for", "a", "an")))
            .Returns(true);
    }

    [Fact]
    public async Task ProcessBusinessNameWordsAsync_WithNewWords_ShouldInsertIntoDatabase()
    {
        var businessName = "Acme Technologies Corporation";
        
        var result = await _service.ProcessBusinessNameWordsAsync(businessName);
        
        result.Should().Be(3);
        
        var words = await _context.Words.ToListAsync();
        words.Should().HaveCount(3);
        words.Should().Contain(w => w.WordLower == "acme" && w.WordCount == 1);
        words.Should().Contain(w => w.WordLower == "technologies" && w.WordCount == 1);
        words.Should().Contain(w => w.WordLower == "corporation" && w.WordCount == 1);
    }

    [Fact]
    public async Task ProcessBusinessNameWordsAsync_WithExistingWords_ShouldIncrementCount()
    {
        await _context.Words.AddAsync(new Word { WordLower = "acme", WordCount = 5 });
        await _context.SaveChangesAsync();
        
        var businessName = "Acme Corporation";
        
        var result = await _service.ProcessBusinessNameWordsAsync(businessName);
        
        result.Should().Be(2);
        
        var acmeWord = await _context.Words.FirstAsync(w => w.WordLower == "acme");
        acmeWord.WordCount.Should().Be(6);
        
        var corpWord = await _context.Words.FirstAsync(w => w.WordLower == "corporation");
        corpWord.WordCount.Should().Be(1);
    }

    [Fact]
    public async Task ProcessBusinessNameWordsAsync_ShouldFilterStopWords()
    {
        var businessName = "The Best Company of the World";
        
        var result = await _service.ProcessBusinessNameWordsAsync(businessName);
        
        result.Should().Be(3);
        
        var words = await _context.Words.Select(w => w.WordLower).ToListAsync();
        words.Should().NotContain(new[] { "the", "of" });
        words.Should().Contain(new[] { "best", "company", "world" });
    }

    [Fact]
    public async Task ProcessBusinessNameWordsAsync_ShouldHandleDuplicateWordsInInput()
    {
        var businessName = "Tech Tech Solutions Tech";
        
        var result = await _service.ProcessBusinessNameWordsAsync(businessName);
        
        result.Should().Be(2);
        
        var techWord = await _context.Words.FirstAsync(w => w.WordLower == "tech");
        techWord.WordCount.Should().Be(3);
        
        var solutionsWord = await _context.Words.FirstAsync(w => w.WordLower == "solutions");
        solutionsWord.WordCount.Should().Be(1);
    }

    [Fact]
    public async Task ProcessBusinessNameWordsAsync_WithSpecialCharacters_ShouldSplitCorrectly()
    {
        var businessName = "Smith & Jones, Inc. - Global/International";
        
        var result = await _service.ProcessBusinessNameWordsAsync(businessName);
        
        result.Should().Be(4);
        
        var words = await _context.Words.Select(w => w.WordLower).ToListAsync();
        words.Should().Contain(new[] { "smith", "jones", "inc", "global" });
    }

    [Fact]
    public async Task ProcessBusinessNameWordsAsync_ShouldFilterSingleCharacterWords()
    {
        var businessName = "A & B Corporation";
        
        var result = await _service.ProcessBusinessNameWordsAsync(businessName);
        
        result.Should().Be(1);
        
        var words = await _context.Words.Select(w => w.WordLower).ToListAsync();
        words.Should().NotContain("a");
        words.Should().NotContain("b");
        words.Should().Contain("corporation");
    }

    [Fact]
    public async Task ProcessBusinessNameWordsAsync_ShouldFilterNumericWords()
    {
        var businessName = "Company 123 456 Solutions";
        
        var result = await _service.ProcessBusinessNameWordsAsync(businessName);
        
        result.Should().Be(2);
        
        var words = await _context.Words.Select(w => w.WordLower).ToListAsync();
        words.Should().NotContain("123");
        words.Should().NotContain("456");
        words.Should().Contain(new[] { "company", "solutions" });
    }

    [Theory]
    [InlineData("", 0)]
    [InlineData(null, 0)]
    [InlineData("   ", 0)]
    [InlineData("the and of", 0)]
    public async Task ProcessBusinessNameWordsAsync_WithInvalidInput_ShouldReturnZero(string input, int expected)
    {
        var result = await _service.ProcessBusinessNameWordsAsync(input);
        
        result.Should().Be(expected);
        
        var words = await _context.Words.ToListAsync();
        words.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessBusinessNameWordsAsync_WithTransactionRollback_ShouldNotSaveChanges()
    {
        _context.Database.EnsureCreated();
        
        var dbContextMock = new Mock<PhonebookContext>(new DbContextOptions<PhonebookContext>());
        dbContextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));
        
        var service = new WordProcessingService(dbContextMock.Object, _indicatorServiceMock.Object, _loggerMock.Object);
        
        await Assert.ThrowsAsync<Exception>(() => service.ProcessBusinessNameWordsAsync("Test Company"));
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}