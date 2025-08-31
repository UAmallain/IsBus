using System;
using System.Text.RegularExpressions;

class TestSuiteDetection
{
    static void Main()
    {
        Console.WriteLine("Testing Suite Number Detection Logic");
        Console.WriteLine("=====================================\n");

        var testCases = new[]
        {
            ("Abed Sangani Masoud Dr 100 Arden Suite 209857-5732", "Suite 209, Phone 857-5732"),
            ("Abed Sangani Masoud Dr 100 Arden Suite 209 857-5732", "Suite 209, Phone 857-5732"),
            ("Smith John 123 Main St Apt 4506234-5678", "Apt 450, Phone 623-4567"),
            ("Smith John 123 Main St Apt 450 623-4567", "Apt 450, Phone 623-4567"),
            ("Johnson Mary 456 Oak Ave Unit 127 894-3212", "Unit 127, Phone 894-3212"),
            ("Wilson James 246 Birch Way 234-5678", "No suite, Phone 234-5678")
        };

        foreach (var (input, expected) in testCases)
        {
            Console.WriteLine($"Input: {input}");
            Console.WriteLine($"Expected: {expected}");
            
            var result = ExtractPhoneNumberSimplified(input);
            Console.WriteLine($"Result: Phone={result.Phone}, Remaining={result.Remaining}");
            Console.WriteLine();
        }
    }

    static (string Phone, string Remaining) ExtractPhoneNumberSimplified(string input)
    {
        // Phone pattern - matches ###-#### or ####-####
        var phonePattern = new Regex(@"\b(\d{3,4})-(\d{4})\b");
        
        // Suite indicators
        var suiteIndicators = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Suite", "Ste", "Unit", "Apt", "Apartment", 
            "Room", "Rm", "Floor", "Fl", "#"
        };

        var phoneMatch = phonePattern.Match(input);
        if (phoneMatch.Success)
        {
            var phone = phoneMatch.Value.Trim();
            var remaining = input.Substring(0, phoneMatch.Index).Trim();
            
            // Check for numbers before the phone that could be suite or area code
            var words = remaining.Split(' ');
            if (words.Length > 0)
            {
                var lastWord = words[^1];
                
                // Check if last word is a number
                if (Regex.IsMatch(lastWord, @"^\d+$"))
                {
                    // Look for suite indicator in the previous few words
                    var suiteFound = false;
                    for (int i = Math.Max(0, words.Length - 4); i < words.Length - 1; i++)
                    {
                        var word = words[i].TrimEnd('.', ',');
                        if (suiteIndicators.Contains(word))
                        {
                            suiteFound = true;
                            Console.WriteLine($"  Found suite indicator '{word}' near number '{lastWord}'");
                            break;
                        }
                    }
                    
                    if (!suiteFound && lastWord.Length == 3)
                    {
                        // Likely area code
                        phone = lastWord + phone;
                        remaining = string.Join(" ", words.Take(words.Length - 1));
                        Console.WriteLine($"  Treating '{lastWord}' as area code");
                    }
                    else if (suiteFound)
                    {
                        Console.WriteLine($"  Keeping '{lastWord}' as suite number");
                    }
                }
            }
            
            return (phone, remaining);
        }
        
        return ("", input);
    }
}