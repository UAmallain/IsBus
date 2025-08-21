# IsBus - Business Name Detection API

A C# Web API that determines if a given string is likely a business name with a confidence rating, integrating with MariaDB for word frequency tracking.

## Features

- Business name detection with confidence scoring (0-100)
- Configurable business indicators
- MariaDB integration for word frequency tracking
- RESTful API with Swagger documentation
- Comprehensive unit tests
- Structured logging with Serilog
- Health checks endpoint

## Prerequisites

- .NET 8.0 SDK
- MariaDB/MySQL Server
- Visual Studio 2022 or VS Code (optional)

## Database Setup

Create the database and table:

```sql
CREATE DATABASE IF NOT EXISTS phonebook_db;
USE phonebook_db;

CREATE TABLE IF NOT EXISTS words (
    word_id INT AUTO_INCREMENT PRIMARY KEY,
    word_lower VARCHAR(255) NOT NULL UNIQUE,
    word_count INT NOT NULL DEFAULT 1
);
```

## Configuration

Update `appsettings.json` with your MariaDB connection string:

```json
{
  "ConnectionStrings": {
    "MariaDbConnection": "Server=localhost;Database=phonebook_db;User=your_user;Password=your_password;"
  }
}
```

## Running the Application

1. Clone the repository
2. Navigate to the project directory
3. Restore dependencies:
   ```bash
   dotnet restore
   ```
4. Run the application:
   ```bash
   dotnet run
   ```
5. Access Swagger UI at: `https://localhost:5001/swagger` or `http://localhost:5000/swagger`

## API Endpoints

### POST /api/business-name/check
Checks if a string is likely a business name.

Request:
```json
{
  "input": "Microsoft Corporation"
}
```

Response:
```json
{
  "input": "Microsoft Corporation",
  "isBusinessName": true,
  "confidence": 85.5,
  "matchedIndicators": ["Corporation"],
  "wordsProcessed": 1
}
```

### GET /api/business-name/health
Health check endpoint.

### GET /health
Application health check.

## Confidence Scoring Algorithm

The confidence score (0-100) is calculated based on:

1. **Business Indicators (40 points max)**
   - Primary suffixes (LLC, Inc, Corp, etc.): 25 points each
   - Secondary indicators (Services, Solutions, etc.): 15 points each

2. **Capitalization Patterns (20 points max)**
   - Proper capitalization of words
   - All-caps abbreviations

3. **Structure Score (20 points max)**
   - Optimal word count (2-6 words)
   - Proper formatting patterns

4. **Separators (10 points max)**
   - Ampersands (&)
   - Hyphens (-)
   - Commas and periods

5. **Length & Complexity (10 points max)**
   - Appropriate length (10-50 characters optimal)
   - Presence of numbers
   - Common business patterns

## Running Tests

```bash
dotnet test
```

## Project Structure

```
IsBus/
├── Controllers/          # API Controllers
├── Data/                # Database context
├── Models/              # Data models
├── Services/            # Business logic services
├── Tests/               # Unit tests
├── appsettings.json     # Configuration
└── Program.cs           # Application entry point
```

## Technologies Used

- ASP.NET Core 8.0
- Entity Framework Core 8.0
- Pomelo.EntityFrameworkCore.MySql
- Serilog for logging
- xUnit for testing
- Moq for mocking
- FluentAssertions for test assertions
- Swagger/OpenAPI for API documentation

## License

This project is provided as-is for demonstration purposes.