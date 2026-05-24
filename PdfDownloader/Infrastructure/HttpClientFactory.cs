using PdfDownloader.Core;
using PdfDownloader.Models;

namespace PdfDownloader.Infrastructure;

/// <summary>
/// Factory for creating configured HttpClient instances.
/// Note: For production applications with multiple requests, consider using Microsoft.Extensions.Http.IHttpClientFactory
/// to avoid socket exhaustion issues.
/// </summary>
public sealed class HttpClientFactory(HttpSettings settings) : Core.IHttpClientFactory
{
    public HttpClient Create()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds)
        };

        // Set a user agent to identify the application
        client.DefaultRequestHeaders.UserAgent.ParseAdd("PdfDownloader/1.0");

        return client;
    }
}
