namespace IsBus.Models;

public class ParseResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string Input { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? LastName { get; set; }
    public string? FirstName { get; set; }
    public string Address { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public bool IsBusinessName { get; set; }
    public bool IsResidentialName { get; set; }
    public ParseConfidence Confidence { get; set; } = new();
}

public class ParseConfidence
{
    public int NameConfidence { get; set; }
    public int AddressConfidence { get; set; }
    public int PhoneConfidence { get; set; }
    public string? Notes { get; set; }
}

public class ParseRequest
{
    public string Input { get; set; } = string.Empty;
    public string? Province { get; set; } // Optional: NS, NB, PE, NL, etc.
    public string? AreaCode { get; set; } // Optional: 3-digit area code (e.g., "902", "506")
    public bool? EnableLearning { get; set; } // Optional: Enable word learning from this parse (null = use default)
}

public class BatchParseRequest
{
    public List<string> Inputs { get; set; } = new();
    public string? Province { get; set; } // Optional: applies to all inputs in batch
    public string? AreaCode { get; set; } // Optional: applies to all inputs in batch
    public bool? EnableLearning { get; set; } // Optional: Enable word learning from this batch (null = use default)
}

public class BatchParseResult
{
    public List<ParseResult> Results { get; set; } = new();
    public int TotalProcessed { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
}