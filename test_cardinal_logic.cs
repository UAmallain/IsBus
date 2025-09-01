using System;
using System.Text.RegularExpressions;
using System.Linq;

class TestCardinalLogic
{
    static void Main()
    {
        Console.WriteLine("Testing Cardinal Direction Logic");
        Console.WriteLine("================================\n");

        var testCases = new[]
        {
            ("Abraham Cheryl SE 14-52-19-W4 Red Deer Co Delburne783-2424", "SE", 2),
            ("Smith John N 123 Main St Calgary 403-123-4567", "N", 2),
            ("ABC North 789 Business Plaza Calgary 403-987-6543", "North", 1),
            ("Northern Lights Electric NE 321 Industrial Way 403-234-5678", "NE", 3),
            ("Wilson Robert W 555 Elm Street 403-111-2222", "W", 2),
            ("K South 234 Pine Ave 403-555-6666", "South", 1),
            ("Johnson Mary Southeast 456 Oak Road 780-777-8888", "Southeast", 2)
        };

        foreach (var (input, expectedCardinal, namePartsBefore) in testCases)
        {
            Console.WriteLine($"Input: {input}");
            Console.WriteLine($"Expected cardinal: {expectedCardinal}, Name parts before: {namePartsBefore}");
            
            var result = AnalyzeCardinalDirection(input);
            Console.WriteLine($"Result: {result}");
            Console.WriteLine();
        }
    }

    static string AnalyzeCardinalDirection(string input)
    {
        var words = input.Split(' ');
        
        for (int i = 0; i < words.Length; i++)
        {
            var word = words[i];
            
            // Check if this is a cardinal direction
            bool isCardinalDirection = Regex.IsMatch(word, 
                @"^(N|S|E|W|NE|NW|SE|SW|North|South|East|West|Northeast|Northwest|Southeast|Southwest)$", 
                RegexOptions.IgnoreCase);
            
            if (isCardinalDirection)
            {
                // Count name parts before this cardinal
                int namePartsBeforeCardinal = 0;
                for (int j = 0; j < i; j++)
                {
                    var wordBefore = words[j];
                    // Skip connectors and non-name parts
                    if (wordBefore != "&" && !wordBefore.Equals("et", StringComparison.OrdinalIgnoreCase) &&
                        !wordBefore.StartsWith("(") && !wordBefore.EndsWith(")"))
                    {
                        namePartsBeforeCardinal++;
                    }
                }
                
                // Check if next word is a number
                bool nextIsNumber = false;
                if (i + 1 < words.Length)
                {
                    nextIsNumber = Regex.IsMatch(words[i + 1], @"^\d+");
                }
                
                // Determine if this cardinal marks address start
                string decision;
                if (namePartsBeforeCardinal >= 2)
                {
                    decision = $"Cardinal '{word}' MARKS ADDRESS START (2+ name parts: {namePartsBeforeCardinal})";
                }
                else if (namePartsBeforeCardinal == 1 && nextIsNumber)
                {
                    decision = $"Cardinal '{word}' MARKS ADDRESS START (1 name part + number follows)";
                }
                else
                {
                    decision = $"Cardinal '{word}' DOES NOT mark address start (only {namePartsBeforeCardinal} name part)";
                }
                
                // Extract name and address based on decision
                string name = string.Join(" ", words.Take(i));
                string address = decision.Contains("MARKS ADDRESS") 
                    ? string.Join(" ", words.Skip(i).TakeWhile(w => !Regex.IsMatch(w, @"\d{3}-\d{4}")))
                    : "Not determined";
                
                return $"Name: '{name}' | Address starts: '{address}' | {decision}";
            }
        }
        
        return "No cardinal direction found";
    }
}