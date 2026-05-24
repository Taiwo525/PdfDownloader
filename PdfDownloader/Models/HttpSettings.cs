//using PdfDownloader.Core;

namespace PdfDownloader.Models;

/// <summary>
/// HTTP client and resilience configuration settings loaded from appsettings.json.
/// Controls timeout, retry behavior, and backoff strategies.
/// 
/// This is a Data Transfer Object (DTO) for configuration binding.
/// </summary>
public sealed class HttpSettings
{
    public int TimeoutSeconds { get; set; } = 30;
    public int MaxRetryAttempts { get; set; } = 3;
    public int RetryDelaySeconds { get; set; } = 2;
    public bool UseExponentialBackoff { get; set; } = true;
    public bool UseJitter { get; set; } = true;

   
}
