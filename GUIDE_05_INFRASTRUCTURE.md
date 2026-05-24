# Comprehensive Guide Part 5: Infrastructure Layer

## Table of Contents
1. [HttpClientFactory](#httpclientfactory)
2. [ResiliencePipelineFactory](#resiliencepipelinefactory)
3. [FileStorageService](#filestorageservice)
4. [LoggerFactory](#loggerfactory)
5. [SystemTimeProvider](#systemtimeprovider)

---

## 1. HttpClientFactory

```csharp
using PdfDownloader.Core;
using PdfDownloader.Models;

namespace PdfDownloader.Infrastructure;

public sealed class HttpClientFactory(HttpSettings settings) : Core.IHttpClientFactory
{
    public HttpClient Create()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds)
        };
        
        client.DefaultRequestHeaders.UserAgent.ParseAdd("PdfDownloader/1.0");
        
        return client;
    }
}
```

### Line-by-Line:

#### Line 6: Class Declaration
```csharp
public sealed class HttpClientFactory(HttpSettings settings) : Core.IHttpClientFactory
```

**sealed**: Cannot be inherited
**Primary constructor**: `(HttpSettings settings)` - parameter becomes field
**: Core.IHttpClientFactory**: Implements interface (fully qualified to avoid ambiguity)

**Why Core.IHttpClientFactory?**
- There's also `System.Net.Http.IHttpClientFactory` (from Microsoft.Extensions.Http)
- `Core.` prefix disambiguates which interface we mean

#### Lines 10-13: Create HttpClient
```csharp
var client = new HttpClient
{
    Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds)
};
```

**new HttpClient**: Creates new instance
**Object initializer**: `{ Timeout = ... }` sets properties during construction
**TimeSpan.FromSeconds**: Converts seconds to TimeSpan
**settings.TimeoutSeconds**: From configuration (default 30)

**What is TimeSpan?**
```csharp
TimeSpan.FromSeconds(30)      // 30 seconds
TimeSpan.FromMinutes(5)       // 5 minutes
TimeSpan.FromHours(1)         // 1 hour
TimeSpan.FromMilliseconds(500) // 0.5 seconds
```

**What happens on timeout?**
```csharp
// If request takes longer than 30 seconds:
await httpClient.GetAsync(url);  // Throws TaskCanceledException
```

#### Line 15: Set User-Agent Header
```csharp
client.DefaultRequestHeaders.UserAgent.ParseAdd("PdfDownloader/1.0");
```

**DefaultRequestHeaders**: Headers sent with every request
**UserAgent**: Identifies the application to the server
**ParseAdd**: Parses string and adds to collection

**HTTP Request looks like**:
```
GET /file.pdf HTTP/1.1
Host: example.com
User-Agent: PdfDownloader/1.0
```

**Why User-Agent?**
- ✅ Identifies your application
- ✅ Some servers block requests without User-Agent
- ✅ Helps server admins identify traffic source
- ✅ Professional/polite

---

## 2. ResiliencePipelineFactory

```csharp
using System.Net;
using PdfDownloader.Core;
using PdfDownloader.Models;
using Polly;
using Polly.Retry;
using Serilog;

namespace PdfDownloader.Infrastructure;

public sealed class ResiliencePipelineFactory(HttpSettings settings, ILogger logger) : IResiliencePipelineFactory
{
    public ResiliencePipeline<HttpResponseMessage> Create()
    {
        var retryOptions = new RetryStrategyOptions<HttpResponseMessage>
        {
            MaxRetryAttempts = settings.MaxRetryAttempts,
            Delay = TimeSpan.FromSeconds(settings.RetryDelaySeconds),
            BackoffType = settings.UseExponentialBackoff ? DelayBackoffType.Exponential : DelayBackoffType.Constant,
            UseJitter = settings.UseJitter,
            ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                .Handle<HttpRequestException>()
                .Handle<TaskCanceledException>()
                .HandleResult(r => IsRetryableStatusCode(r.StatusCode)),
            OnRetry = args =>
            {
                var reason = args.Outcome.Exception?.Message ?? $"HTTP {(int)args.Outcome.Result!.StatusCode} ({args.Outcome.Result.ReasonPhrase})";
                
                logger.Warning(
                    "Retry attempt {RetryAttempt} of {MaxRetryAttempts} after {Delay:F2}s delay. Reason: {Reason}",
                    args.AttemptNumber,
                    settings.MaxRetryAttempts,
                    args.RetryDelay.TotalSeconds,
                    reason);

                return ValueTask.CompletedTask;
            }
        };

        return new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(retryOptions)
            .Build();
    }

    private static bool IsRetryableStatusCode(HttpStatusCode statusCode)
    {
        var code = (int)statusCode;
        return code == 408 || code == 429 || code >= 500;
    }
}
```

### Line-by-Line:

#### Lines 14-19: Basic Retry Configuration
```csharp
var retryOptions = new RetryStrategyOptions<HttpResponseMessage>
{
    MaxRetryAttempts = settings.MaxRetryAttempts,
    Delay = TimeSpan.FromSeconds(settings.RetryDelaySeconds),
    BackoffType = settings.UseExponentialBackoff ? DelayBackoffType.Exponential : DelayBackoffType.Constant,
    UseJitter = settings.UseJitter,
```

**RetryStrategyOptions<HttpResponseMessage>**: Configuration for retry behavior
**Generic type**: `<HttpResponseMessage>` - what we're retrying

**MaxRetryAttempts**: How many times to retry (default 3)
- Attempt 1: Original request
- Attempt 2: First retry
- Attempt 3: Second retry
- Attempt 4: Third retry
- Total: 4 attempts

**Delay**: Base delay between retries (default 2 seconds)

**BackoffType**: How delay increases
- **Constant**: 2s, 2s, 2s (same delay)
- **Exponential**: 2s, 4s, 8s (doubles each time)

**Ternary operator**: `condition ? valueIfTrue : valueIfFalse`
```csharp
settings.UseExponentialBackoff ? DelayBackoffType.Exponential : DelayBackoffType.Constant
// If UseExponentialBackoff is true, use Exponential, otherwise Constant
```

**UseJitter**: Adds randomness to delays
- Without: 2s, 4s, 8s (predictable)
- With: 1.8s, 3.7s, 7.2s (randomized)

**Why jitter?** Prevents thundering herd problem:
```
Without jitter:
Client 1: Fails → waits 2s → retries at 12:00:02
Client 2: Fails → waits 2s → retries at 12:00:02
Client 3: Fails → waits 2s → retries at 12:00:02
All hit server at same time! 💥

With jitter:
Client 1: Fails → waits 1.8s → retries at 12:00:01.8
Client 2: Fails → waits 2.1s → retries at 12:00:02.1
Client 3: Fails → waits 1.9s → retries at 12:00:01.9
Spread out! ✅
```

#### Lines 20-23: Retry Conditions
```csharp
ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
    .Handle<HttpRequestException>()
    .Handle<TaskCanceledException>()
    .HandleResult(r => IsRetryableStatusCode(r.StatusCode)),
```

**ShouldHandle**: Defines WHEN to retry
**PredicateBuilder**: Fluent API for building conditions

**Handle<HttpRequestException>()**: Retry on network errors
- Connection refused
- DNS lookup failed
- Network unreachable

**Handle<TaskCanceledException>()**: Retry on timeouts
- Request exceeded HttpClient.Timeout
- CancellationToken was cancelled

**HandleResult(r => ...)**: Retry based on response
- **r**: HttpResponseMessage
- **Lambda**: `r => IsRetryableStatusCode(r.StatusCode)`
- **Returns**: true if should retry

#### Lines 24-36: Retry Callback
```csharp
OnRetry = args =>
{
    var reason = args.Outcome.Exception?.Message ?? 
        $"HTTP {(int)args.Outcome.Result!.StatusCode} ({args.Outcome.Result.ReasonPhrase})";
    
    logger.Warning(
        "Retry attempt {RetryAttempt} of {MaxRetryAttempts} after {Delay:F2}s delay. Reason: {Reason}",
        args.AttemptNumber,
        settings.MaxRetryAttempts,
        args.RetryDelay.TotalSeconds,
        reason);

    return ValueTask.CompletedTask;
};
```

**OnRetry**: Called before each retry attempt
**args**: Contains retry information

**Line 26**: Get failure reason
```csharp
var reason = args.Outcome.Exception?.Message ?? 
    $"HTTP {(int)args.Outcome.Result!.StatusCode} ({args.Outcome.Result.ReasonPhrase})";
```

**Null-coalescing operator**: `??`
- If left side is null, use right side
- If exception exists, use exception message
- Otherwise, use HTTP status code

**args.Outcome.Exception?.Message**: 
- **?.**: Null-conditional - returns null if Exception is null
- **Message**: Exception message

**args.Outcome.Result!.StatusCode**:
- **!**: Null-forgiving operator - tells compiler "I know this isn't null"
- **StatusCode**: HTTP status code enum

**Example reasons**:
- "The remote name could not be resolved: 'example.com'" (DNS error)
- "HTTP 503 (Service Unavailable)" (Server error)
- "A task was canceled" (Timeout)

**Lines 28-33**: Log retry attempt
```csharp
logger.Warning(
    "Retry attempt {RetryAttempt} of {MaxRetryAttempts} after {Delay:F2}s delay. Reason: {Reason}",
    args.AttemptNumber,
    settings.MaxRetryAttempts,
    args.RetryDelay.TotalSeconds,
    reason);
```

**Warning level**: Retries are unusual but not errors
**Structured logging**: Named properties for querying
**{Delay:F2}**: Format with 2 decimal places

**Example output**:
```
[2026-05-19 15:30:15 WRN] Retry attempt 1 of 3 after 2.00s delay. Reason: HTTP 503 (Service Unavailable)
[2026-05-19 15:30:19 WRN] Retry attempt 2 of 3 after 4.00s delay. Reason: HTTP 503 (Service Unavailable)
```

**Line 35**: Return completed task
```csharp
return ValueTask.CompletedTask;
```

**ValueTask**: Lightweight Task for synchronous operations
**CompletedTask**: Already-completed task (no async work)

#### Lines 39-42: Build Pipeline
```csharp
return new ResiliencePipelineBuilder<HttpResponseMessage>()
    .AddRetry(retryOptions)
    .Build();
```

**ResiliencePipelineBuilder**: Fluent API for building pipelines
**AddRetry**: Adds retry strategy
**Build**: Creates final pipeline

**Could add more strategies**:
```csharp
return new ResiliencePipelineBuilder<HttpResponseMessage>()
    .AddRetry(retryOptions)
    .AddTimeout(TimeSpan.FromSeconds(60))  // Overall timeout
    .AddCircuitBreaker(...)                // Stop calling failing service
    .Build();
```

#### Lines 44-48: Retryable Status Codes
```csharp
private static bool IsRetryableStatusCode(HttpStatusCode statusCode)
{
    var code = (int)statusCode;
    return code == 408 || code == 429 || code >= 500;
}
```

**private static**: Helper method, doesn't need instance
**HttpStatusCode**: Enum of HTTP status codes
**(int)statusCode**: Convert enum to integer

**Retryable codes**:
- **408**: Request Timeout - server took too long
- **429**: Too Many Requests - rate limited
- **500+**: Server errors (500, 502, 503, 504, etc.)

**Non-retryable codes**:
- **400**: Bad Request - won't succeed on retry
- **401**: Unauthorized - need credentials
- **403**: Forbidden - no permission
- **404**: Not Found - resource doesn't exist

---

## 3. FileStorageService

```csharp
using PdfDownloader.Core;
using Serilog;

namespace PdfDownloader.Infrastructure;

public sealed class FileStorageService(ILogger logger) : IFileStorageService
{
    public async Task<string> SaveAsync(
        string source,
        string outputDirectory,
        Stream content,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(outputDirectory);

        var fileName = $"{source}_{utcNow:yyyy-MM-dd}.pdf";
        var targetPath = Path.Combine(outputDirectory, fileName);

        if (File.Exists(targetPath))
        {
            logger.Warning("File {FileName} already exists and will be overwritten", fileName);
        }

        await using var fileStream = File.Create(targetPath);
        await content.CopyToAsync(fileStream, cancellationToken);
        await fileStream.FlushAsync(cancellationToken);

        var fileInfo = new FileInfo(targetPath);
        logger.Information("File saved successfully: {FileName}, Size: {FileSize} bytes", 
            fileName, fileInfo.Length);

        return targetPath;
    }
}
```

### Line-by-Line:

#### Line 16: Create Directory
```csharp
Directory.CreateDirectory(outputDirectory);
```

**Directory.CreateDirectory**: Static method
**Idempotent**: Safe to call if directory exists
**Creates parent directories**: If path is `C:\A\B\C`, creates A, B, and C

**Example**:
```csharp
Directory.CreateDirectory("C:\\Users\\Decagon\\Desktop\\AIM");
// Creates AIM folder if it doesn't exist
```

#### Line 18: Generate Filename
```csharp
var fileName = $"{source}_{utcNow:yyyy-MM-dd}.pdf";
```

**String interpolation**: `$"..."` allows embedding expressions
**{source}**: Source name (e.g., "toom")
**{utcNow:yyyy-MM-dd}**: Date format
- **yyyy**: 4-digit year (2026)
- **MM**: 2-digit month (05)
- **dd**: 2-digit day (19)

**Example**: `toom_2026-05-19.pdf`

#### Line 19: Combine Path
```csharp
var targetPath = Path.Combine(outputDirectory, fileName);
```

**Path.Combine**: Safely joins path segments
**Handles separators**: Uses correct separator for OS (\ on Windows, / on Linux)

**Example**:
```csharp
Path.Combine("C:\\Users\\Decagon\\Desktop\\AIM", "toom_2026-05-19.pdf")
// Returns: "C:\\Users\\Decagon\\Desktop\\AIM\\toom_2026-05-19.pdf"
```

**Why not string concatenation?**
```csharp
// ❌ Bad - might have double slashes or missing slashes
var path = outputDirectory + "\\" + fileName;

// ✅ Good - handles separators correctly
var path = Path.Combine(outputDirectory, fileName);
```

#### Lines 21-24: Check Existing File
```csharp
if (File.Exists(targetPath))
{
    logger.Warning("File {FileName} already exists and will be overwritten", fileName);
}
```

**File.Exists**: Returns true if file exists
**Warning**: Informs user file will be overwritten
**No exception**: Continues and overwrites

**Alternative strategies**:
```csharp
// Skip if exists
if (File.Exists(targetPath)) return targetPath;

// Version the file
if (File.Exists(targetPath))
{
    fileName = $"{source}_{utcNow:yyyy-MM-dd}_v2.pdf";
}

// Throw exception
if (File.Exists(targetPath))
{
    throw new IOException("File already exists");
}
```

#### Lines 26-28: Write File
```csharp
await using var fileStream = File.Create(targetPath);
await content.CopyToAsync(fileStream, cancellationToken);
await fileStream.FlushAsync(cancellationToken);
```

**Line 26**: `File.Create(targetPath)`
- Creates new file
- Returns FileStream
- **Overwrites** if file exists

**Line 27**: `content.CopyToAsync(fileStream, cancellationToken)`
- Copies all data from content stream to file stream
- **Async**: Doesn't block thread
- **Buffered**: Copies in chunks

**Line 28**: `fileStream.FlushAsync(cancellationToken)`
- Writes buffered data to disk
- **Ensures**: Data is physically written
- **Why?** OS might buffer writes in memory

**await using**: Automatically closes and disposes fileStream

#### Lines 30-32: Log Success
```csharp
var fileInfo = new FileInfo(targetPath);
logger.Information("File saved successfully: {FileName}, Size: {FileSize} bytes", 
    fileName, fileInfo.Length);
```

**FileInfo**: Provides file metadata
**Length**: File size in bytes
**Why FileInfo?** Gets actual size from disk (confirms write succeeded)

#### Line 34: Return Path
```csharp
return targetPath;
```

**Returns**: Full path to saved file
**Example**: `C:\Users\Decagon\Desktop\AIM\toom_2026-05-19.pdf`

---

## 4. LoggerFactory

```csharp
using Serilog;
using Serilog.Events;

namespace PdfDownloader.Infrastructure;

public static class LoggerFactory
{
    public static ILogger Create()
    {
        return new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "PdfDownloader")
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                path: "logs/pdfdownloader-.txt",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }
}
```

### Line-by-Line:

#### Line 11: Minimum Log Level
```csharp
.MinimumLevel.Information()
```

**Filters out**: Verbose and Debug logs
**Allows**: Information, Warning, Error, Fatal

**Log levels**:
```
Verbose   → Filtered
Debug     → Filtered
Information → ✅ Logged
Warning   → ✅ Logged
Error     → ✅ Logged
Fatal     → ✅ Logged
```

#### Lines 12-13: Override Noisy Namespaces
```csharp
.MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
.MinimumLevel.Override("System", LogEventLevel.Warning)
```

**Why?** Microsoft and System namespaces log too much
**Effect**: Only log Warning+ from those namespaces

**Without override**:
```
[INF] Microsoft.Extensions.Configuration: Loading configuration
[INF] System.Net.Http.HttpClient: Sending request
[INF] Microsoft.Extensions.Configuration: Configuration loaded
```

**With override**:
```
(No logs unless Warning or higher)
```

#### Line 14: Enrich from Context
```csharp
.Enrich.FromLogContext()
```

**What**: Adds properties from LogContext
**Usage**:
```csharp
using (LogContext.PushProperty("UserId", 123))
{
    logger.Information("User action");
    // Log includes UserId=123
}
```

#### Line 15: Static Property
```csharp
.Enrich.WithProperty("Application", "PdfDownloader")
```

**What**: Adds static property to ALL logs
**Result**: Every log entry has `Application="PdfDownloader"`

**Why?** Useful when aggregating logs from multiple applications

#### Lines 16-17: Console Sink
```csharp
.WriteTo.Console(
    outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
```

**WriteTo.Console**: Writes logs to console/terminal
**outputTemplate**: Custom format

**Template breakdown**:
- `[{Timestamp:yyyy-MM-dd HH:mm:ss}`: Date and time
- `{Level:u3}]`: Log level (3 chars uppercase: INF, WRN, ERR)
- `{Message:lj}`: Message (lj = literal JSON - escapes special chars)
- `{NewLine}`: Line break
- `{Exception}`: Exception details (if any)

**Example output**:
```
[2026-05-19 15:29:39 INF] Application started
[2026-05-19 15:29:40 WRN] Retry attempt 1
[2026-05-19 15:29:41 ERR] Download failed
```

#### Lines 18-22: File Sink
```csharp
.WriteTo.File(
    path: "logs/pdfdownloader-.txt",
    rollingInterval: RollingInterval.Day,
    retainedFileCountLimit: 30,
    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
```

**path**: `"logs/pdfdownloader-.txt"`
- **logs/**: Subdirectory
- **pdfdownloader-**: Prefix
- **-.txt**: Suffix (date inserted here)

**rollingInterval**: `RollingInterval.Day`
- Creates new file each day
- **Files created**:
  ```
  logs/pdfdownloader-20260519.txt
  logs/pdfdownloader-20260520.txt
  logs/pdfdownloader-20260521.txt
  ```

**retainedFileCountLimit**: `30`
- Keeps last 30 files
- Deletes older files automatically

**outputTemplate**: More detailed than console
- `{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}`: Includes milliseconds and timezone
- **fff**: Milliseconds (001-999)
- **zzz**: Timezone offset (+00:00)

**Example**:
```
2026-05-19 15:29:39.123 +00:00 [INF] Application started
```

#### Line 23: Create Logger
```csharp
.CreateLogger();
```

**Returns**: Configured `ILogger` instance
**Type**: `Serilog.ILogger` (not Microsoft.Extensions.Logging.ILogger)

---

## 5. SystemTimeProvider

```csharp
using PdfDownloader.Core;

namespace PdfDownloader.Infrastructure;

public sealed class SystemTimeProvider : ITimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
```

### Line-by-Line:

#### Line 7: Expression-Bodied Property
```csharp
public DateTime UtcNow => DateTime.UtcNow;
```

**=>**: Expression-bodied member (shorthand)
**Equivalent to**:
```csharp
public DateTime UtcNow 
{ 
    get { return DateTime.UtcNow; } 
}
```

**DateTime.UtcNow**: Static property returning current UTC time
**UTC**: Coordinated Universal Time (no timezone offset)

**Why UTC?**
- ✅ No timezone ambiguity
- ✅ Consistent across servers
- ✅ Easy to convert to local time

**Example**:
```csharp
var provider = new SystemTimeProvider();
var now = provider.UtcNow;  // 2026-05-19 15:29:39 UTC
```

**Why abstraction?**
```csharp
// ❌ Hard to test
var fileName = $"{source}_{DateTime.UtcNow:yyyy-MM-dd}.pdf";

// ✅ Easy to test
var fileName = $"{source}_{timeProvider.UtcNow:yyyy-MM-dd}.pdf";

// In tests:
var fakeProvider = new FakeTimeProvider 
{ 
    UtcNow = new DateTime(2026, 5, 19) 
};
```

---

## Summary

### Architecture Layers:
1. **Program.cs**: Entry point, composition root
2. **Models**: Configuration and options
3. **Core**: Business logic and interfaces
4. **Infrastructure**: External dependencies

### Key Patterns:
- **Dependency Injection**: Constructor injection
- **Interface Segregation**: Small, focused interfaces
- **Factory Pattern**: Creating complex objects
- **Options Pattern**: Separate loading from using
- **Repository Pattern**: FileStorageService abstracts file I/O

### Best Practices Applied:
✅ Async/await throughout
✅ Proper resource disposal (using statements)
✅ Structured logging
✅ Configuration externalization
✅ Retry with exponential backoff
✅ Comprehensive error handling
✅ Input validation
✅ Testable design (interfaces, time abstraction)

---

**End of Comprehensive Guide**

You now have a complete understanding of:
- C# fundamentals (sealed, static, required, init, async/await)
- NuGet packages (Polly, Serilog, Configuration)
- Every line of code in the application
- Why each design decision was made
