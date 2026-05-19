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
    Task<string> SaveAsync(string source, string outputDirectory, Stream content, DateTime utcNow, CancellationToken cancellationToken);
}
