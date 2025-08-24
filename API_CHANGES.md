# IsBus API Technical Changes Documentation

## Session Date: 2025-08-22

### Initial State
- Basic business name detection using hardcoded patterns
- Two separate tables: `names` and `words` in `phonebook_db`
- Simple classification based on pattern matching

### Major Architectural Changes

#### 1. Database Restructuring
**Problem**: Separate `names` and `words` tables made classification complex and inefficient

**Solution**: Unified `word_data` table
```sql
CREATE TABLE word_data (
    word_id INT PRIMARY KEY AUTO_INCREMENT,
    word_lower VARCHAR(255) NOT NULL,
    word_type ENUM('first', 'last', 'both', 'business', 'indeterminate') NOT NULL,
    word_count INT DEFAULT 1,
    -- ... timestamps
)
```

**Files Created/Modified**:
- `Database/create_unified_words_table.sql`
- `Database/cleanup_old_tables.sql`
- `Database/update_views_and_functions.sql`
- `Models/WordData.cs`
- `Data/PhonebookContext.cs`

#### 2. Classification System Overhaul

**Original Issues**:
- "Inc" classified as residential
- Single surnames classified as residential
- No distinction between legitimate names and business words

**Improvements Made**:

##### Absolute Business Indicators
```csharp
private readonly string[] _absoluteBusinessIndicators = new[]
{
    "inc", "incorporated", "corp", "corporation", "ltd", "limited", 
    "llc", "llp", "lp", "plc", "gmbh", "ag", "sa", "nv", "bv"
};
```

##### Residential Pattern Requirements
- Must have 2+ words (first name/initial + last name)
- Single words → always business
- Minimum name count threshold = 10 to filter noise

##### Context Mapping System
```
Example: "A Hair Affair With Addy"
Context Map: [indeterminate, business, business, business, both]
Result: Business (pattern analysis)
```

**Files Created/Modified**:
- `Services/ClassificationService.cs` - Enhanced with comprehensive checks
- `Services/ContextClassificationService.cs` - New context-based system
- `Services/IClassificationService.cs` - Interface for services

#### 3. API Endpoints

**New Controllers**:
- `Controllers/ClassificationController.cs`

**Endpoints Created**:
```
POST /api/classification/classify
POST /api/classification/classify/batch
GET  /api/classification/is-residential?input={text}
GET  /api/classification/is-business?input={text}
```

**Response Structure**:
```json
{
  "success": true,
  "input": "Smith Jack",
  "classification": "residential",
  "confidence": 85,
  "isBusiness": false,
  "isResidential": true,
  "reason": "valid name pattern",
  "businessScore": 15,
  "residentialScore": 85,
  "detailedAnalysis": {
    "words": ["smith", "jack"],
    "scores": { ... }
  }
}
```

#### 4. Data Import and Cleaning

**Problem**: Raw name data had business words miscategorized as names

**Solution**: Comprehensive cleaning scripts
- Remove titles (Mr., Mrs., Dr.)
- Split hyphenated names
- Filter business words with ratio check
- Handle duplicates with count aggregation

**Files Created**:
- `Database/import_names_clean.py`
- `Database/preprocess_names.py`
- `Database/import_names_from_csv.sql`
- `Database/direct_import_names.sql`
- `Database/cleanup_false_names.sql`
- `Database/smart_migration_with_ratio.sql`

#### 5. Configuration Changes

**appsettings.json**:
```json
{
  "ConnectionStrings": {
    "MariaDbConnection": "Server=localhost;Database=bor_db;..."
  },
  "UseContextClassification": true
}
```

**Program.cs**:
- Dynamic service registration based on config
- MariaDB specific version configuration
- CORS policy for API access

### Bug Fixes Implemented

1. **Database Connection Issues**
   - Fixed: ServerVersion.AutoDetect hanging
   - Solution: Explicit MariaDbServerVersion

2. **Transaction Conflicts**
   - Fixed: MySqlRetryingExecutionStrategy errors
   - Solution: Wrapped transactions in execution strategy

3. **Name Query Issues**
   - Fixed: Only finding one record per name
   - Solution: Query all records with `Where().ToListAsync()`

4. **Classification Accuracy**
   - Fixed: "Jones Soda" classified as residential
   - Solution: Business/name ratio checking

5. **Build Errors**
   - Fixed: Missing IsBusiness/IsResidential properties
   - Solution: Added properties to ClassificationResult

### Testing Tools Created

- `test_classification.ps1` - PowerShell test script
- `test_import_duplicates.sql` - Database import testing
- `check_names.sql` - Verify name data
- `check_dependencies.sql` - Find dependent objects

### Performance Optimizations

1. **Database Indexing**:
   - Composite index on (word_lower, word_type)
   - Separate indexes on word_lower, word_type, word_count

2. **Query Optimization**:
   - Single table lookups instead of multiple
   - Batch processing for multiple classifications
   - Memory caching for frequent lookups

3. **Code Optimization**:
   - Async/await throughout
   - Connection pooling
   - Retry strategies for resilience

### Breaking Changes

None - Backward compatibility maintained through:
- Wrapper functions for old function names
- Support for both old and new classification services
- Migration scripts preserve existing data

### Known Issues / Future Improvements

1. Consider removing backward compatibility functions after full migration
2. Old tables (names, words) can be deleted after verification
3. Consider implementing machine learning for classification
4. Add support for international business identifiers

### Deployment Checklist

- [ ] Backup existing database
- [ ] Run migration scripts in order
- [ ] Update appsettings.json
- [ ] Deploy new API code
- [ ] Run tests
- [ ] Monitor logs for errors
- [ ] Clean up old tables after verification

### Rollback Plan

If issues occur:
1. Restore from backup tables (names_backup_*, words_backup_*)
2. Set `UseContextClassification: false` in config
3. Revert to previous API version

### Metrics to Monitor

- Classification accuracy rate
- API response times
- Database query performance
- Memory usage with new caching