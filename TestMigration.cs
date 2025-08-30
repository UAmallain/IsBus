using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using IsBus.Services;
using IsBus.Data;
using IsBus.Models;
using Microsoft.Extensions.Caching.Memory;

// Simple test to verify the migration is working
public class TestMigration
{
    public static async Task Main()
    {
        var services = new ServiceCollection();
        
        // Setup services
        services.AddDbContext<PhonebookContext>(options =>
            options.UseMySql("Server=localhost;Database=bor_db;User=root;Password=YourStrong!Passw0rd;",
                ServerVersion.AutoDetect("Server=localhost;Database=bor_db;User=root;Password=YourStrong!Passw0rd;")));
        
        services.AddMemoryCache();
        services.AddLogging(builder => builder.AddConsole());
        services.AddScoped<IBusinessWordService, BusinessWordService>();
        
        var serviceProvider = services.BuildServiceProvider();
        var businessWordService = serviceProvider.GetRequiredService<IBusinessWordService>();
        var logger = serviceProvider.GetRequiredService<ILogger<TestMigration>>();
        
        Console.WriteLine("Testing Migration Results:");
        Console.WriteLine("==========================");
        
        // Test 1: Corporate suffix
        var isCorpSuffix = await businessWordService.IsCorporateSuffixAsync("Inc");
        Console.WriteLine($"1. 'Inc' is corporate suffix: {isCorpSuffix} (Expected: True)");
        
        // Test 2: Abraham analysis
        var abrahamStrength = await businessWordService.GetWordStrengthAsync("Abraham");
        Console.WriteLine($"2. 'Abraham' strength: {abrahamStrength} (Expected: None or Weak due to higher name counts)");
        
        // Test 3: Kaine analysis
        var kaineStrength = await businessWordService.GetWordStrengthAsync("Kaine");
        Console.WriteLine($"3. 'Kaine' strength: {kaineStrength} (Expected: None or Weak due to higher name counts)");
        
        // Test 4: Phrase analysis
        var phraseAnalysis = await businessWordService.AnalyzePhraseAsync("Abraham Kaine");
        Console.WriteLine($"4. 'Abraham Kaine' is business: {phraseAnalysis.isBusiness} (Expected: False)");
        Console.WriteLine($"   Reason: {phraseAnalysis.reason}");
        
        // Test 5: Corporate phrase
        var corpPhrase = await businessWordService.AnalyzePhraseAsync("A P Reid Insurance Stores Inc");
        Console.WriteLine($"5. 'A P Reid Insurance Stores Inc' is business: {corpPhrase.isBusiness} (Expected: True)");
        Console.WriteLine($"   Reason: {corpPhrase.reason}");
        
        // Test 6: Check word counts in database
        var context = serviceProvider.GetRequiredService<PhonebookContext>();
        
        var incWord = await context.WordData
            .Where(w => w.WordLower == "inc" && w.WordType == "business")
            .FirstOrDefaultAsync();
        Console.WriteLine($"6. 'inc' business count in DB: {incWord?.WordCount ?? 0} (Expected: 99999)");
        
        var abrahamWords = await context.WordData
            .Where(w => w.WordLower == "abraham")
            .ToListAsync();
        Console.WriteLine($"7. 'abraham' entries in DB:");
        foreach (var word in abrahamWords)
        {
            Console.WriteLine($"   - {word.WordType}: {word.WordCount}");
        }
        
        Console.WriteLine("\nMigration test complete!");
    }
}