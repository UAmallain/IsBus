using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;

public class TestEntry
{
    public string Input { get; set; }
    public string ExpectedType { get; set; }
    public Dictionary<string, object> WordData { get; set; }
}

public class TestParseRequest
{
    public string Input { get; set; }
    public string Province { get; set; } = "NB";
    public string AreaCode { get; set; } = "506";
}

public class TestParseResponse
{
    public bool success { get; set; }
    public string name { get; set; }
    public string lastName { get; set; }
    public string firstName { get; set; }
    public string address { get; set; }
    public string phone { get; set; }
    public string community { get; set; }
    public string province { get; set; }
    public bool isBusinessName { get; set; }
    public bool isResidentialName { get; set; }
    public Dictionary<string, object> confidence { get; set; }
    public string classification { get; set; }
    public int businessScore { get; set; }
    public int residentialScore { get; set; }
    public string classificationReason { get; set; }
}

class TestInitialPatterns
{
    static async Task Main(string[] args)
    {
        var testCases = new List<TestEntry>
        {
            new TestEntry 
            { 
                Input = "A Dizon 38 Rachel St 351-3045",
                ExpectedType = "residential",
                WordData = new Dictionary<string, object> { ["dizon"] = "last: 98, first: 36, business: 1" }
            },
            new TestEntry 
            { 
                Input = "A Kevin 25 Anne St 830-5522",
                ExpectedType = "residential",
                WordData = new Dictionary<string, object> { ["kevin"] = "first: 464, last: 20, both: 7226, business: 481" }
            },
            new TestEntry 
            { 
                Input = "A Leblanc 454 Route 933 Beaubassin East 532-3602",
                ExpectedType = "residential",
                WordData = new Dictionary<string, object> { ["leblanc"] = "first: 20, last: 196, both: 17060, business: 204" }
            },
            new TestEntry 
            { 
                Input = "A Noel 148 Houlahan St Dieppe 830-0985",
                ExpectedType = "residential",
                WordData = new Dictionary<string, object> { ["noel"] = "first: 68, last: 42, both: 672, business: 89" }
            },
            new TestEntry 
            { 
                Input = "Abbass A 26 Fairway Blvd Riverview 386-1708",
                ExpectedType = "residential",
                WordData = new Dictionary<string, object> { ["abbass"] = "last: 164, business: 11" }
            },
            new TestEntry 
            { 
                Input = "Abbass S 43 Woodleigh St 384-4803",
                ExpectedType = "residential",
                WordData = new Dictionary<string, object> { ["abbass"] = "last: 164, business: 11" }
            },
            new TestEntry 
            { 
                Input = "Abdelhadi Z 152 Holland Dr 830-2167",
                ExpectedType = "residential",
                WordData = new Dictionary<string, object> { ["abdelhadi"] = "last: 12" }
            },
            new TestEntry 
            { 
                Input = "Abdelhai H B 14 Ruth St 830-5066",
                ExpectedType = "residential",
                WordData = new Dictionary<string, object> { ["abdelhai"] = "last: 8" }
            },
            new TestEntry 
            { 
                Input = "Abdoulaye M 2115 Amirault St Dieppe 204-9407",
                ExpectedType = "residential",
                WordData = new Dictionary<string, object> { ["abdoulaye"] = "first: 10, last: 4, both: 26" }
            },
            new TestEntry 
            { 
                Input = "Aberathna B 150 Wynwood Dr 830-7163",
                ExpectedType = "residential",
                WordData = new Dictionary<string, object> { ["aberathna"] = "last: 10" }
            }
        };

        Console.WriteLine("\n=== Testing Initial Pattern Recognition ===\n");
        
        using (var client = new HttpClient())
        {
            client.BaseAddress = new Uri("http://localhost:5000/");
            client.Timeout = TimeSpan.FromSeconds(30);
            
            int passed = 0;
            int failed = 0;
            
            foreach (var test in testCases)
            {
                var request = new TestParseRequest { Input = test.Input };
                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                try
                {
                    var response = await client.PostAsync("api/Parser/parse", content);
                    var responseText = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<TestParseResponse>(responseText, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    
                    bool isCorrect = (test.ExpectedType == "residential" && result.isResidentialName) ||
                                    (test.ExpectedType == "business" && result.isBusinessName);
                    
                    if (isCorrect)
                    {
                        passed++;
                        Console.WriteLine($"✓ PASS: {test.Input}");
                        Console.WriteLine($"  Classified as: {(result.isResidentialName ? "residential" : "business")}");
                    }
                    else
                    {
                        failed++;
                        Console.WriteLine($"✗ FAIL: {test.Input}");
                        Console.WriteLine($"  Expected: {test.ExpectedType}, Got: {(result.isBusinessName ? "business" : "residential")}");
                        Console.WriteLine($"  Scores: Business={result.businessScore}, Residential={result.residentialScore}");
                        Console.WriteLine($"  Reason: {result.classificationReason}");
                    }
                    
                    // Show parsed name details for residential entries
                    if (result.isResidentialName)
                    {
                        Console.WriteLine($"  Name: {result.name}, LastName: {result.lastName}, FirstName: {result.firstName}");
                    }
                    
                    Console.WriteLine();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"✗ ERROR: {test.Input}");
                    Console.WriteLine($"  {ex.Message}");
                    failed++;
                }
            }
            
            Console.WriteLine($"\n=== RESULTS ===");
            Console.WriteLine($"Passed: {passed}/{testCases.Count}");
            Console.WriteLine($"Failed: {failed}/{testCases.Count}");
            Console.WriteLine($"Success Rate: {(passed * 100.0 / testCases.Count):F1}%");
        }
    }
}