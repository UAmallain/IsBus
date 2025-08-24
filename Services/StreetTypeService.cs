using System.Text.RegularExpressions;

namespace IsBus.Services;

public interface IStreetTypeService
{
    bool IsStreetType(string word);
    string? GetStreetTypeStandardForm(string word);
    bool ContainsStreetType(string text, out string? streetType, out int position);
    bool IsLikelyDoctorTitle(string text, int position);
}

public class StreetTypeService : IStreetTypeService
{
    // English street types with their abbreviations
    private readonly Dictionary<string, string> _englishStreetTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        // Common types
        { "street", "Street" }, { "st", "Street" },
        { "road", "Road" }, { "rd", "Road" },
        { "avenue", "Avenue" }, { "ave", "Avenue" }, { "av", "Avenue" },
        { "drive", "Drive" }, { "dr", "Drive" },
        { "lane", "Lane" }, { "ln", "Lane" },
        { "boulevard", "Boulevard" }, { "blvd", "Boulevard" },
        { "court", "Court" }, { "ct", "Court" },
        { "place", "Place" }, { "pl", "Place" },
        { "circle", "Circle" }, { "cir", "Circle" },
        { "trail", "Trail" }, { "tr", "Trail" },
        { "parkway", "Parkway" }, { "pkwy", "Parkway" }, { "pky", "Parkway" },
        { "highway", "Highway" }, { "hwy", "Highway" },
        { "way", "Way" }, { "wy", "Way" },
        { "terrace", "Terrace" }, { "ter", "Terrace" }, { "terr", "Terrace" },
        { "plaza", "Plaza" }, { "plz", "Plaza" },
        { "square", "Square" }, { "sq", "Square" },
        { "crescent", "Crescent" }, { "cres", "Crescent" }, { "cr", "Crescent" },
        { "grove", "Grove" }, { "grv", "Grove" },
        { "park", "Park" }, { "pk", "Park" },
        { "point", "Point" }, { "pt", "Point" },
        { "heights", "Heights" }, { "hts", "Heights" },
        { "gardens", "Gardens" }, { "gdns", "Gardens" },
        { "meadows", "Meadows" }, { "mdws", "Meadows" },
        { "ridge", "Ridge" }, { "rdg", "Ridge" },
        { "view", "View" }, { "vw", "View" },
        { "crossing", "Crossing" }, { "xing", "Crossing" },
        { "alley", "Alley" }, { "aly", "Alley" },
        { "bypass", "Bypass" }, { "byp", "Bypass" },
        { "expressway", "Expressway" }, { "expy", "Expressway" },
        { "freeway", "Freeway" }, { "fwy", "Freeway" },
        { "route", "Route" }, { "rte", "Route" }, { "rt", "Route" },
        
        // Less common but important
        { "close", "Close" }, { "cl", "Close" },
        { "commons", "Commons" }, { "cmns", "Commons" },
        { "cove", "Cove" }, { "cv", "Cove" },
        { "estates", "Estates" }, { "est", "Estates" },
        { "green", "Green" }, { "grn", "Green" },
        { "hill", "Hill" }, { "hl", "Hill" },
        { "hollow", "Hollow" }, { "holw", "Hollow" },
        { "island", "Island" }, { "is", "Island" },
        { "junction", "Junction" }, { "jct", "Junction" },
        { "landing", "Landing" }, { "lndg", "Landing" },
        { "loop", "Loop" },
        { "mall", "Mall" },
        { "manor", "Manor" }, { "mnr", "Manor" },
        { "mews", "Mews" },
        { "pass", "Pass" },
        { "path", "Path" },
        { "pike", "Pike" },
        { "promenade", "Promenade" }, { "prom", "Promenade" },
        { "row", "Row" },
        { "run", "Run" },
        { "spur", "Spur" },
        { "station", "Station" }, { "sta", "Station" },
        { "turnpike", "Turnpike" }, { "tpke", "Turnpike" },
        { "valley", "Valley" }, { "vly", "Valley" },
        { "viaduct", "Viaduct" }, { "via", "Viaduct" },
        { "village", "Village" }, { "vlg", "Village" },
        { "walk", "Walk" },
        { "wharf", "Wharf" }, { "whf", "Wharf" }
    };

    // French street types with their abbreviations
    private readonly Dictionary<string, string> _frenchStreetTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        { "rue", "Rue" },
        { "route", "Route" }, { "rte", "Route" },
        { "chemin", "Chemin" }, { "ch", "Chemin" },
        { "avenue", "Avenue" }, { "av", "Avenue" },
        { "boulevard", "Boulevard" }, { "boul", "Boulevard" }, { "blvd", "Boulevard" },
        { "rang", "Rang" }, { "rg", "Rang" },
        { "montée", "Montée" }, { "montee", "Montée" },
        { "côte", "Côte" }, { "cote", "Côte" },
        { "place", "Place" }, { "pl", "Place" },
        { "allée", "Allée" }, { "allee", "Allée" },
        { "impasse", "Impasse" }, { "imp", "Impasse" },
        { "croissant", "Croissant" }, { "crois", "Croissant" },
        { "terrasse", "Terrasse" }, { "ter", "Terrasse" },
        { "cercle", "Cercle" },
        { "carré", "Carré" }, { "carre", "Carré" },
        { "sentier", "Sentier" },
        { "passage", "Passage" }, { "pass", "Passage" },
        { "promenade", "Promenade" }, { "prom", "Promenade" },
        { "quai", "Quai" },
        { "voie", "Voie" },
        { "parc", "Parc" },
        { "jardin", "Jardin" }, { "jard", "Jardin" },
        { "esplanade", "Esplanade" }, { "espl", "Esplanade" },
        { "autoroute", "Autoroute" }, { "aut", "Autoroute" },
        { "concession", "Concession" }, { "conc", "Concession" },
        { "ligne", "Ligne" }, { "ln", "Ligne" }
    };

    // Titles that might conflict with street abbreviations
    private readonly HashSet<string> _titles = new(StringComparer.OrdinalIgnoreCase)
    {
        "dr", "doctor", "docteur", "mr", "mrs", "ms", "miss", "mme", "mlle", "m"
    };

    private readonly ILogger<StreetTypeService> _logger;

    public StreetTypeService(ILogger<StreetTypeService> logger)
    {
        _logger = logger;
    }

    public bool IsStreetType(string word)
    {
        if (string.IsNullOrWhiteSpace(word))
            return false;

        word = word.Trim().Trim('.', ',');
        return _englishStreetTypes.ContainsKey(word) || _frenchStreetTypes.ContainsKey(word);
    }

    public string? GetStreetTypeStandardForm(string word)
    {
        if (string.IsNullOrWhiteSpace(word))
            return null;

        word = word.Trim().Trim('.', ',');
        
        if (_englishStreetTypes.TryGetValue(word, out var englishForm))
            return englishForm;
            
        if (_frenchStreetTypes.TryGetValue(word, out var frenchForm))
            return frenchForm;
            
        return null;
    }

    public bool ContainsStreetType(string text, out string? streetType, out int position)
    {
        streetType = null;
        position = -1;

        if (string.IsNullOrWhiteSpace(text))
            return false;

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        for (int i = 0; i < words.Length; i++)
        {
            var word = words[i].Trim().Trim('.', ',');
            
            // Special handling for "Dr" - check if it's likely a title
            if (word.Equals("dr", StringComparison.OrdinalIgnoreCase))
            {
                if (IsLikelyDoctorTitle(text, i))
                    continue;
            }
            
            if (IsStreetType(word))
            {
                streetType = GetStreetTypeStandardForm(word);
                position = i;
                
                // Calculate character position in original string
                int charPos = 0;
                for (int j = 0; j < i; j++)
                {
                    charPos += words[j].Length + 1; // +1 for space
                }
                position = charPos;
                
                return true;
            }
        }

        return false;
    }

    public bool IsLikelyDoctorTitle(string text, int wordPosition)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        // If "Dr" is at position 0 or 1, it's likely a title
        if (wordPosition <= 1)
            return true;
        
        // If followed by a name-like word (capitalized), it's likely a title
        if (wordPosition < words.Length - 1)
        {
            var nextWord = words[wordPosition + 1];
            if (char.IsUpper(nextWord[0]) && !IsStreetType(nextWord))
            {
                // Check if the next word looks like a first or last name
                if (!Regex.IsMatch(nextWord, @"^\d+"))
                {
                    return true;
                }
            }
        }
        
        // If preceded by a number, it's likely "Drive"
        if (wordPosition > 0)
        {
            var prevWord = words[wordPosition - 1];
            if (Regex.IsMatch(prevWord, @"^\d+$"))
            {
                return false; // It's likely "Drive"
            }
        }
        
        // Default to treating it as Drive if we're not sure
        return false;
    }
}