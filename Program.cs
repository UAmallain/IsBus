using IsBus.Data;
using IsBus.Services;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/isbusiness-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        builder =>
        {
            builder.AllowAnyOrigin()
                   .AllowAnyMethod()
                   .AllowAnyHeader();
        });
});

var connectionString = builder.Configuration.GetConnectionString("MariaDbConnection") 
    ?? "Server=localhost;Database=phonebook_db;User=root;Password=;";

Log.Information("Using connection string: Server={Server};Database={Database}", 
    connectionString.Contains("localhost") ? "localhost" : "remote", 
    "phonebook_db");

builder.Services.AddDbContext<PhonebookContext>(options =>
{
    // Use a specific version instead of AutoDetect to avoid hanging
    var serverVersion = new MariaDbServerVersion(new Version(10, 11, 0));
    
    options.UseMySql(connectionString, 
        serverVersion,
        mySqlOptions => 
        {
            mySqlOptions.EnableRetryOnFailure(
                maxRetryCount: 2,
                maxRetryDelay: TimeSpan.FromSeconds(3),
                errorNumbersToAdd: null);
            mySqlOptions.CommandTimeout(5);
        });
    options.EnableSensitiveDataLogging(builder.Environment.IsDevelopment());
    options.EnableDetailedErrors();
});

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IBusinessIndicatorService, BusinessIndicatorService>();
builder.Services.AddScoped<IWordFrequencyService, WordFrequencyService>();
builder.Services.AddScoped<IBusinessNameDetectionService, BusinessNameDetectionService>();
builder.Services.AddScoped<IWordProcessingService, WordProcessingService>();

builder.Services.AddHealthChecks();
    // Temporarily disabled to avoid startup issues
    // .AddDbContextCheck<PhonebookContext>();

var app = builder.Build();

// Enable Swagger in all environments for testing
// if (app.Environment.IsDevelopment())
// {
    app.UseSwagger();
    app.UseSwaggerUI();
// }

app.UseSerilogRequestLogging();

// Comment out HTTPS redirection as it might cause issues
// app.UseHttpsRedirection();

app.UseCors("AllowAll");

app.UseAuthorization();

app.MapControllers();

app.MapHealthChecks("/health");

try
{
    Log.Information("Starting IsBus API");
    
    // Get the URLs the app is listening on
    var urls = app.Urls;
    if (!urls.Any())
    {
        // Add default URLs if none specified
        app.Urls.Add("http://localhost:5000");
        app.Urls.Add("https://localhost:5001");
    }
    
    foreach (var url in app.Urls)
    {
        Log.Information("Now listening on: {Url}", url);
    }
    
    Log.Information("Application started. Press Ctrl+C to shut down.");
    Log.Information("Swagger UI available at: http://localhost:5000/swagger");
    
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application start-up failed");
}
finally
{
    Log.CloseAndFlush();
}