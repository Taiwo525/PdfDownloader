# Comprehensive Guide Part 4: Core & Infrastructure Deep Dive

## Table of Contents
1. [Core/Contracts.cs - Interfaces](#contracts)
2. [Core/FlyerDownloader.cs - Main Logic](#flyerdownloader)
3. [Core/PdfValidators.cs - Validation](#validators)
4. [Infrastructure Classes](#infrastructure)

---

## 1. Core/Contracts.cs - Interfaces

```csharp
using Polly;

namespace PdfDownloader.Core;

public interface IHttpClientFactory
{
    HttpClient Create();
}

public interface IResiliencePipelineFactory
{
    ResiliencePipeline<HttpResponseMessage> Create();
}

public interface IFileStorageService
{
    Task<string> SaveAsync(string source, string outputDirectory, Stream content, 
        DateTime utcNow, CancellationToken cancellationToken);
}

public interface ITimeProvider
{
    DateTime UtcNow { get; }
}
```

### What is an Interface?
**Definition**: A contract that defines WHAT methods a class must implement (not HOW).

**Analogy**: Like a job description - defines responsibilities, not how to do them.

### Line-by-Line:

#### Lines 5-8: IHttpClientFactory
```csharp
public interface IHttpClientFactory
{
    HttpClient Create();
}
```

**What**: Defines a factory for creating HttpClient instances
**Method**: `Create()` returns a configured `HttpClient`
**Why interface?** Allows swapping implementations (testing, different configs)

**Implementation**:
```csharp
public class HttpClientFactory : IHttpClientFactory
{
    public HttpClient Create() 
    {
        return new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }
}
```

**Testing**:
```csharp
public class MockHttpClientFactory : IHttpClientFactory
{
    public HttpClient Create() 
    {
        return new HttpClient(new MockHttpMessageHandler());
    }
}
```

#### Lines 10-13: IResiliencePipelineFactory
```csharp
public interface IResiliencePipelineFactory
{
    ResiliencePipeline<HttpResponseMessage> Create();
}
```

**What**: Factory for creating Polly resilience pipelines
**ResiliencePipeline<HttpResponseMessage>**: Polly's retry wrapper for HTTP responses
**Why generic?** `<HttpResponseMessage>` specifies what type the pipeline handles

#### Lines 15-18: IFileStorageService
```csharp
public interface IFileStorageService
{
    Task<string> SaveAsync(string source, string outputDirectory, Stream content, 
        DateTime utcNow, CancellationToken cancellationToken);
}
```

**Task<string>**: Async method returning a string (the saved file path)
**Parameters**:
- `source`: Source name (e.g., "toom")
- `outputDirectory`: Where to save (e.g., "C:\\Downloads")
- `content`: Stream of PDF data
- `utcNow`: Current UTC time for filename
- `cancellationToken`: For cancellation support

**Why DateTime parameter?** Testability - can inject fake time for testing

#### Lines 20-23: ITimeProvider
```csharp
public interface ITimeProvider
{
    DateTime UtcNow { get; }
}
```

**What**: Abstraction over `DateTime.UtcNow`
**Why?** `DateTime.UtcNow` is static - hard to test

**Without abstraction**:
```csharp
// ❌ Hard to test - always uses real time
var fileName = $"{source}_{DateTime.UtcNow:yyyy-MM-dd}.pdf";
```

**With abstraction**:
```csharp
// ✅ Easy to test - can inject fake time
var fileName = $"{source}_{timeProvider.UtcNow:yyyy-MM-dd}.pdf";

// In tests:
var fakeTime = new FakeTimeProvider { UtcNow = new DateTime(2026, 5, 19) };
```

---

## 2. Core/FlyerDownloader.cs - Main Business Logic

### Constructor (Primary Constructor Syntax)
```csharp
public sealed class FlyerDownloader(
    IHttpClientFactory httpClientFactory,
    IResiliencePipelineFactory resiliencePipelineFactory,
    IFileStorageService fileStorageService,
    ILogger logger,
    ITimeProvider timeProvider)
```

**Primary Constructor**: C# 12 feature - parameters become fields automatically

**Equivalent old syntax**:
```csharp
public sealed class FlyerDownloader
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IResiliencePipelineFactory _resiliencePipelineFactory;
    // ... more fields
    
    public FlyerDownloader(
        IHttpClientFactory httpClientFactory,
        IResiliencePipelineFactory resiliencePipelineFactory,
        // ... more parameters
    )
    {
        _httpClientFactory = httpClientFactory;
        _resiliencePipelineFactory = resiliencePipelineFactory;
        // ... more assignments
    }
}
```

**Benefits**: Less boilerplate, cleaner code

### DownloadAsync Method - Part 1: Setup

```csharp
public async Task DownloadAsync(AppOptions options, CancellationToken cancellationToken)
{
    Directory.CreateDirectory(options.OutputDirectory);

    using var httpClient = httpClientFactory.Create();
    var pipeline = resiliencePipelineFactory.Create();

    logger.Information("Starting PDF download from {Url} for source '{Source}'", 
        options.PdfUrl, options.Source);
```

#### Line 1: Method Signature
```csharp
public async Task DownloadAsync(AppOptions options, CancellationToken cancellationToken)
```

**public**: Accessible from anywhere
**async**: Method contains async operations
**Task**: Returns a Task (async void equivalent)
**DownloadAsync**: Convention - async methods end with "Async"
**Parameters**: Options and cancellation token

#### Line 3: Create Directory
```csharp
Directory.CreateDirectory(options.OutputDirectory);
```

**What**: Creates directory if it doesn't exist
**Idempotent**: Safe to call multiple times - does nothing if exists
**Example**: `C:\Users\Decagon\Desktop\AIM`

#### Line 5: Create HttpClient
```csharp
using var httpClient = httpClientFactory.Create();
```

**using**: Automatically disposes HttpClient when method exits
**var**: Type inference - compiler knows it's `HttpClient`
**Why factory?** Centralizes configuration (timeout, headers)

**Disposal**:
```csharp
using var httpClient = ...;
// Use httpClient
// Automatically calls httpClient.Dispose() at end of method
```

#### Line 6: Create Resilience Pipeline
```csharp
var pipeline = resiliencePipelineFactory.Create();
```

**What**: Creates Polly retry pipeline
**Type**: `ResiliencePipeline<HttpResponseMessage>`
**Why?** Wraps HTTP calls with retry logic

### Part 2: HTTP Request with Retry

```csharp
using var response = await pipeline.ExecuteAsync(async token =>
{
    using var request = new HttpRequestMessage(HttpMethod.Get, options.PdfUrl);
    return await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
}, cancellationToken);
```

#### Line 1: Execute with Retry
```csharp
using var response = await pipeline.ExecuteAsync(async token =>
```

**pipeline.ExecuteAsync**: Executes code with retry logic
**async token =>**: Lambda expression (anonymous function)
**token**: CancellationToken passed by Polly
**await**: Waits for async operation

**What happens**:
1. Executes the lambda
2. If fails with retryable error → waits → retries
3. If succeeds → returns response
4. If all retries fail → throws exception

#### Lines 3-4: Send HTTP Request
```csharp
using var request = new HttpRequestMessage(HttpMethod.Get, options.PdfUrl);
return await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
```

**HttpRequestMessage**: Represents an HTTP request
**HttpMethod.Get**: GET request (retrieve data)
**options.PdfUrl**: URL to download from
**HttpCompletionOption.ResponseHeadersRead**: Return as soon as headers received (don't wait for body)
**token**: Cancellation token from Polly

**Why ResponseHeadersRead?**
- ✅ Faster - can validate headers before downloading body
- ✅ Memory efficient - can stream large files
- ✅ Can check content-type before downloading

### Part 3: Validation

```csharp
if (!response.IsSuccessStatusCode)
{
    logger.Error("Download failed with HTTP {StatusCode} ({ReasonPhrase})", 
        (int)response.StatusCode, response.ReasonPhrase);
    throw new FlyerDownloadException($"Download failed with HTTP {(int)response.StatusCode} ({response.ReasonPhrase}).");
}

logger.Information("Download successful, validating content");

var contentType = response.Content.Headers.ContentType?.MediaType;
if (contentType is null || !PdfValidators.IsPdfContentType(contentType))
{
    logger.Error("Invalid content-type: {ContentType}", contentType ?? "(null)");
    throw new FlyerDownloadException($"Invalid content-type '{contentType ?? "(null)"}'. Expected a PDF content type.");
}
```

#### Lines 1-6: Check HTTP Status
```csharp
if (!response.IsSuccessStatusCode)
```

**IsSuccessStatusCode**: True if status code is 200-299
**StatusCode**: Enum (e.g., `HttpStatusCode.OK` = 200)
**(int)response.StatusCode**: Converts enum to integer
**ReasonPhrase**: Human-readable status (e.g., "Not Found")

**Examples**:
- 200 OK → `IsSuccessStatusCode = true`
- 404 Not Found → `IsSuccessStatusCode = false`
- 500 Internal Server Error → `IsSuccessStatusCode = false`

#### Lines 10-15: Check Content-Type
```csharp
var contentType = response.Content.Headers.ContentType?.MediaType;
```

**response.Content**: HTTP response body
**Headers**: HTTP headers
**ContentType**: Content-Type header
**?.MediaType**: Null-conditional operator - returns null if ContentType is null
**MediaType**: The actual type (e.g., "application/pdf")

**Example headers**:
```
Content-Type: application/pdf
Content-Length: 13264
```

**Validation**:
```csharp
if (contentType is null || !PdfValidators.IsPdfContentType(contentType))
```

**is null**: Pattern matching - checks if null
**!PdfValidators.IsPdfContentType**: Calls validator method
**Accepts**: "application/pdf" or "application/x-pdf"

### Part 4: Download with Progress

```csharp
var contentLength = response.Content.Headers.ContentLength;
if (contentLength.HasValue)
{
    logger.Information("Content-Type: {ContentType}, Content-Length: {ContentLength} bytes", 
        contentType, contentLength.Value);
}
else
{
    logger.Information("Content-Type: {ContentType}, Content-Length: unknown", contentType);
}

await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
var memoryStream = new MemoryStream();

try
{
    var totalBytesRead = 0L;
    var buffer = new byte[8192];
    int bytesRead;
    
    while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
    {
        await memoryStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
        totalBytesRead += bytesRead;
        
        if (totalBytesRead % (1024 * 1024) == 0 || 
            (totalBytesRead / (1024 * 1024)) != ((totalBytesRead - bytesRead) / (1024 * 1024)))
        {
            logger.Information("Downloaded {BytesRead} MB so far...", totalBytesRead / (1024 * 1024));
        }
    }
```

#### Lines 1-10: Log Content Length
```csharp
var contentLength = response.Content.Headers.ContentLength;
if (contentLength.HasValue)
```

**ContentLength**: Nullable long (`long?`)
**HasValue**: True if not null
**Why nullable?** Server might not send Content-Length header

#### Line 12: Read Response Stream
```csharp
await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
```

**await using**: Async disposal
**ReadAsStreamAsync**: Gets response body as stream
**Why stream?** Memory efficient for large files

#### Line 13: Create Memory Buffer
```csharp
var memoryStream = new MemoryStream();
```

**Why MemoryStream?** Need to:
1. Validate PDF header before saving
2. Reset position to read again
3. Prevent partial file writes on validation failure

#### Lines 17-19: Initialize Loop Variables
```csharp
var totalBytesRead = 0L;
var buffer = new byte[8192];
int bytesRead;
```

**0L**: Long literal (L suffix)
**byte[8192]**: 8KB buffer
**bytesRead**: Will hold bytes read in each iteration

#### Lines 21-31: Download Loop
```csharp
while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
{
    await memoryStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
    totalBytesRead += bytesRead;
    
    if (totalBytesRead % (1024 * 1024) == 0 || 
        (totalBytesRead / (1024 * 1024)) != ((totalBytesRead - bytesRead) / (1024 * 1024)))
    {
        logger.Information("Downloaded {BytesRead} MB so far...", totalBytesRead / (1024 * 1024));
    }
}
```

**Line 21**: `while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)`
- **Assignment in condition**: Reads bytes AND assigns to `bytesRead`
- **> 0**: Continues while data available
- **Returns 0**: When end of stream reached

**Line 23**: `await memoryStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken)`
- **buffer.AsMemory(0, bytesRead)**: Creates memory slice from 0 to bytesRead
- **Why slice?** Buffer might not be full (last chunk)
- **Example**: Buffer is 8192 bytes, but only 100 bytes read → write only those 100

**Line 24**: `totalBytesRead += bytesRead`
- **+=**: Add and assign
- **Tracks**: Total bytes downloaded

**Lines 26-30**: Progress logging
- **% (1024 * 1024)**: Modulo - remainder after dividing by 1MB
- **Logs**: Every time we cross a MB boundary
- **Example**: Logs at 1MB, 2MB, 3MB, etc.

### Part 5: Validation and Save

```csharp
    logger.Information("Download complete. Total size: {TotalBytes} bytes ({TotalMB:F2} MB)", 
        totalBytesRead, totalBytesRead / (1024.0 * 1024.0));

    if (memoryStream.Length == 0)
    {
        logger.Error("Downloaded file is empty");
        throw new FlyerDownloadException("Downloaded file is empty.");
    }

    memoryStream.Position = 0;
    if (!await PdfValidators.HasPdfHeaderAsync(memoryStream, cancellationToken))
    {
        logger.Error("Downloaded content does not have a valid PDF header");
        throw new FlyerDownloadException("Downloaded content does not look like a valid PDF file header.");
    }

    logger.Information("PDF validation successful");

    memoryStream.Position = 0;
    var savedPath = await fileStorageService.SaveAsync(
        options.Source,
        options.OutputDirectory,
        memoryStream,
        timeProvider.UtcNow,
        cancellationToken);

    logger.Information("Successfully saved flyer to {Path}", savedPath);
}
finally
{
    await memoryStream.DisposeAsync();
}
```

#### Lines 1-2: Log Completion
```csharp
logger.Information("Download complete. Total size: {TotalBytes} bytes ({TotalMB:F2} MB)", 
    totalBytesRead, totalBytesRead / (1024.0 * 1024.0));
```

**{TotalMB:F2}**: Format specifier - 2 decimal places
**1024.0**: Double literal (forces floating-point division)
**Output**: `Download complete. Total size: 13264 bytes (0.01 MB)`

#### Lines 4-8: Empty File Check
```csharp
if (memoryStream.Length == 0)
```

**Length**: Total bytes in stream
**Why check?** Server might return 200 OK with empty body

#### Lines 10-15: PDF Header Validation
```csharp
memoryStream.Position = 0;
if (!await PdfValidators.HasPdfHeaderAsync(memoryStream, cancellationToken))
```

**Position = 0**: Reset stream to beginning
**Why?** We just wrote to it - position is at end
**HasPdfHeaderAsync**: Checks for "%PDF-" magic bytes

#### Lines 19-25: Save File
```csharp
memoryStream.Position = 0;
var savedPath = await fileStorageService.SaveAsync(
    options.Source,
    options.OutputDirectory,
    memoryStream,
    timeProvider.UtcNow,
    cancellationToken);
```

**Position = 0**: Reset again for reading
**SaveAsync**: Writes stream to file
**Returns**: Full path where file was saved

#### Lines 28-31: Cleanup
```csharp
finally
{
    await memoryStream.DisposeAsync();
}
```

**finally**: ALWAYS executes
**DisposeAsync**: Releases memory
**Why?** MemoryStream not in `using` because we need it in finally

---

**Continue to Infrastructure section?** This is comprehensive but getting long. Should I create Part 5 for Infrastructure classes?
