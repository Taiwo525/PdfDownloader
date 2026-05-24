# PDF Flyer Downloader
.NET console application that automatically downloads advertising flyer PDFs from external websites and saves them locally with standardized naming: `<source>_<yyyy-MM-dd>.pdf`

**Example:** `toom_2026-05-19.pdf`

---

## Approach

The solution follows **industry-standard architecture patterns** with clean separation of concerns and SOLID principles:

### Project Structure
```
PdfDownloader/
├── Core/                          # Business logic & domain
│   ├── Contracts.cs              # Interface definitions
│   ├── FlyerDownloader.cs        # Main download orchestration
│   ├── FlyerDownloadException.cs # Domain exception
│   └── PdfValidators.cs          # PDF validation logic
├── Infrastructure/                # External dependencies
│   ├── FileStorageService.cs     # File system operations
│   ├── HttpClientFactory.cs      # HTTP client creation
│   ├── LoggerFactory.cs          # Serilog configuration
│   ├── ResiliencePipelineFactory.cs # Polly retry policies
│   └── SystemTimeProvider.cs     # Time abstraction for testability
├── Models/                        # Data models & configuration
│   ├── AppConfiguration.cs       # HTTP & retry settings
│   └── AppOptions.cs             # Runtime options & parsing
├── Program.cs                     # Entry point & composition root
└── appsettings.json              # Configuration file
```

### Design Principles
- **Dependency Injection**: Constructor-based DI for testability
- **Interface Segregation**: Small, focused interfaces
- **Single Responsibility**: Each class has one clear purpose
- **Separation of Concerns**: Core logic isolated from infrastructure
- **Configuration over Code**: Externalized settings via appsettings.json

---

## Tools & Libraries

| Package | Version | Purpose |
|---------|---------|---------|
| **.NET 8** | - | Modern C# features, nullable reference types |
| **Polly** | 8.5.2 | Resilience & retry policies with exponential backoff |
| **Serilog.AspNetCore** | 10.0.0 | Structured logging framework |
| **Microsoft.Extensions.Configuration** | 10.0.8 | Configuration management |

---

## Configuration

### appsettings.json
```json
{
  "AppSettings": {
    "Source": "toom",
    "PdfUrl": "https://www.toom.de/medias/toom-Prospekt-KW21.pdf",
    "OutputDirectory": "C:\\Users\\YourName\\Desktop\\AIM"
  },
  "HttpSettings": {
    "TimeoutSeconds": 30,
    "MaxRetryAttempts": 3,
    "RetryDelaySeconds": 2,
    "UseExponentialBackoff": true,
    "UseJitter": true
  }
}
```

## Usage

### Basic Usage (Uses appsettings.json)
```bash
dotnet run --project PdfDownloader
dotnet run -- --source=toom --url=https://example.com/file.pdf --output=C:\Downloads
```

---

## Error Handling & Validation

The application implements **comprehensive error handling** at every layer:

### Network & HTTP Errors
- ✅ Connection timeouts (configurable)
- ✅ Network failures (automatic retry with exponential backoff)
- ✅ HTTP error status codes (4xx, 5xx)
- ✅ Retry on transient failures (408, 429, 5xx)
- ✅ Detailed logging of retry attempts

### Content Validation
- ✅ Content-Type validation (`application/pdf`, `application/x-pdf`)
- ✅ Empty file detection
- ✅ PDF magic bytes validation (`%PDF-` header)
- ✅ Content-Length logging

### Input Validation
- ✅ URL format validation (must be absolute HTTP/HTTPS)
- ✅ Source name sanitization (alphanumeric, hyphens, underscores only)
- ✅ Output directory validation
- ✅ Configuration value validation

### File Operations
- ✅ Directory creation if not exists
- ✅ File overwrite warning
- ✅ Proper stream disposal (no memory leaks)
- ✅ Async I/O throughout

### Exit Codes
- `0` - Success
- `1` - Unexpected runtime failure (fatal error)
- `2` - Expected download/validation failure (business logic error)

---

## Logging

### Structured Logging with Serilog
- **Timestamp** on every log entry
- **Log levels**: Information, Warning, Error, Fatal
- **Contextual properties**: URL, source, file size, retry attempts
- **Progress tracking**: Download progress for large files (every 1MB)

---

## Resilience & Retry Strategy

### Polly Resilience Pipeline
- **Max Retry Attempts**: 3 (configurable)
- **Backoff Strategy**: Exponential with jitter
- **Base Delay**: 2 seconds (configurable)
- **Jitter**: Randomized delays to prevent thundering herd

### Retry Conditions
- Network exceptions (`HttpRequestException`, `TaskCanceledException`)
- HTTP 408 (Request Timeout)
- HTTP 429 (Too Many Requests)
- HTTP 5xx (Server Errors)

### Non-Retryable Errors
- HTTP 4xx (except 408, 429) - Client errors
- Invalid content type
- Empty files
- Invalid PDF headers

---

## Known Limitations

1. **Direct PDF URLs Only**
   - Assumes direct PDF download links
   - No JavaScript rendering or dynamic page scraping
   - No authentication/authorization support

2. **Basic PDF Validation**
   - Only validates PDF magic bytes (`%PDF-`)
   - Does not perform full PDF structure parsing
   - Cannot detect corrupted PDFs beyond header check

3. **No Checksum Verification**
   - No MD5/SHA256 hash validation
   - Cannot verify file integrity from source

4. **UTC Timestamps**
   - File naming uses UTC date
   - Local timezone naming not supported

5. **Single File Download**
   - No batch processing of multiple URLs
   - No parallel downloads

6. **File Overwrite Behavior**
   - Existing files are overwritten with warning
   - No versioning or duplicate handling strategy

---

## What Would Be Improved with More Time

### Testing
- ✨ Unit tests for all classes (validators, parsers, factories)
- ✨ Integration tests with mocked HTTP responses
- ✨ Property-based testing for input validation
- ✨ Load testing for large file downloads

### Features
- ✨ Batch mode for multiple sources/URLs
- ✨ Parallel downloads with concurrency limits
- ✨ HEAD request pre-check for content-length validation
- ✨ File size limits to prevent disk exhaustion
- ✨ Checksum verification (MD5, SHA256)
- ✨ Duplicate file handling strategies (skip, version, overwrite)
- ✨ Support for authentication (Basic, Bearer tokens)
- ✨ Proxy configuration support

### Observability
- ✨ File-based logging with rolling intervals (Serilog.Sinks.File)
- ✨ Centralized logging (Seq, Elasticsearch, Application Insights)
- ✨ OpenTelemetry integration for distributed tracing
- ✨ Metrics export (Prometheus, StatsD)
- ✨ Health check endpoints

### Production Readiness
- ✨ Docker containerization
- ✨ Kubernetes deployment manifests
- ✨ CI/CD pipeline (GitHub Actions, Azure DevOps)
- ✨ Secrets management (Azure Key Vault, AWS Secrets Manager)
- ✨ Configuration validation on startup
- ✨ Graceful shutdown handling

---

## Technical Decisions & Trade-offs

### Why HttpClient Instead of RestSharp/Flurl?
- **Native .NET**: No external dependencies for simple HTTP GET
- **Performance**: Direct control over request/response handling
- **Polly Integration**: Seamless integration with resilience policies

### Why Serilog Instead of Microsoft.Extensions.Logging?
- **Structured Logging**: First-class support for structured data
- **Rich Sinks**: Wide ecosystem of output targets
- **Performance**: Efficient async logging

### Why Manual HttpClient Creation Instead of IHttpClientFactory?
- **Simplicity**: Single-use console app doesn't need socket pooling
- **Trade-off**: For high-frequency requests, IHttpClientFactory would be better
- **Note**: Documented in code comments for future improvement

### Why MemoryStream Instead of Direct File Write?
- **Validation**: Need to validate PDF header before saving
- **Atomic Operations**: Prevents partial file writes on validation failure
- **Trade-off**: Higher memory usage for large files

### Why UTC Timestamps?
- **Consistency**: Avoids timezone ambiguity
- **Automation**: Scheduled tasks run in different timezones
- **Trade-off**: May not match local business hours

---
