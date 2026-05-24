using PdfDownloader.Models;
using Serilog;

namespace PdfDownloader.Core;

public sealed class FlyerDownloader(
    IHttpClientFactory httpClientFactory,
    IResiliencePipelineFactory resiliencePipelineFactory,
    IFileStorageService fileStorageService,
    ILogger logger,
    ITimeProvider timeProvider)
{
    public async Task DownloadAsync(AppOptions options, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(options.OutputDirectory);

        using var httpClient = httpClientFactory.Create();
        var pipeline = resiliencePipelineFactory.Create();

        logger.Information("Starting PDF download from {Url} for source '{Source}'", options.PdfUrl, options.Source);

        using var response = await pipeline.ExecuteAsync(async token =>
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, options.PdfUrl);
            return await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        }, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            logger.Error("Download failed with HTTP {StatusCode} ({ReasonPhrase})", (int)response.StatusCode, response.ReasonPhrase);
            throw new FlyerDownloadException($"Download failed with HTTP {(int)response.StatusCode} ({response.ReasonPhrase}).");
        }

        logger.Information("Download successful, validating content");

        var contentType = response.Content.Headers.ContentType?.MediaType;
        if (contentType is null || !PdfValidators.IsPdfContentType(contentType))
        {
            logger.Error("Invalid content-type: {ContentType}", contentType ?? "(null)");
            throw new FlyerDownloadException($"Invalid content-type '{contentType ?? "(null)"}'. Expected a PDF content type.");
        }

        var contentLength = response.Content.Headers.ContentLength;
        if (contentLength.HasValue)
        {
            logger.Information("Content-Type: {ContentType}, Content-Length: {ContentLength} bytes", contentType, contentLength.Value);
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
                
                // Log progress for large files (every 1MB)
                if (totalBytesRead % (1024 * 1024) == 0 || (totalBytesRead / (1024 * 1024)) != ((totalBytesRead - bytesRead) / (1024 * 1024)))
                {
                    logger.Information("Downloaded {BytesRead} MB so far...", totalBytesRead / (1024 * 1024));
                }
            }

            logger.Information("Download complete. Total size: {TotalBytes} bytes ({TotalMB:F2} MB)", totalBytesRead, totalBytesRead / (1024.0 * 1024.0));

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
    }
}
