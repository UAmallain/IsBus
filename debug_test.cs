using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;

public class DebugTestEntry
{
    public string Input { get; set; }
}

public class DebugParseRequest
{
    public string Input { get; set; }
    public string Province { get; set; } = "NB";
    public string AreaCode { get; set; } = "506";
    public bool EnableDebug { get; set; } = true;
}

public class DebugParseResponse
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

class DebugTest
{
    static async Task Main(string[] args)
    {
        var testCases = new List<DebugTestEntry>
        {
            new DebugTestEntry { Input = "Adshade Wilfred 231 Westminster Av Riverview 386-8496" },
            new DebugTestEntry { Input = "Adsett Robert B 210 Country View Rd 384-8266" },
            new DebugTestEntry { Input = "Adesina A Mountain Rd 830-7098" },
            new DebugTestEntry { Input = "Adesina 0 382-4955" },
            new DebugTestEntry { Input = "Adeoye Haqq 183 Oakfield Dr 386-4519" },
            new DebugTestEntry { Input = "Adebukola Bada 115 Lorne St 388-2674" },
            new DebugTestEntry { Input = "Addington Andrea 26 Fairlane Dr 851-1340" },
            new DebugTestEntry { Input = "Adao Aurora 858-8912" },
            new DebugTestEntry { Input = "Adams Walter 88 Sunset Rd 853-4115" },
            new DebugTestEntry { Input = "Adams Todd 1009 Cleveland Av 853-8202" },
            new DebugTestEntry { Input = "Adams T 925 Elmwood Dr 204-4110" },
            new DebugTestEntry { Input = "Adams S Grindstone Dr 854-8144" },
            new DebugTestEntry { Input = "Adams Nick 7 Parkwood Av 533-7099" },
            new DebugTestEntry { Input = "Adams M 97 Notingham Dr 204-8479" },
            new DebugTestEntry { Input = "Adams Jessica 19 Naples Dr 854-1588" },
            new DebugTestEntry { Input = "Adams D 115 Chateau Dr 857-0697" },
            new DebugTestEntry { Input = "Adair A 53 Dundee Dr 372-9518" },
            new DebugTestEntry { Input = "Ackman F Douglas Dr Cap - Brûlé 532-3836" },
            new DebugTestEntry { Input = "Abli R A22 21 Arden St 830-8056" },
            new DebugTestEntry { Input = "Aberathna B 150 Wynwood Dr 830-7163" },
            new DebugTestEntry { Input = "Abdelhadi Z 152 Holland Dr 830-2167" }
        };

        var debugLog = new StringBuilder();
        debugLog.AppendLine($"=== DEBUG TEST RUN - {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\n");
        
        using (var client = new HttpClient())
        {
            client.BaseAddress = new Uri("http://localhost:5000/");
            client.Timeout = TimeSpan.FromSeconds(30);
            
            foreach (var test in testCases)
            {
                debugLog.AppendLine($"\n{new string('=', 80)}\nINPUT: {test.Input}\n{new string('=', 80)}");
                
                var request = new DebugParseRequest { Input = test.Input };
                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                try
                {
                    var response = await client.PostAsync("api/Parser/parse", content);
                    var responseText = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<DebugParseResponse>(responseText, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    
                    debugLog.AppendLine($"RESULT: {(result.isBusinessName ? "BUSINESS" : "RESIDENTIAL")}");
                    debugLog.AppendLine($"  Classification: {result.classification ?? "null"}");
                    debugLog.AppendLine($"  Scores: Business={result.businessScore}, Residential={result.residentialScore}");
                    debugLog.AppendLine($"  Confidence: {result.confidence?["nameConfidence"]}%");
                    debugLog.AppendLine($"  Reason: {result.classificationReason ?? "null"}");
                    
                    if (result.isResidentialName)
                    {
                        debugLog.AppendLine($"  Name Parts: First='{result.firstName ?? ""}', Last='{result.lastName ?? ""}'");
                    }
                    else
                    {
                        debugLog.AppendLine($"  Business Name: '{result.name ?? ""}'");
                    }
                    
                    debugLog.AppendLine($"  Address: '{result.address ?? ""}'");
                    debugLog.AppendLine($"  Phone: '{result.phone ?? ""}'");
                }
                catch (Exception ex)
                {
                    debugLog.AppendLine($"ERROR: {ex.Message}");
                }
            }
        }
        
        var logFile = "debug_classification_log.txt";
        File.WriteAllText(logFile, debugLog.ToString());
        Console.WriteLine($"Debug log written to: {logFile}");
        Console.WriteLine("\nSummary of results written to log file.");
    }
}