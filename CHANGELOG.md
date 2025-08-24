# IsBus API Change Log

All notable changes to the IsBus Business/Residential Classification API are documented here.

## [Unreleased] - 2025-08-22

### Added (Latest)
- **Initial and Connector Recognition**
  - Added `Initial` type to `WordTypeEnum` for single letter words (A-Z)
  - Added `Connector` type to `WordTypeEnum` for words like "&", "and", "or"
  - Implemented `IsInitial()` method to identify single letters with or without periods
  - Implemented `IsConnector()` method to identify name connectors
  - Added `CheckForInitialPatterns()` for comprehensive initial pattern detection
  - Patterns supported: "Smith J", "J Smith", "Smith J & M", "J & M Smith"
  - Enhanced `PatternAnalysis` with `HasInitialPattern` and `InitialPatternType` properties

- **Context-Based Classification System**
  - New `ContextClassificationService` that builds word context maps for classification
  - Context mapping shows word usage across all types (first, last, both, business, indeterminate, initial, connector)
  - Pattern analysis for better classification accuracy
  - Added `UseContextClassification` config option in appsettings.json

- **Unified Database Structure**
  - Created `word_data` table combining `names` and `words` tables
  - Single table with `word_type` field: 'first', 'last', 'both', 'business', 'indeterminate'
  - Migration scripts: `create_unified_words_table.sql`
  - Database cleanup scripts: `cleanup_old_tables.sql`, `check_database_usage.sql`

- **New Models**
  - `WordData` entity for unified word storage
  - `WordContext` class for context mapping
  - `WordTypeEnum` enumeration
  - `PatternAnalysis` class for pattern detection

- **Classification Endpoints**
  - `POST /api/classification/classify` - Main classification with detailed analysis
  - `POST /api/classification/classify/batch` - Batch processing up to 100 inputs
  - `GET /api/classification/is-residential` - Quick residential check
  - `GET /api/classification/is-business` - Quick business check

- **Database Import Tools**
  - `import_names_clean.py` - Python script for importing and cleaning name CSVs
  - `preprocess_names.py` - Preprocessor for creating clean CSVs
  - `import_names_from_csv.sql` - SQL import with cleaning logic
  - `direct_import_names.sql` - Direct SQL import with all cleaning in database
  - `test_import_duplicates.sql` - Test script for duplicate handling

- **Database Views and Functions**
  - `v_word_classification` - Comprehensive word classification view
  - `v_multi_type_words` - Words appearing in multiple categories
  - `update_word_data_count()` - Unified function for updating word counts
  - Updated views: `v_recent_activity`, `v_top_business_words`, `v_top_names`

### Changed
- **Classification Logic Improvements**
  - Absolute business indicators (Inc, Ltd, Corp, LLC) now override all other rules with 100% confidence
  - Single word inputs now classified as business (residential requires first + last name)
  - Minimum name count threshold (10) to distinguish real names from noise
  - Word-to-name ratio analysis for better classification
  - Support for "LastName FirstName" pattern in addition to "FirstName LastName"
  - Enhanced handling of names with type="both"

- **Database Context**
  - Switched from `phonebook_db` to `bor_db` database
  - Updated connection strings in Program.cs and appsettings.json
  - Added `WordData` DbSet to PhonebookContext

- **Service Updates**
  - `ClassificationService` now checks both `names` and `words` tables comprehensively
  - Added `IsBusiness` and `IsResidential` boolean properties to `ClassificationResult`
  - Fixed queries to handle multiple name type records per word
  - Improved possessive pattern detection (e.g., "John's Pizza")

### Fixed
- **Classification Issues**
  - Fixed "Smith J & M" incorrectly classified as business (now correctly identifies initials and connectors)
  - Fixed single letter words not being recognized as initials (J, M, etc.)
  - Fixed "&" and "and" not being recognized as connectors in name patterns
  - Fixed "Driftwood Park Retreat Inc" incorrectly classified as residential
  - Fixed "Smith Jack" not being recognized as residential when both are names
  - Fixed "Jones Soda" being classified as residential despite "soda" being a business word
  - Resolved issue with names having multiple type entries not being found correctly
  - Fixed ambiguous column error in migration scripts

- **Database Issues**
  - Fixed MariaDB connection hanging with ServerVersion.AutoDetect
  - Resolved transaction strategy conflicts with MySqlRetryingExecutionStrategy
  - Fixed composite unique index issues on names table

- **Build Issues**
  - Added missing `IsBusiness` and `IsResidential` properties to ClassificationResult
  - Removed unused variable warnings
  - Fixed test project structure and references

### Migration Notes
1. Run database migration: `mysql -u root -p bor_db < Database/create_unified_words_table.sql`
2. Update views/functions: `mysql -u root -p bor_db < Database/update_views_and_functions.sql`
3. Import name data if needed: Use `import_names_clean.py` or `direct_import_names.sql`
4. Set `UseContextClassification: true` in appsettings.json to use new context-based system
5. After verification, cleanup old tables: `mysql -u root -p bor_db < Database/cleanup_old_tables.sql`

### Technical Debt
- Old `names` and `words` tables can be removed after migration verification
- Backward compatibility functions (`update_name_count`, `update_word_count`) can be removed in future version

---

## Version History Format

### [Version] - YYYY-MM-DD

#### Added
- New features or capabilities

#### Changed  
- Changes to existing functionality

#### Deprecated
- Features that will be removed in future versions

#### Removed
- Features that were removed

#### Fixed
- Bug fixes

#### Security
- Security improvements or fixes

---

## Notes for Developers

When making changes to the API:
1. Document the change in this file immediately
2. Include the date and category (Added/Changed/Fixed/etc.)
3. Reference any relevant files or scripts
4. Note any migration requirements
5. Update version number when releasing