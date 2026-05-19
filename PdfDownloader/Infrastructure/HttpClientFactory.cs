using PdfDownloader.Core;

namespace PdfDownloader.Infrastructure;

public sealed class HttpClientFactory : IHttpClientFactory
{
    public HttpClient Create()
    {
        return new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
    }
}
