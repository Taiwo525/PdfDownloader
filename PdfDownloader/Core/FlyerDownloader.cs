using PdfDownloader.Models;
using Serilog;

namespace PdfDownloader.Core;

public sealed class FlyerDownloader(
    IHttpClientFactory httpClientFactory,
    IResiliencePipelineFactory resiliencePipelineFactory,
    IFileStorageService fileStorageService,
    ILogger logger)
{
    public async Task DownloadAsync(AppOptions options, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(options.OutputDirectory);

        using var httpClient = httpClientFactory.Create();
        var pipeline = resiliencePipelineFactory.Create();

        logger.Information("Downloading PDF from {Url}", options.PdfUrl);

        using var response = await pipeline.ExecuteAsync(async token =>
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, options.PdfUrl);
            return await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        }, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new FlyerDownloadException($"Download failed with HTTP {(int)response.StatusCode} ({response.ReasonPhrase}).");
        }

        var contentType = response.Content.Headers.ContentType?.MediaType;
        if (contentType is null || !PdfValidators.IsPdfContentType(contentType))
        {
            throw new FlyerDownloadException($"Invalid content-type '{contentType ?? "(null)"}'. Expected a PDF content type.");
        }

        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var memoryStream = new MemoryStream();
        await contentStream.CopyToAsync(memoryStream, cancellationToken);

        if (memoryStream.Length == 0)
        {
            throw new FlyerDownloadException("Downloaded file is empty.");
        }

        memoryStream.Position = 0;
        if (!await PdfValidators.HasPdfHeaderAsync(memoryStream, cancellationToken))
        {
            throw new FlyerDownloadException("Downloaded content does not look like a valid PDF file header.");
        }

        memoryStream.Position = 0;
        var savedPath = await fileStorageService.SaveAsync(
            options.Source,
            options.OutputDirectory,
            memoryStream,
            DateTime.UtcNow,
            cancellationToken);

        logger.Information("Successfully saved flyer to {Path}", savedPath);
    }
}
