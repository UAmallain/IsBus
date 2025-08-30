# Migration Plan: Static Lists to Database-Driven Approach

## Current State Analysis

### Static Lists Found in Codebase:

1. **ClassificationService.cs**
   - `_absoluteBusinessIndicators[]` - Corporate suffixes (Inc, Ltd, Corp, etc.)
   - `_strongBusinessPatterns[]` - Business keyword patterns
   - `_residentialPatterns[]` - Residential name patterns

2. **DatabaseDrivenParserService.cs**
   - `businessIndicators` HashSet - Corporate terminators
   - `strongBusinessWords` HashSet - Business keywords (duplicated 3 times!)
   - Road indicators for address detection

3. **ImprovedStringParserService.cs**
   - `_businessWords` HashSet - Business-related words

4. **BusinessNameDetectionService.cs**
   - Business keywords and patterns

## Database Schema Available

### word_data table
- **word_lower**: The word in lowercase
- **word_type**: 'business', 'first', 'last', 'both', 'indeterminate'
- **word_count**: Frequency count
- **Examples from data**:
  - "equipment": word_count = 5054, type = 'business'
  - "cranes": word_count = 112, type = 'business'

### names table
- Contains first/last name data with counts
- Can be cross-referenced with word_data

## Proposed Business Classification Logic

### Business Strength Indicators Based on word_count:

```csharp
public enum BusinessIndicatorStrength
{
    None = 0,
    Weak = 1,      // word_count 10-99, no name entry
    Medium = 2,    // word_count 100-999, no name entry
    Strong = 3,    // word_count 1000-4999, no name entry
    Absolute = 4   // word_count >= 5000, no name entry OR corporate suffix
}
```

### Classification Rules:

1. **Absolute Business (Override Everything)**
   - word_count >= 5000 with word_type='business' AND no corresponding name entry
   - Corporate suffixes (Inc, Ltd, Corp, LLC, etc.) - could be stored with special marker

2. **Strong Business Indicator**
   - word_count >= 1000 with word_type='business' AND no name entry
   - Multiple medium indicators (2+ words with count > 100)

3. **Medium Business Indicator**
   - word_count >= 100 with word_type='business' AND no name entry

4. **Weak Business Indicator**
   - word_count >= 10 with word_type='business'

## Migration Steps

### Phase 1: Create Database-Driven Service
```csharp
public interface IBusinessWordService
{
    Task<BusinessIndicatorStrength> GetWordStrengthAsync(string word);
    Task<bool> IsStrongBusinessWordAsync(string word);
    Task<bool> IsCorporateSuffixAsync(string word);
    Task<Dictionary<string, BusinessIndicatorStrength>> AnalyzeWordsAsync(string[] words);
}
```

### Phase 2: Implement BusinessWordService
```csharp
public class BusinessWordService : IBusinessWordService
{
    private readonly PhonebookContext _context;
    
    public async Task<BusinessIndicatorStrength> GetWordStrengthAsync(string word)
    {
        var wordData = await _context.WordData
            .FirstOrDefaultAsync(w => w.WordLower == word.ToLower() && 
                                     w.WordType == "business");
        
        if (wordData == null) return BusinessIndicatorStrength.None;
        
        // Check if word exists as a name
        var hasNameEntry = await _context.Names
            .AnyAsync(n => n.NameLower == word.ToLower());
        
        if (hasNameEntry && wordData.WordCount < 1000)
            return BusinessIndicatorStrength.Weak; // Reduce strength if also a name
        
        return wordData.WordCount switch
        {
            >= 5000 => BusinessIndicatorStrength.Absolute,
            >= 1000 => BusinessIndicatorStrength.Strong,
            >= 100 => BusinessIndicatorStrength.Medium,
            >= 10 => BusinessIndicatorStrength.Weak,
            _ => BusinessIndicatorStrength.None
        };
    }
}
```

### Phase 3: Update Services to Use BusinessWordService

1. **Update ClassificationService**
   - Remove all static arrays
   - Inject IBusinessWordService
   - Replace static checks with database queries

2. **Update DatabaseDrivenParserService**
   - Remove the 3 duplicate strongBusinessWords HashSets
   - Use IBusinessWordService for business detection
   - Cache results for performance if needed

3. **Update Other Services**
   - ImprovedStringParserService
   - BusinessNameDetectionService

### Phase 4: Add Corporate Suffixes to Database

Create migration to add corporate suffixes to word_data:
```sql
INSERT INTO word_data (word_lower, word_type, word_count) VALUES
('inc', 'business', 99999),
('incorporated', 'business', 99999),
('ltd', 'business', 99999),
('limited', 'business', 99999),
('corp', 'business', 99999),
('corporation', 'business', 99999),
('llc', 'business', 99999),
('llp', 'business', 99999)
ON DUPLICATE KEY UPDATE word_count = 99999;
```

### Phase 5: Performance Optimization

1. **Add Caching Layer**
   ```csharp
   private readonly MemoryCache _cache;
   private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(30);
   ```

2. **Batch Queries**
   - Load all words for a phrase in one query
   - Use Include() for related data

3. **Add Database Indexes** (already exist)
   - idx_word_lower
   - idx_word_type
   - idx_word_count

## Benefits of Database Approach

1. **Dynamic Updates**: Business words can be added/updated without code changes
2. **Data-Driven**: Uses actual frequency data from real phonebook entries
3. **No Duplication**: Single source of truth in database
4. **Scalable**: Can handle millions of words efficiently
5. **Contextual**: Can consider both business and name usage
6. **Maintainable**: No need to update code for new business terms

## Implementation Priority

1. **High Priority** (Fixes current issues):
   - Implement BusinessWordService
   - Update DatabaseDrivenParserService (removes triple duplication)

2. **Medium Priority**:
   - Update ClassificationService
   - Add corporate suffixes to database

3. **Low Priority**:
   - Update other services
   - Add caching layer
   - Performance optimization

## Testing Strategy

1. Create test cases for known issues:
   - "A P Reid Insurance Stores Moncton" → Business
   - "A W Leil Cranes & Equipment" → Business
   - "A Mwinkeu C" → Residential

2. Verify word_data lookups:
   - "equipment" (5054 count) → Absolute indicator
   - "cranes" (112 count) → Medium indicator
   - "insurance" → Check actual count in DB

3. Performance testing:
   - Measure query times
   - Test with batch processing
   - Verify caching effectiveness