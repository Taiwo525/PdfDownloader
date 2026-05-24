# Comprehensive Guide Part 2: NuGet Packages & Libraries

## Table of Contents
1. [Polly - Resilience & Retry](#polly)
2. [Serilog - Structured Logging](#serilog)
3. [Microsoft.Extensions.Configuration](#configuration)
4. [HttpClient](#httpclient)

---

## 1. Polly - Resilience & Retry

### What is Polly?
**Official**: A .NET resilience and transient-fault-handling library.
**Simple**: Automatically retries failed operations with smart strategies.

### Package:
```xml
<PackageReference Include="Polly" Version="8.5.2" />
```

### Why Use Polly?
✅ **Automatic retries** - handles transient failures
✅ **Exponential backoff** - waits longer between retries
✅ **Jitter** - randomizes delays to prevent thundering herd
✅ **Circuit breaker** - stops calling failing services

### Basic Retry Example:
```csharp
// Without Polly - manual retry (messy!)
for (int i = 0; i < 3; i++)
{
    try
    {
        return await httpClient.GetAsync(url);
    }
    catch (HttpRequestException)
    {
        if (i == 2) throw;
        await Task.Delay(2000);
    }
}

// With Polly - clean and powerful!
var pipeline = new ResiliencePipelineBuilder<HttpResponseMessage>()
    .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
    {
        MaxRetryAttempts = 3,
        Delay = TimeSpan.FromSeconds(2)
    })
    .Build();

return await pipeline.ExecuteAsync(async ct => 
    await httpClient.GetAsync(url, ct), cancellationToken);
```

### Our Implementation:
```csharp
public ResiliencePipeline<HttpResponseMessage> Create()
{
    var retryOptions = new RetryStrategyOptions<HttpResponseMessage>
    {
        MaxRetryAttempts = 3,                    // Try 3 times
        Delay = TimeSpan.FromSeconds(2),         // Wait 2s between retries
        BackoffType = DelayBackoffType.Exponential,  // 2s, 4s, 8s
        UseJitter = true,                        // Add randomness
        
        // When to retry?
        ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
            .Handle<HttpRequestException>()      // Network errors
            .Handle<TaskCanceledException>()     // Timeouts
            .HandleResult(r => IsRetryableStatusCode(r.StatusCode)),  // 5xx errors
        
        // What to do on retry?
        OnRetry = args =>
        {
            logger.Warning("Retry {Attempt} after {Delay}s", 
                args.AttemptNumber, args.RetryDelay.TotalSeconds);
            return ValueTask.CompletedTask;
        }
    };

    return new ResiliencePipelineBuilder<HttpResponseMessage>()
        .AddRetry(retryOptions)
        .Build();
}
```

### Exponential Backoff:
```
Attempt 1: Immediate
Attempt 2: Wait 2 seconds
Attempt 3: Wait 4 seconds (2 * 2)
Attempt 4: Wait 8 seconds (4 * 2)
```

### Jitter (Randomness):
```
Without Jitter: 2s, 4s, 8s (predictable)
With Jitter:    1.8s, 3.7s, 7.2s (randomized)
```
**Why?** Prevents all clients from retrying at the same time (thundering herd).

### Retryable Status Codes:
```csharp
private static bool IsRetryableStatusCode(HttpStatusCode statusCode)
{
    var code = (int)statusCode;
    return code == 408 ||  // Request Timeout
           code == 429 ||  // Too Many Requests
           code >= 500;    // Server Errors (500, 502, 503, etc.)
}
```

**Don't retry**: 400, 401, 403, 404 (client errors - won't succeed on retry)

---

## 2. Serilog - Structured Logging

### What is Serilog?
**Official**: A diagnostic logging library for .NET applications.
**Simple**: Logs with structure (not just strings).

### Package:
```xml
<PackageReference Include="Serilog.AspNetCore" Version="10.0.0" />
```
**Note**: `Serilog.AspNetCore` includes Console, File, and Debug sinks.

### Traditional Logging (String-based):
```csharp
// ❌ String interpolation - hard to query
logger.LogInformation($"Downloaded {size} bytes from {url}");
```

### Structured Logging (Property-based):
```csharp
// ✅ Structured - easy to query and analyze
logger.Information("Downloaded {ByteSize} bytes from {Url}", size, url);
```

### Log Levels:
```csharp
logger.Verbose("Very detailed");      // Rarely used
logger.Debug("Debugging info");       // Development only
logger.Information("Normal flow");    // ✅ Default
logger.Warning("Something unusual");  // ⚠️ Attention needed
logger.Error("Operation failed");     // ❌ Error occurred
logger.Fatal("App crashing");         // 💀 Critical failure
```

### Our Configuration:
```csharp
return new LoggerConfiguration()
    // Minimum level - filters out Debug and Verbose
    .MinimumLevel.Information()
    
    // Override for noisy namespaces
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    
    // Enrich with context
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "PdfDownloader")
    
    // Console sink
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    
    // File sink with daily rolling
    .WriteTo.File(
        path: "logs/pdfdownloader-.txt",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30)
    
    .CreateLogger();
```

### Output Template Explained:
```
[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}
```
- `{Timestamp:yyyy-MM-dd HH:mm:ss}` - Date and time
- `{Level:u3}` - Log level (INF, WRN, ERR)
- `{Message:lj}` - Log message (lj = literal JSON)
- `{NewLine}` - Line break
- `{Exception}` - Exception details (if any)

### Example Output:
```
[2026-05-19 15:29:39 INF] === PDF Downloader Application Started ===
[2026-05-19 15:29:39 INF] HTTP Configuration - Timeout: 30s, Max Retries: 3
[2026-05-19 15:29:40 WRN] Retry attempt 1 of 3 after 2.00s delay
[2026-05-19 15:29:42 ERR] Download failed with HTTP 500 (Internal Server Error)
```

### Sinks:
- **Console**: Writes to terminal
- **File**: Writes to log files
- **Seq**: Centralized logging server
- **Elasticsearch**: Search and analytics
- **Application Insights**: Azure monitoring

### Rolling Files:
```csharp
.WriteTo.File(
    path: "logs/pdfdownloader-.txt",
    rollingInterval: RollingInterval.Day,
    retainedFileCountLimit: 30)
```
Creates:
```
logs/pdfdownloader-20260519.txt
logs/pdfdownloader-20260520.txt
logs/pdfdownloader-20260521.txt
...
```
Keeps last 30 days, deletes older files.

---

## 3. Microsoft.Extensions.Configuration

### What is it?
**Official**: Configuration abstraction for .NET applications.
**Simple**: Loads settings from JSON, environment variables, command-line.

### Packages:
```xml
<PackageReference Include="Microsoft.Extensions.Configuration.Json" Version="10.0.8" />
<PackageReference Include="Microsoft.Extensions.Configuration.Binder" Version="10.0.8" />
<PackageReference Include="Microsoft.Extensions.Configuration.EnvironmentVariables" Version="10.0.8" />
```

### Configuration Sources:
```csharp
var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .AddEnvironmentVariables(prefix: "PDFDOWNLOADER_")
    .Build();
```

### Priority (Last wins):
1. appsettings.json (lowest)
2. Environment variables
3. Command-line arguments (highest)

### Binding to Classes:
```csharp
// appsettings.json
{
  "HttpSettings": {
    "TimeoutSeconds": 30,
    "MaxRetryAttempts": 3
  }
}

// C# class
public class HttpSettings
{
    public int TimeoutSeconds { get; set; }
    public int MaxRetryAttempts { get; set; }
}

// Bind
var httpSettings = new HttpSettings();
configuration.GetSection("HttpSettings").Bind(httpSettings);
// httpSettings.TimeoutSeconds = 30
// httpSettings.MaxRetryAttempts = 3
```

### Environment Variables:
```bash
# Set environment variable
export PDFDOWNLOADER_HttpSettings__TimeoutSeconds=60

# Overrides appsettings.json value
```

### Command-Line Arguments:
```bash
dotnet run -- --source=toom --url=https://example.com/file.pdf
```

---

## 4. HttpClient

### What is HttpClient?
**Official**: Provides a base class for sending HTTP requests.
**Simple**: Makes web requests (GET, POST, etc.).

### Basic Usage:
```csharp
using var httpClient = new HttpClient();
var response = await httpClient.GetAsync("https://example.com");
var content = await response.Content.ReadAsStringAsync();
```

### Our Usage:
```csharp
public HttpClient Create()
{
    var client = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(30)
    };
    
    client.DefaultRequestHeaders.UserAgent.ParseAdd("PdfDownloader/1.0");
    
    return client;
}
```

### HttpCompletionOption:
```csharp
// ❌ ResponseContentRead - waits for entire response
var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead);

// ✅ ResponseHeadersRead - returns immediately after headers
var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
```

**Why ResponseHeadersRead?**
- ✅ Faster - doesn't wait for body
- ✅ Memory efficient - can stream large files
- ✅ Can validate headers before downloading body

### Reading Response:
```csharp
// Check status
if (!response.IsSuccessStatusCode)
{
    throw new Exception($"HTTP {(int)response.StatusCode}");
}

// Check content type
var contentType = response.Content.Headers.ContentType?.MediaType;
if (contentType != "application/pdf")
{
    throw new Exception("Not a PDF");
}

// Read content as stream
await using var stream = await response.Content.ReadAsStreamAsync();
```

### User-Agent Header:
```csharp
client.DefaultRequestHeaders.UserAgent.ParseAdd("PdfDownloader/1.0");
```
**Why?** Identifies your application to the server. Some servers block requests without User-Agent.

### Timeout:
```csharp
client.Timeout = TimeSpan.FromSeconds(30);
```
**What happens?** If request takes longer than 30 seconds, throws `TaskCanceledException`.

### Socket Exhaustion Warning:
```csharp
// ❌ Bad - creates new HttpClient each time
public async Task DownloadAsync()
{
    using var client = new HttpClient();  // Don't do this in a loop!
    await client.GetAsync(url);
}

// ✅ Good - reuse HttpClient
private static readonly HttpClient _client = new HttpClient();
public async Task DownloadAsync()
{
    await _client.GetAsync(url);
}
```

**Why?** Each HttpClient holds sockets. Creating many can exhaust available sockets.

---

**Next**: [Part 3 - Code Walkthrough](GUIDE_03_CODE_WALKTHROUGH.md)
