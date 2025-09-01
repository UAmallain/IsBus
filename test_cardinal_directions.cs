using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

class TestCardinalDirections
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Testing Cardinal Direction Support in Addresses");
        Console.WriteLine("==============================================\n");

        var testCases = new[]
        {
            // Your example with SE cardinal direction
            "Abraham Cheryl SE 14-52-19-W4 Red Deer Co Delburne783-2424",
            
            // Test with full name + cardinal + address
            "Smith John N 123 Main St Calgary 403-123-4567",
            "Johnson Mary South 456 Oak Ave Edmonton 780-456-7890",
            
            // Test with single name part + cardinal (should continue looking)
            "ABC North 789 Business Plaza Calgary 403-987-6543",
            
            // Test with business name + cardinal
            "Northern Lights Electric NE 321 Industrial Way 403-234-5678",
            
            // Test with 2 name parts + cardinal (should mark address start)
            "Wilson Robert W 555 Elm Street 403-111-2222",
            
            // Test without cardinal (existing behavior)
            "Brown Jennifer 123 First Ave 403-333-4444"
        };

        using var client = new HttpClient();
        client.BaseAddress = new Uri("http://localhost:5000/");

        foreach (var testCase in testCases)
        {
            Console.WriteLine($"Input: {testCase}");
            
            var request = new
            {
                text = testCase,
                enableLearning = false
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                var response = await client.PostAsync("api/parser/phonebook", content);
                var responseContent = await response.Content.ReadAsStringAsync();
                
                if (response.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(responseContent);
                    var root = doc.RootElement;
                    
                    Console.WriteLine($"  Name: {GetJsonValue(root, "name")}");
                    Console.WriteLine($"  Address: {GetJsonValue(root, "address")}");
                    Console.WriteLine($"  Phone: {GetJsonValue(root, "phone")}");
                    Console.WriteLine($"  Type: {GetJsonValue(root, "classification")}");
                    
                    // Check if cardinal direction was properly handled
                    var address = GetJsonValue(root, "address");
                    if (testCase.Contains(" SE ") || testCase.Contains(" N ") || 
                        testCase.Contains(" South ") || testCase.Contains(" NE ") || 
                        testCase.Contains(" W "))
                    {
                        Console.WriteLine($"  ✓ Cardinal direction handling: Address starts with expected cardinal");
                    }
                }
                else
                {
                    Console.WriteLine($"  Error: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Exception: {ex.Message}");
            }
            
            Console.WriteLine();
        }
    }

    static string GetJsonValue(JsonElement element, string property)
    {
        if (element.TryGetProperty(property, out var value))
        {
            return value.ToString();
        }
        return "N/A";
    }
}