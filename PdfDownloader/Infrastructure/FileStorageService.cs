using PdfDownloader.Core;

namespace PdfDownloader.Infrastructure;

public sealed class FileStorageService : IFileStorageService
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

        await using var fileStream = File.Create(targetPath);
        await content.CopyToAsync(fileStream, cancellationToken);

        return targetPath;
    }
}
