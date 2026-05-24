# Comprehensive Guide Part 3: Line-by-Line Code Walkthrough

## Table of Contents
1. [Program.cs - Application Entry Point](#programcs)
2. [Models - Configuration & Options](#models)
3. [Core - Business Logic](#core)
4. [Infrastructure - External Dependencies](#infrastructure)

---

## 1. Program.cs - Application Entry Point

### Complete File:
```csharp
using Microsoft.Extensions.Configuration;
using PdfDownloader.Core;
using PdfDownloader.Infrastructure;
using PdfDownloader.Models;
using Serilog;

Log.Logger = LoggerFactory.Create();

try
{
    Log.Information("=== PDF Downloader Application Started ===");
    
    var configuration = new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
        .AddEnvironmentVariables(prefix: "PDFDOWNLOADER_")
        .Build();

    var appSettings = new AppSettings();
    configuration.GetSection("AppSettings").Bind(appSettings);
    
    var httpSettings = new HttpSettings();
    configuration.GetSection("HttpSettings").Bind(httpSettings);
    httpSettings.Validate();
    
    Log.Information("HTTP Configuration - Timeout: {Timeout}s, Max Retries: {MaxRetries}, Retry Delay: {RetryDelay}s",
        httpSettings.TimeoutSeconds,
        httpSettings.MaxRetryAttempts,
        httpSettings.RetryDelaySeconds);

    var options = AppOptions.Create(appSettings, args, Log.Logger);

    var downloader = new FlyerDownloader(
        new HttpClientFactory(httpSettings),
        new ResiliencePipelineFactory(httpSettings, Log.Logger),
        new FileStorageService(Log.Logger),
        Log.Logger,
        new SystemTimeProvider());

    await downloader.DownloadAsync(options, CancellationToken.None);
    
    Log.Information("=== PDF Downloader Application Completed Successfully ===");
    return 0;
}
catch (FlyerDownloadException ex)
{
    Log.Error(ex, "Flyer download failed: {Message}", ex.Message);
    Log.Information("=== PDF Downloader Application Failed ===");
    return 2;
}
catch (Exception ex)
{
    Log.Fatal(ex, "Unexpected error occurred");
    Log.Information("=== PDF Downloader Application Failed ===");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}
```

### Line-by-Line Explanation:

#### Lines 1-5: Using Directives
```csharp
using Microsoft.Extensions.Configuration;  // Configuration system
using PdfDownloader.Core;                  // Business logic interfaces
using PdfDownloader.Infrastructure;        // Implementation classes
using PdfDownloader.Models;                // Data models
using Serilog;                             // Logging
```
**What**: Import namespaces to use their types
**Why**: Avoids writing full type names (e.g., `Serilog.ILogger` → `ILogger`)

#### Line 7: Initialize Logger
```csharp
Log.Logger = LoggerFactory.Create();
```
**What**: Creates and sets the global Serilog logger
**Why**: Must be done BEFORE any logging calls
**How**: Calls our static factory method that configures console + file sinks
**Type**: `Log.Logger` is a static property of type `ILogger`

#### Line 9: Try Block
```csharp
try
{
```
**What**: Begins exception handling block
**Why**: Catches and handles all errors gracefully
**Pattern**: Try-Catch-Finally for resource cleanup

#### Line 11: Log Application Start
```csharp
Log.Information("=== PDF Downloader Application Started ===");
```
**What**: Writes an Information-level log entry
**Why**: Marks the beginning of execution in logs
**Output**: `[2026-05-19 15:29:39 INF] === PDF Downloader Application Started ===`

#### Lines 13-16: Build Configuration
```csharp
var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .AddEnvironmentVariables(prefix: "PDFDOWNLOADER_")
    .Build();
```

**Line 13**: `new ConfigurationBuilder()`
- **What**: Creates a builder for configuration
- **Type**: `IConfigurationBuilder`
- **Pattern**: Fluent API (method chaining)

**Line 14**: `.SetBasePath(AppContext.BaseDirectory)`
- **What**: Sets the directory to look for config files
- **AppContext.BaseDirectory**: Directory where the .exe is located
- **Example**: `C:\Users\Decagon\Desktop\AIM\PdfDownloader\bin\Debug\net8.0\`

**Line 15**: `.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)`
- **What**: Loads settings from JSON file
- **optional: false**: Throws exception if file not found
- **reloadOnChange: false**: Doesn't watch for file changes (not needed for console app)

**Line 16**: `.AddEnvironmentVariables(prefix: "PDFDOWNLOADER_")`
- **What**: Loads environment variables starting with prefix
- **Example**: `PDFDOWNLOADER_HttpSettings__TimeoutSeconds=60`
- **Why**: Allows overriding settings without changing files

**Line 17**: `.Build()`
- **What**: Creates the final `IConfiguration` object
- **Returns**: `IConfiguration` with all sources merged

#### Lines 18-20: Bind AppSettings
```csharp
var appSettings = new AppSettings();
configuration.GetSection("AppSettings").Bind(appSettings);
```

**Line 18**: `new AppSettings()`
- **What**: Creates empty AppSettings object with default values
- **Defaults**: `Source = "toom"`, `PdfUrl = ""`, `OutputDirectory = ""`

**Line 19**: `configuration.GetSection("AppSettings").Bind(appSettings)`
- **GetSection("AppSettings")**: Gets the "AppSettings" section from JSON
- **Bind(appSettings)**: Populates the object properties from JSON
- **How**: Uses reflection to match JSON keys to property names

**JSON Example**:
```json
{
  "AppSettings": {
    "Source": "toom",
    "PdfUrl": "https://example.com/file.pdf",
    "OutputDirectory": "C:\\Users\\Decagon\\Desktop\\AIM"
  }
}
```
After binding:
- `appSettings.Source = "toom"`
- `appSettings.PdfUrl = "https://example.com/file.pdf"`
- `appSettings.OutputDirectory = "C:\\Users\\Decagon\\Desktop\\AIM"`

#### Lines 22-24: Bind and Validate HttpSettings
```csharp
var httpSettings = new HttpSettings();
configuration.GetSection("HttpSettings").Bind(httpSettings);
httpSettings.Validate();
```

**Line 22-23**: Same binding process as AppSettings

**Line 24**: `httpSettings.Validate()`
- **What**: Calls custom validation method
- **Why**: Ensures values are within acceptable ranges
- **Throws**: `FlyerDownloadException` if invalid

#### Lines 26-29: Log Configuration
```csharp
Log.Information("HTTP Configuration - Timeout: {Timeout}s, Max Retries: {MaxRetries}, Retry Delay: {RetryDelay}s",
    httpSettings.TimeoutSeconds,
    httpSettings.MaxRetryAttempts,
    httpSettings.RetryDelaySeconds);
```

**What**: Structured logging with named properties
**Placeholders**: `{Timeout}`, `{MaxRetries}`, `{RetryDelay}`
**Values**: Passed as separate arguments (not string interpolation!)
**Output**: `[2026-05-19 15:29:39 INF] HTTP Configuration - Timeout: 30s, Max Retries: 3, Retry Delay: 2s`

**Why structured?** Can query logs by property:
```sql
SELECT * FROM Logs WHERE MaxRetries > 3
```

#### Line 31: Create AppOptions
```csharp
var options = AppOptions.Create(appSettings, args, Log.Logger);
```

**What**: Transforms AppSettings → AppOptions
**args**: Command-line arguments (string array)
**Process**:
1. Merges appSettings with command-line args
2. Validates URL format
3. Sanitizes source name
4. Returns immutable AppOptions

**Example**:
```bash
dotnet run -- --source=test --url=https://example.com/file.pdf
```
- `args[0] = "--source=test"`
- `args[1] = "--url=https://example.com/file.pdf"`

#### Lines 33-38: Create Dependencies (Manual DI)
```csharp
var downloader = new FlyerDownloader(
    new HttpClientFactory(httpSettings),
    new ResiliencePipelineFactory(httpSettings, Log.Logger),
    new FileStorageService(Log.Logger),
    Log.Logger,
    new SystemTimeProvider());
```

**What**: Manual dependency injection (composition root)
**Pattern**: Constructor injection

**Dependencies**:
1. `HttpClientFactory` - Creates configured HttpClient
2. `ResiliencePipelineFactory` - Creates Polly retry pipeline
3. `FileStorageService` - Saves files to disk
4. `Log.Logger` - Serilog logger
5. `SystemTimeProvider` - Provides current UTC time

**Why manual?** Simple console app doesn't need DI container

#### Line 40: Execute Download
```csharp
await downloader.DownloadAsync(options, CancellationToken.None);
```

**await**: Waits for async operation to complete
**CancellationToken.None**: No cancellation support (runs until complete or error)
**What happens**: Downloads PDF, validates, saves to disk

#### Lines 42-43: Success Path
```csharp
Log.Information("=== PDF Downloader Application Completed Successfully ===");
return 0;
```

**return 0**: Exit code 0 = success
**Why exit codes?** Allows scripts/schedulers to detect success/failure

#### Lines 45-49: Handle Expected Errors
```csharp
catch (FlyerDownloadException ex)
{
    Log.Error(ex, "Flyer download failed: {Message}", ex.Message);
    Log.Information("=== PDF Downloader Application Failed ===");
    return 2;
}
```

**FlyerDownloadException**: Our custom domain exception
**When thrown**: Invalid URL, wrong content-type, empty file, etc.
**return 2**: Exit code 2 = expected business logic failure
**Log.Error(ex, ...)**: Logs exception with stack trace

#### Lines 50-54: Handle Unexpected Errors
```csharp
catch (Exception ex)
{
    Log.Fatal(ex, "Unexpected error occurred");
    Log.Information("=== PDF Downloader Application Failed ===");
    return 1;
}
```

**Exception**: Catches ALL other exceptions
**When thrown**: Null reference, out of memory, etc.
**return 1**: Exit code 1 = unexpected failure
**Log.Fatal**: Highest severity level

#### Lines 55-58: Cleanup
```csharp
finally
{
    await Log.CloseAndFlushAsync();
}
```

**finally**: ALWAYS executes (even if exception thrown)
**CloseAndFlushAsync()**: 
- Writes any buffered logs to disk
- Closes file handles
- Releases resources

**Why async?** File I/O is async for performance

---

## 2. Models - Configuration & Options

### AppSettings.cs

```csharp
namespace PdfDownloader.Models;

public sealed class AppSettings
{
    public string Source { get; set; } = "toom";
    public string PdfUrl { get; set; } = string.Empty;
    public string OutputDirectory { get; set; } = string.Empty;
}
```

#### Line 3: Sealed Class
```csharp
public sealed class AppSettings
```
**sealed**: Cannot be inherited
**Why**: This is a DTO (Data Transfer Object) - not designed for extension

#### Lines 5-7: Properties
```csharp
public string Source { get; set; } = "toom";
```

**public**: Accessible from anywhere
**string**: Property type
**Source**: Property name
**{ get; set; }**: Auto-property (compiler generates backing field)
**= "toom"**: Default value if not set by configuration binder

**Why set?** Configuration binder needs to SET values
**Why defaults?** Fallback if JSON is missing values

---

### HttpSettings.cs

```csharp
using PdfDownloader.Core;

namespace PdfDownloader.Models;

public sealed class HttpSettings
{
    public int TimeoutSeconds { get; set; } = 30;
    public int MaxRetryAttempts { get; set; } = 3;
    public int RetryDelaySeconds { get; set; } = 2;
    public bool UseExponentialBackoff { get; set; } = true;
    public bool UseJitter { get; set; } = true;

    public void Validate()
    {
        if (TimeoutSeconds <= 0)
        {
            throw new FlyerDownloadException("TimeoutSeconds must be greater than 0.");
        }

        if (MaxRetryAttempts < 0)
        {
            throw new FlyerDownloadException("MaxRetryAttempts must be 0 or greater.");
        }

        if (RetryDelaySeconds < 0)
        {
            throw new FlyerDownloadException("RetryDelaySeconds must be 0 or greater.");
        }
    }
}
```

#### Lines 7-11: Configuration Properties
```csharp
public int TimeoutSeconds { get; set; } = 30;
```
**int**: Integer type (whole numbers)
**= 30**: Default 30 seconds timeout

#### Lines 13-29: Validation Method
```csharp
public void Validate()
{
    if (TimeoutSeconds <= 0)
    {
        throw new FlyerDownloadException("TimeoutSeconds must be greater than 0.");
    }
    // ... more validations
}
```

**void**: Returns nothing
**throw**: Stops execution and throws exception
**Why validate?** Catch configuration errors early (fail fast)

**Example**:
```json
{
  "HttpSettings": {
    "TimeoutSeconds": -5  // ❌ Invalid!
  }
}
```
Throws: `FlyerDownloadException: TimeoutSeconds must be greater than 0.`

---

### AppOptions.cs (Key Parts)

```csharp
public sealed class AppOptions
{
    public required string Source { get; init; }
    public required Uri PdfUrl { get; init; }
    public required string OutputDirectory { get; init; }
```

#### Line 5: Required + Init
```csharp
public required string Source { get; init; }
```

**required**: MUST be set during initialization
**init**: Can ONLY be set during initialization (immutable after)
**Why required?** Compile-time safety - prevents null
**Why init?** Thread-safety - cannot change after creation

**Comparison**:
```csharp
// ❌ Compiler error - Source not set
var options = new AppOptions();

// ✅ Works - all required properties set
var options = new AppOptions 
{ 
    Source = "test",
    PdfUrl = new Uri("https://example.com"),
    OutputDirectory = "C:\\Downloads"
};

// ❌ Compiler error - cannot change after init
options.Source = "changed";
```

#### Line 6: Uri Type
```csharp
public required Uri PdfUrl { get; init; }
```

**Uri vs string**: 
- `string`: Any text (could be invalid URL)
- `Uri`: Validated, parsed URL object

**Benefits**:
```csharp
Uri url = new Uri("https://example.com/file.pdf");
string scheme = url.Scheme;  // "https"
string host = url.Host;      // "example.com"
string path = url.AbsolutePath;  // "/file.pdf"
```

---

**Continue to next section?** This is getting long. Should I create Part 4 for Core and Infrastructure?
