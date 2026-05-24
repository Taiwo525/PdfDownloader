using PdfDownloader.Core;
using Serilog;

namespace PdfDownloader.Infrastructure;

/// <summary>
/// Service for saving downloaded PDF files to the file system.
/// </summary>
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

        // Check if file already exists and handle accordingly
        if (File.Exists(targetPath))
        {
            logger.Warning("File {FileName} already exists and will be overwritten", fileName);
        }

        await using var fileStream = File.Create(targetPath);
        await content.CopyToAsync(fileStream, cancellationToken);
        await fileStream.FlushAsync(cancellationToken);

        var fileInfo = new FileInfo(targetPath);
        logger.Information("File saved successfully: {FileName}, Size: {FileSize} bytes", fileName, fileInfo.Length);

        return targetPath;
    }
}

